use std::collections::{BTreeMap, HashMap};
use std::fs;
use std::io::{self, Write};

use opencadcodec::entities::{AcisData, EntityCommon, Insert};
use opencadcodec::objects::ObjectType;
use opencadcodec::tables::BlockRecord;
use opencadcodec::types::Transform;
use opencadcodec::{
    CadDocument, Color, DwgReader, DxfReader, EntityType, Handle, LineWeight, Transparency,
};
use opencadkernel::brep::mesh::{self, TessellationTolerance};
use serde_json::json;

use crate::cli::Cli;
use crate::geometry;
use crate::limits::{ensure_within, ResourceBudget, ResourceLimitError};
use crate::scene::{
    AffineTransform, ConversionStats, PrimitiveKey, Rgba8, Scene, SceneNode, SceneObject,
};

const MAX_INSERT_DEPTH: usize = 32;
const DEFAULT_SOURCE_FIT: f64 = 1.0e-6;
const TARGET_BATCH_POSITION_ELEMENTS: usize = 500_000;
const TARGET_BATCH_TRIANGLES: usize = 250_000;
const TARGET_BATCH_LINE_SEGMENTS: usize = 250_000;

#[derive(Debug)]
pub struct ConversionError {
    pub exit_code: u8,
    pub message: String,
}

impl ConversionError {
    /// Creates an error for invalid command-line arguments or input paths.
    fn invalid(message: impl Into<String>) -> Self {
        Self {
            exit_code: 2,
            message: message.into(),
        }
    }

    /// Creates an error for failures while reading or decoding CAD data.
    fn import(message: impl Into<String>) -> Self {
        Self {
            exit_code: 4,
            message: message.into(),
        }
    }

    /// Creates an error for geometry conversion or GLB export failures.
    fn export(message: impl Into<String>) -> Self {
        Self {
            exit_code: 5,
            message: message.into(),
        }
    }

    /// Creates an error for an exceeded resource budget.
    fn resource(error: impl Into<String>) -> Self {
        Self {
            exit_code: 7,
            message: error.into(),
        }
    }
}

impl From<ResourceLimitError> for ConversionError {
    /// Converts a resource-limit failure into the worker's resource error type.
    fn from(error: ResourceLimitError) -> Self {
        Self::resource(error.to_string())
    }
}

/// Converts the requested CAD file into a GLB while enforcing configured limits.
pub fn convert(cli: &Cli) -> Result<(), ConversionError> {
    validate(cli)?;
    let input_bytes = fs::metadata(&cli.input)
        .map_err(|error| ConversionError::invalid(format!("cannot inspect CAD input: {error}")))?
        .len();
    ensure_within("input bytes", input_bytes, cli.limits.input_bytes)?;
    emit_progress(1, "validating");

    let format = cli.format.to_ascii_lowercase();
    emit_progress(5, "reading");
    let document = match format.as_str() {
        "dwg" | "dws" | "dwt" => {
            let mut reader = DwgReader::from_file(&cli.input).map_err(|error| {
                ConversionError::import(format!("cannot open CAD file: {error}"))
            })?;
            reader
                .read()
                .map_err(|error| ConversionError::import(format!("DWG import failed: {error}")))?
        }
        "dxf" => DxfReader::from_file(&cli.input)
            .and_then(DxfReader::read)
            .map_err(|error| ConversionError::import(format!("DXF import failed: {error}")))?,
        _ => unreachable!("validated format"),
    };
    emit_progress(25, "imported");

    let budget = ResourceBudget::new(cli.limits);
    budget.ensure_document_entities(document.entity_count())?;

    let source_units = document.header.insertion_units;
    let meters_per_unit = meters_per_drawing_unit(source_units).unwrap_or_else(|| {
        eprintln!(
            "warning: INSUNITS {source_units} is unitless or unsupported; treating one drawing unit as one metre"
        );
        1.0
    });
    let linear_deflection = cli.linear_deflection_meters / meters_per_unit;
    if !linear_deflection.is_finite() || linear_deflection <= 0.0 {
        return Err(ConversionError::invalid(
            "linear deflection cannot be represented in drawing units",
        ));
    }
    let reference_span = geometry::reference_span_from_extents(
        document.header.model_space_extents_min,
        document.header.model_space_extents_max,
        linear_deflection,
    );

    let model_entities = document.model_space_entities().cloned().collect::<Vec<_>>();
    let mut worker = Worker {
        document: &document,
        include_hidden: cli.include_hidden,
        export_edge_wires: cli.export_edge_wires,
        angular_deflection: cli.angular_deflection_degrees.to_radians(),
        linear_deflection,
        reference_span,
        budget,
        scene: Scene {
            meters_per_unit,
            source_units,
            ..Default::default()
        },
        block_variants: BTreeMap::new(),
        block_prototypes: Vec::new(),
        stats: ConversionStats {
            source_units,
            meters_per_unit,
            linear_deflection_drawing_units: linear_deflection,
            document_entities: document.entity_count(),
            model_space_entities: model_entities.len(),
            ..Default::default()
        },
    };

    let total = model_entities.len().max(1);
    let mut last_progress = 25_u8;
    let mut model_geometry = GeometryBatches::default();
    let mut model_blocks = Vec::new();
    for (index, entity) in model_entities.into_iter().enumerate() {
        worker.visit(
            entity,
            0,
            &mut Vec::new(),
            1.0,
            &mut model_geometry,
            &mut model_blocks,
        )?;
        let progress = 25 + (((index + 1) * 60) / total) as u8;
        let progress = progress.min(85);
        if progress > last_progress {
            emit_progress(progress, "converting");
            last_progress = progress;
        }
    }
    emit_progress(88, "instancing");
    worker.finish_model_scene(model_geometry, model_blocks)?;

    if !worker.stats.unsupported_entities.is_empty() {
        eprintln!(
            "warning: intentionally unsupported annotation/proxy entities were omitted: {}",
            serde_json::to_string(&worker.stats.unsupported_entities)
                .unwrap_or_else(|_| "<unavailable>".to_owned())
        );
    }
    if worker.stats.missing_references > 0 {
        eprintln!(
            "warning: {} empty, missing, partial, or external block reference instance(s) were not fully expanded",
            worker.stats.missing_references
        );
    }
    if worker.stats.cyclic_references > 0 {
        eprintln!(
            "warning: {} cyclic or over-depth block reference(s) were stopped",
            worker.stats.cyclic_references
        );
    }
    if worker.stats.invalid_native_faces > 0 {
        eprintln!(
            "warning: {} malformed native mesh face(s) were omitted",
            worker.stats.invalid_native_faces
        );
    }
    if worker.stats.degenerate_native_faces > 0 {
        eprintln!(
            "warning: {} zero-area native mesh face(s) were omitted",
            worker.stats.degenerate_native_faces
        );
    }

    if cli.require_complete_geometry {
        let problems = worker.completeness_problems();
        if !problems.is_empty() {
            return Err(ConversionError::export(format!(
                "strict geometry completeness check failed: {}",
                problems.join(", ")
            )));
        }
    }

    worker
        .scene
        .calculate_origin()
        .ok_or_else(|| ConversionError::export("conversion produced no finite geometry"))?;
    emit_progress(92, "writing");
    if let Some(parent) = cli
        .output
        .parent()
        .filter(|path| !path.as_os_str().is_empty())
    {
        fs::create_dir_all(parent).map_err(|error| {
            ConversionError::export(format!("cannot create output directory: {error}"))
        })?;
    }
    let glb_statistics =
        match crate::glb::write(&cli.output, &worker.scene, cli.limits.output_bytes) {
            Ok(statistics) => statistics,
            Err(crate::glb::GlbWriteError::Resource(error)) => return Err(error.into()),
            Err(crate::glb::GlbWriteError::Export(error)) => {
                return Err(ConversionError::export(format!(
                    "GLB export failed: {error}"
                )));
            }
        };
    worker.stats.triangle_count = glb_statistics.triangle_count;
    worker.stats.edge_segment_count = glb_statistics.line_segment_count;
    if glb_statistics.dropped_source_degenerate_triangles > 0 {
        eprintln!(
            "warning: {} zero-area source triangle(s) were omitted",
            glb_statistics.dropped_source_degenerate_triangles
        );
    }
    if glb_statistics.dropped_precision_collapsed_triangles > 0 {
        eprintln!(
            "warning: {} non-zero source triangle(s) collapsed during f32 GLB mapping and were omitted",
            glb_statistics.dropped_precision_collapsed_triangles
        );
    }

    let output = json!({
        "progress": 100,
        "stage": "complete",
        "stats": worker.stats,
        "glb": {
            "meshCount": glb_statistics.mesh_count,
            "nodeCount": glb_statistics.node_count,
            "triangleCount": glb_statistics.triangle_count,
            "lineSegmentCount": glb_statistics.line_segment_count,
            "uniqueTriangleCount": glb_statistics.unique_triangle_count,
            "uniqueLineSegmentCount": glb_statistics.unique_line_segment_count,
            "droppedDegenerateTriangles": glb_statistics.dropped_degenerate_triangles,
            "droppedSourceDegenerateTriangles": glb_statistics.dropped_source_degenerate_triangles,
            "droppedPrecisionCollapsedTriangles": glb_statistics.dropped_precision_collapsed_triangles
        }
    });
    println!("{output}");
    let _ = io::stdout().flush();
    Ok(())
}

/// Validates paths, format, tessellation settings, and resource limits.
fn validate(cli: &Cli) -> Result<(), ConversionError> {
    if !cli.input.is_file() {
        return Err(ConversionError::invalid(format!(
            "input file does not exist: {}",
            cli.input.display()
        )));
    }
    if cli.input == cli.output {
        return Err(ConversionError::invalid(
            "input and output paths must be different",
        ));
    }
    if cli.output.exists() {
        return Err(ConversionError::invalid(format!(
            "output path already exists: {}",
            cli.output.display()
        )));
    }
    match cli.format.to_ascii_lowercase().as_str() {
        "dwg" | "dxf" | "dws" | "dwt" => {}
        other => {
            return Err(ConversionError::invalid(format!(
                "unsupported input format '{other}'"
            )))
        }
    }
    if !cli.linear_deflection_meters.is_finite() || cli.linear_deflection_meters <= 0.0 {
        return Err(ConversionError::invalid(
            "--linear-deflection-meters must be finite and greater than zero",
        ));
    }
    if !cli.angular_deflection_degrees.is_finite()
        || cli.angular_deflection_degrees <= 0.0
        || cli.angular_deflection_degrees > 180.0
    {
        return Err(ConversionError::invalid(
            "--angular-deflection-degrees must be in (0, 180]",
        ));
    }
    for (option, value) in [
        ("--max-input-bytes", cli.limits.input_bytes),
        ("--max-entities", cli.limits.entities),
        ("--max-vertices", cli.limits.position_elements),
        ("--max-triangles", cli.limits.triangles),
        ("--max-line-segments", cli.limits.line_segments),
        ("--max-output-bytes", cli.limits.output_bytes),
    ] {
        if value == 0 {
            return Err(ConversionError::invalid(format!(
                "{option} must be greater than zero"
            )));
        }
    }
    if cli.limits.output_bytes > u64::from(u32::MAX) {
        return Err(ConversionError::invalid(
            "--max-output-bytes cannot exceed the GLB 32-bit length limit",
        ));
    }
    Ok(())
}

/// Writes a machine-readable progress event to standard output.
fn emit_progress(progress: u8, stage: &str) {
    println!("{}", json!({ "progress": progress, "stage": stage }));
    let _ = io::stdout().flush();
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
enum ColorKey {
    ByLayer,
    None,
    ByBlock,
    Index(u8),
    Rgb(u8, u8, u8),
}

impl From<Color> for ColorKey {
    /// Converts a CAD color into an orderable block-variant cache component.
    fn from(value: Color) -> Self {
        match value {
            Color::ByLayer => Self::ByLayer,
            Color::None => Self::None,
            Color::ByBlock => Self::ByBlock,
            Color::Index(index) => Self::Index(index),
            Color::Rgb { r, g, b } => Self::Rgb(r, g, b),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
struct BlockVariantKey {
    block_name: String,
    layer: String,
    color: ColorKey,
    line_weight: i16,
    transparency: Transparency,
    material_flags: u8,
    material_handle: Option<u64>,
    invisible: bool,
    tessellation_scale_bits: u64,
    remaining_depth: usize,
}

impl BlockVariantKey {
    /// Builds the cache key for geometry whose inherited appearance and
    /// scale-dependent tessellation must be identical before it can be shared.
    fn new(insert: &Insert, tessellation_scale: f64, depth: usize) -> Self {
        Self {
            block_name: insert.block_name.clone(),
            layer: insert.common.layer.clone(),
            color: insert.common.color.into(),
            line_weight: insert.common.line_weight.value(),
            transparency: insert.common.transparency,
            material_flags: insert.common.material_flags,
            material_handle: insert.common.material_handle.map(|handle| handle.value()),
            invisible: insert.common.invisible,
            tessellation_scale_bits: tessellation_scale.to_bits(),
            remaining_depth: MAX_INSERT_DEPTH.saturating_sub(depth),
        }
    }
}

#[derive(Debug, Clone, Copy)]
struct BlockPlacement {
    prototype: usize,
    transform: AffineTransform,
}

#[derive(Debug, Clone)]
struct BlockPrototype {
    name: String,
    meshes: Vec<usize>,
    children: Vec<BlockPlacement>,
}

#[derive(Debug, Default)]
struct GeometryBatches {
    batches: Vec<SceneObject>,
}

impl GeometryBatches {
    /// Appends one entity's geometry to the current bounded batch or starts a new batch.
    fn push(&mut self, object: SceneObject) -> Result<(), ConversionError> {
        if object.is_empty() {
            return Ok(());
        }
        let needs_new_batch = self
            .batches
            .last()
            .map(|batch| !batch_can_accept(batch, &object))
            .unwrap_or(true);
        if needs_new_batch {
            self.batches.try_reserve(1).map_err(|_| {
                ConversionError::resource(
                    "resource limit exceeded: unable to reserve geometry batches",
                )
            })?;
            self.batches.push(SceneObject::default());
        }
        append_scene_object(
            self.batches
                .last_mut()
                .expect("a geometry batch was just created"),
            object,
        )
    }
}

struct Worker<'a> {
    document: &'a CadDocument,
    include_hidden: bool,
    export_edge_wires: bool,
    angular_deflection: f64,
    linear_deflection: f64,
    reference_span: f64,
    budget: ResourceBudget,
    scene: Scene,
    block_variants: BTreeMap<BlockVariantKey, usize>,
    block_prototypes: Vec<BlockPrototype>,
    stats: ConversionStats,
}

impl Worker<'_> {
    /// Visits one unique entity, compiling reusable blocks or appending its
    /// drawable geometry to the caller's bounded batches.
    fn visit(
        &mut self,
        entity: EntityType,
        depth: usize,
        active_blocks: &mut Vec<String>,
        tessellation_scale: f64,
        geometry_batches: &mut GeometryBatches,
        block_placements: &mut Vec<BlockPlacement>,
    ) -> Result<(), ConversionError> {
        self.budget.charge_entity()?;
        self.stats.visited_entities += 1;
        let common = entity.common();
        let layer_visible = self
            .document
            .layers
            .get(&common.layer)
            .map(|layer| layer.is_visible())
            .unwrap_or(true);
        if !self.include_hidden && (common.invisible || !layer_visible) {
            self.stats.skipped_hidden_entities += 1;
            return Ok(());
        }

        if let EntityType::Insert(insert) = entity {
            return self.visit_insert(
                insert,
                depth,
                active_blocks,
                tessellation_scale,
                block_placements,
            );
        }

        if let Some(data) = acis_data(&entity) {
            if let Some(object) = self.extract_acis(&entity, data, tessellation_scale)? {
                geometry_batches.push(object)?;
            }
            return Ok(());
        }

        let entity_type = entity.as_entity().entity_type().to_owned();
        let color = self.resolve_entity_color(entity.common());
        let layer = entity.common().layer.clone();
        match geometry::extract(
            &entity,
            self.angular_deflection,
            scaled_linear_deflection(self.linear_deflection, tessellation_scale)?,
            scaled_reference_span(self.reference_span, tessellation_scale),
            geometry::GeometryCapacity {
                position_elements: self.budget.remaining_position_elements(),
                triangles: self.budget.remaining_triangles(),
                line_segments: self.budget.remaining_line_segments(),
            },
        ) {
            Ok(Some(mut native)) => {
                self.stats.invalid_native_faces += native.invalid_faces;
                self.stats.degenerate_native_faces += native.degenerate_faces;
                if !self.export_edge_wires && !native.triangles.is_empty() {
                    native.line_positions.clear();
                }
                if native.is_empty() {
                    *self
                        .stats
                        .unsupported_entities
                        .entry(entity_type)
                        .or_default() += 1;
                    return Ok(());
                }
                self.budget.charge_geometry(
                    native.position_element_count(),
                    native.triangles.len(),
                    native.line_positions.len() / 2,
                )?;
                let primitive = PrimitiveKey { layer, color };
                let mut object = SceneObject {
                    positions: native.positions,
                    normals: native.normals,
                    ..Default::default()
                };
                if !native.triangles.is_empty() {
                    object
                        .triangle_groups
                        .insert(primitive.clone(), native.triangles);
                }
                if !native.line_positions.is_empty() {
                    object.line_groups.insert(primitive, native.line_positions);
                }
                geometry_batches.push(object)?;
            }
            Ok(None) => {
                *self
                    .stats
                    .unsupported_entities
                    .entry(entity_type)
                    .or_default() += 1;
            }
            Err(error) => return Err(error.into()),
        }
        Ok(())
    }

    /// Records INSERT/MINSERT placements while compiling each reusable block
    /// variant only once for a matching inherited appearance and scale.
    fn visit_insert(
        &mut self,
        insert: Insert,
        depth: usize,
        active_blocks: &mut Vec<String>,
        parent_tessellation_scale: f64,
        placements: &mut Vec<BlockPlacement>,
    ) -> Result<(), ConversionError> {
        if depth >= MAX_INSERT_DEPTH {
            self.stats.cyclic_references += 1;
            return Ok(());
        }
        if active_blocks.iter().any(|name| name == &insert.block_name) {
            self.stats.cyclic_references += 1;
            return Ok(());
        }
        let Some(record) = self.document.block_records.get(&insert.block_name) else {
            self.stats.missing_references += 1;
            self.stats.missing_block_definitions += 1;
            self.record_reference_issue("missing-definition", &insert.block_name);
            return Ok(());
        };
        if !record.explodable {
            // `explodable` controls the interactive AutoCAD EXPLODE command;
            // it does not hide the block reference from rendering.
            self.stats.non_explodable_insert_flags_ignored += 1;
        }
        let source_entity_count = record.entity_handles.len();
        let unresolved_external = source_entity_count == 0
            && (record.flags.is_xref || record.flags.is_external || record.flags.is_xref_unloaded);
        if unresolved_external {
            self.stats.missing_references += 1;
            self.stats.unresolved_external_references += 1;
            self.record_reference_issue("external", &insert.block_name);
            return Ok(());
        }
        if source_entity_count == 0 && !record.xref_path.is_empty() {
            // Some AutoCAD vertical products use an xref-path-looking field
            // on empty metadata blocks without setting any XREF flag. They do
            // not name drawable external geometry and are non-fatal.
            self.stats.missing_references += 1;
            self.stats.empty_path_reference_inserts += 1;
            self.record_reference_issue("empty-path-placeholder", &insert.block_name);
            return Ok(());
        }
        if source_entity_count == 0 {
            // An ordinary empty block is valid. Do not call `Insert::explode`:
            // for MINSERT it would materialize every array transform even
            // though there is no child geometry to render.
            self.stats.expanded_inserts = self.stats.expanded_inserts.saturating_add(
                usize::try_from(insert_instance_count(&insert)?).unwrap_or(usize::MAX),
            );
            return Ok(());
        }

        let instance_count = insert_instance_count(&insert)?;
        self.budget.charge_prototype_placements(instance_count)?;
        let insert_transform = AffineTransform::from_matrix(insert.get_transform().matrix.m);
        let tessellation_scale = combined_tessellation_scale(
            parent_tessellation_scale,
            linear_stretch_bound(insert_transform)?,
        )?;
        let Some(prototype) =
            self.compile_block_prototype(&insert, depth, active_blocks, tessellation_scale)?
        else {
            return Ok(());
        };
        let instance_capacity = usize::try_from(instance_count).map_err(|_| {
            ConversionError::resource(
                "resource limit exceeded: block prototype placement count overflow",
            )
        })?;
        placements.try_reserve(instance_capacity).map_err(|_| {
            ConversionError::resource(
                "resource limit exceeded: unable to reserve block prototype placements",
            )
        })?;

        let mut single = insert.clone();
        single.column_count = 1;
        single.row_count = 1;
        let rows = if insert.is_minsert() {
            insert.row_count
        } else {
            1
        };
        let columns = if insert.is_minsert() {
            insert.column_count
        } else {
            1
        };
        let (sin, cos) = insert.rotation.sin_cos();
        for row in 0..rows {
            for column in 0..columns {
                let x = f64::from(column) * insert.column_spacing;
                let y = f64::from(row) * insert.row_spacing;
                single.insert_point = insert.insert_point
                    + opencadcodec::Vector3::new(x * cos - y * sin, x * sin + y * cos, 0.0);
                placements.push(BlockPlacement {
                    prototype,
                    transform: AffineTransform::from_matrix(single.get_transform().matrix.m),
                });
            }
        }
        Ok(())
    }

    /// Compiles one inherited block variant into shared mesh definitions and
    /// remembers its nested block placements for later scene instantiation.
    fn compile_block_prototype(
        &mut self,
        insert: &Insert,
        depth: usize,
        active_blocks: &mut Vec<String>,
        tessellation_scale: f64,
    ) -> Result<Option<usize>, ConversionError> {
        let key = BlockVariantKey::new(insert, tessellation_scale, depth);
        if let Some(prototype) = self.block_variants.get(&key) {
            return Ok(Some(*prototype));
        }

        let record = self
            .document
            .block_records
            .get(&insert.block_name)
            .expect("the block record was validated before prototype compilation");
        let definition = prepare_insert_definition(self.document, record);
        if definition.missing_handles > 0 {
            self.stats.missing_references += 1;
            self.stats.missing_referenced_entity_handles += definition.missing_handles;
            self.record_reference_issue("missing-child-handle", &insert.block_name);
        }
        if definition.entities.is_empty() {
            if definition.missing_handles == 0 {
                self.stats.missing_references += 1;
            }
            self.stats.empty_insert_expansions += 1;
            self.record_reference_issue("empty-expansion", &insert.block_name);
            return Ok(None);
        }

        let cyclic_references_before = self.stats.cyclic_references;
        active_blocks.push(insert.block_name.clone());
        let compilation = (|| {
            let mut geometry = GeometryBatches::default();
            let mut children = Vec::new();
            for mut entity in definition.entities {
                inherit_block_properties(self.document, entity.common_mut(), &insert.common);
                self.visit(
                    entity,
                    depth + 1,
                    active_blocks,
                    tessellation_scale,
                    &mut geometry,
                    &mut children,
                )?;
            }

            let prototype = self.block_prototypes.len();
            let prefix = format!("Block_{}_variant_{prototype}", insert.block_name);
            let meshes = self.store_batches(geometry, &prefix)?;
            self.block_prototypes.try_reserve(1).map_err(|_| {
                ConversionError::resource(
                    "resource limit exceeded: unable to reserve block prototypes",
                )
            })?;
            self.block_prototypes.push(BlockPrototype {
                name: insert.block_name.clone(),
                meshes,
                children,
            });
            if self.stats.cyclic_references == cyclic_references_before {
                self.block_variants.insert(key, prototype);
            }
            Ok(Some(prototype))
        })();
        active_blocks.pop();
        compilation
    }

    /// Stores bounded geometry batches as unique scene meshes and returns their indices.
    fn store_batches(
        &mut self,
        batches: GeometryBatches,
        name_prefix: &str,
    ) -> Result<Vec<usize>, ConversionError> {
        let count = batches.batches.len();
        self.scene.objects.try_reserve(count).map_err(|_| {
            ConversionError::resource("resource limit exceeded: unable to reserve scene meshes")
        })?;
        let mut meshes = Vec::new();
        meshes.try_reserve(count).map_err(|_| {
            ConversionError::resource(
                "resource limit exceeded: unable to reserve prototype mesh indices",
            )
        })?;
        for (batch_index, mut object) in batches.batches.into_iter().enumerate() {
            object.name = format!("{name_prefix}_batch_{batch_index}");
            meshes.push(self.scene.objects.len());
            self.scene.objects.push(object);
        }
        Ok(meshes)
    }

    /// Finalizes model-space batches and expands the compact prototype graph
    /// into transformed GLB nodes that share the stored meshes.
    fn finish_model_scene(
        &mut self,
        model_geometry: GeometryBatches,
        model_blocks: Vec<BlockPlacement>,
    ) -> Result<(), ConversionError> {
        let model_meshes = self.store_batches(model_geometry, "ModelSpace")?;
        for mesh in model_meshes {
            self.push_scene_node(mesh, AffineTransform::translation([0.0; 3]))?;
        }
        for placement in model_blocks {
            self.instantiate_prototype(placement.prototype, placement.transform, 0)?;
        }
        Ok(())
    }

    /// Recursively emits one logical block occurrence while composing nested transforms.
    fn instantiate_prototype(
        &mut self,
        prototype_index: usize,
        transform: AffineTransform,
        depth: usize,
    ) -> Result<(), ConversionError> {
        if depth > MAX_INSERT_DEPTH {
            return Err(ConversionError::export(
                "compiled block hierarchy exceeds the supported nesting depth",
            ));
        }
        self.budget.charge_logical_block_instances(1)?;
        self.stats.expanded_inserts += 1;
        let prototype = self
            .block_prototypes
            .get(prototype_index)
            .ok_or_else(|| ConversionError::export("invalid compiled block prototype index"))?;
        let name = prototype.name.clone();
        let meshes = prototype.meshes.clone();
        let children = prototype.children.clone();
        for mesh in meshes {
            self.push_scene_node_named(mesh, transform, &name)?;
        }
        for child in children {
            self.instantiate_prototype(
                child.prototype,
                transform.compose(child.transform),
                depth + 1,
            )?;
        }
        Ok(())
    }

    /// Emits one scene node using the referenced mesh's stable descriptive name.
    fn push_scene_node(
        &mut self,
        mesh: usize,
        transform: AffineTransform,
    ) -> Result<(), ConversionError> {
        let name = self
            .scene
            .objects
            .get(mesh)
            .ok_or_else(|| ConversionError::export("invalid scene mesh index"))?
            .name
            .clone();
        self.push_scene_node_named(mesh, transform, &name)
    }

    /// Emits one transformed node that references a previously stored shared mesh.
    fn push_scene_node_named(
        &mut self,
        mesh: usize,
        transform: AffineTransform,
        name: &str,
    ) -> Result<(), ConversionError> {
        if self.scene.objects.get(mesh).is_none() {
            return Err(ConversionError::export("invalid scene mesh index"));
        }
        if !transform.is_finite_affine() {
            return Err(ConversionError::export(
                "block reference contains a non-finite or non-affine transform",
            ));
        }
        self.budget.charge_scene_nodes(1)?;
        self.scene.nodes.try_reserve(1).map_err(|_| {
            ConversionError::resource("resource limit exceeded: unable to reserve scene nodes")
        })?;
        self.scene.nodes.push(SceneNode {
            name: name.to_owned(),
            mesh,
            transform,
        });
        Ok(())
    }

    /// Parses, lifts, and tessellates one ACIS entity into reusable local geometry.
    fn extract_acis(
        &mut self,
        entity: &EntityType,
        data: &AcisData,
        tessellation_scale: f64,
    ) -> Result<Option<SceneObject>, ConversionError> {
        self.stats.acis_entities += 1;
        let Some(sat) = data.parse() else {
            self.stats.acis_parse_failures += 1;
            return Ok(None);
        };
        self.stats.acis_payloads_parsed += 1;
        let (bodies, loss) = opencadkernel::acis::lift(&sat);
        self.stats.acis_lost_surfaces += loss.surfaces.len();
        self.stats.acis_lost_curves += loss.curves.len();
        self.stats.acis_broken_records += loss.broken.len();
        self.stats.acis_bodies_lifted += bodies.len();
        if bodies.is_empty() {
            self.stats.acis_entities_without_bodies += 1;
            return Ok(None);
        }

        let base_color = self.resolve_entity_color(entity.common());
        let face_colors = self.acis_face_colors(&sat, data, base_color);
        let source_fit = if sat.header.normal_tolerance.is_finite()
            && sat.header.normal_tolerance > 0.0
            && sat.header.normal_tolerance < 1.0e-2
        {
            sat.header.normal_tolerance
        } else {
            DEFAULT_SOURCE_FIT
        };
        let tolerance = TessellationTolerance::new(self.angular_deflection, source_fit)
            .with_chordal_deflection(scaled_linear_deflection(
                self.linear_deflection,
                tessellation_scale,
            )?);
        let layer = entity.common().layer.clone();
        let line_key = PrimitiveKey {
            layer: layer.clone(),
            color: base_color,
        };
        let mut object = SceneObject::default();

        for body in bodies {
            self.stats.acis_faces += body.face_keys().count();
            let tessellation = mesh::tessellate(&body, tolerance);
            self.stats.missing_faces += tessellation.missing_faces.len();
            let edge_segments = if self.export_edge_wires {
                tessellation
                    .drawing_edges
                    .iter()
                    .try_fold(0_usize, |total, edge| {
                        total.checked_add(edge.positions.len().saturating_sub(1))
                    })
                    .ok_or_else(|| {
                        ConversionError::resource(
                            "resource limit exceeded: ACIS edge segment count overflow",
                        )
                    })?
            } else {
                0
            };
            let line_position_elements = edge_segments.checked_mul(2).ok_or_else(|| {
                ConversionError::resource(
                    "resource limit exceeded: ACIS line POSITION count overflow",
                )
            })?;
            let position_elements = tessellation
                .mesh
                .positions
                .len()
                .checked_add(line_position_elements)
                .ok_or_else(|| {
                    ConversionError::resource(
                        "resource limit exceeded: ACIS POSITION count overflow",
                    )
                })?;
            self.budget.charge_geometry(
                position_elements,
                tessellation.mesh.triangles.len(),
                edge_segments,
            )?;
            let base = u32::try_from(object.positions.len()).map_err(|_| {
                ConversionError::export("one ACIS entity exceeds the GLB 32-bit index limit")
            })?;
            let added = u32::try_from(tessellation.mesh.positions.len()).map_err(|_| {
                ConversionError::export("one ACIS body exceeds the GLB 32-bit index limit")
            })?;
            base.checked_add(added).ok_or_else(|| {
                ConversionError::export("one ACIS entity exceeds the GLB 32-bit index limit")
            })?;
            object
                .positions
                .try_reserve(tessellation.mesh.positions.len())
                .map_err(|_| {
                    ConversionError::resource(
                        "resource limit exceeded: unable to reserve ACIS positions",
                    )
                })?;
            object
                .normals
                .try_reserve(tessellation.mesh.normals.len())
                .map_err(|_| {
                    ConversionError::resource(
                        "resource limit exceeded: unable to reserve ACIS normals",
                    )
                })?;
            object
                .line_groups
                .entry(line_key.clone())
                .or_default()
                .try_reserve(line_position_elements)
                .map_err(|_| {
                    ConversionError::resource(
                        "resource limit exceeded: unable to reserve ACIS edge positions",
                    )
                })?;
            object
                .positions
                .extend_from_slice(&tessellation.mesh.positions);
            object.normals.extend_from_slice(&tessellation.mesh.normals);
            for (index, triangle) in tessellation.mesh.triangles.iter().enumerate() {
                let face_color = tessellation
                    .triangle_faces
                    .get(index)
                    .and_then(|face_key| body.faces.get(*face_key))
                    .and_then(|face| face.provenance.source())
                    .and_then(|source| face_colors.get(&(source.index() as i32)).copied())
                    .unwrap_or(base_color);
                let mut mapped = [0_u32; 3];
                for (target, source) in mapped.iter_mut().zip(triangle) {
                    if *source >= tessellation.mesh.positions.len() {
                        return Err(ConversionError::export(
                            "ACIS tessellation returned an out-of-range triangle index",
                        ));
                    }
                    let local = u32::try_from(*source).map_err(|_| {
                        ConversionError::export("one ACIS body exceeds the GLB 32-bit index limit")
                    })?;
                    *target = base.checked_add(local).ok_or_else(|| {
                        ConversionError::export(
                            "one ACIS entity exceeds the GLB 32-bit index limit",
                        )
                    })?;
                }
                let group = object
                    .triangle_groups
                    .entry(PrimitiveKey {
                        layer: layer.clone(),
                        color: face_color,
                    })
                    .or_default();
                group.try_reserve(1).map_err(|_| {
                    ConversionError::resource(
                        "resource limit exceeded: unable to reserve ACIS triangles",
                    )
                })?;
                group.push(mapped);
            }
            if self.export_edge_wires {
                let lines = object.line_groups.entry(line_key.clone()).or_default();
                for edge in tessellation.drawing_edges {
                    for segment in edge.positions.windows(2) {
                        lines.extend_from_slice(segment);
                    }
                }
            }
        }

        if object.is_empty() {
            self.stats.acis_entities_without_bodies += 1;
            Ok(None)
        } else {
            Ok(Some(object))
        }
    }

    /// Resolves an entity's effective RGBA color from entity, layer, and material data.
    fn resolve_entity_color(&self, common: &EntityCommon) -> Rgba8 {
        let layer = self.document.layers.get(&common.layer);
        let source = match common.color {
            Color::ByLayer | Color::ByBlock | Color::None => {
                layer.map(|value| value.color).unwrap_or(Color::WHITE)
            }
            explicit => explicit,
        };
        let (red, green, blue) = source.rgb().unwrap_or((255, 255, 255));
        let transparency = match common.transparency {
            Transparency::Explicit(value) => value,
            Transparency::ByLayer | Transparency::ByBlock => layer
                .and_then(|value| value.transparency.explicit_alpha())
                .unwrap_or(0),
        };
        let base = Rgba8 {
            red,
            green,
            blue,
            alpha: 255_u8.saturating_sub(transparency),
        };
        let material_handle = if common.material_flags == 0b11 {
            common.material_handle
        } else if common.material_flags == 0 {
            layer
                .map(|value| value.material)
                .filter(|handle| !handle.is_null())
        } else {
            None
        };
        material_handle
            .and_then(|handle| self.material_color(handle, base))
            .unwrap_or(base)
    }

    /// Applies a referenced material's diffuse color and opacity to a base color.
    fn material_color(&self, handle: Handle, current: Rgba8) -> Option<Rgba8> {
        let ObjectType::Material(material) = self.document.objects.get(&handle)? else {
            return None;
        };
        let mut red = current.red as f64;
        let mut green = current.green as f64;
        let mut blue = current.blue as f64;
        if material.diffuse_color.flag == 1 {
            if let Some(packed) = material.diffuse_color.rgb {
                let packed = packed as u32;
                red = ((packed >> 16) & 0xff) as f64;
                green = ((packed >> 8) & 0xff) as f64;
                blue = (packed & 0xff) as f64;
            }
        }
        let factor = material.diffuse_color.factor.clamp(0.0, 1.0);
        Some(Rgba8 {
            red: (red * factor).round().clamp(0.0, 255.0) as u8,
            green: (green * factor).round().clamp(0.0, 255.0) as u8,
            blue: (blue * factor).round().clamp(0.0, 255.0) as u8,
            alpha: (current.alpha as f64 * material.opacity_percent.clamp(0.0, 1.0)).round() as u8,
        })
    }

    /// Maps ACIS face record identifiers to their effective material or explicit colors.
    fn acis_face_colors(
        &self,
        sat: &opencadcodec::SatDocument,
        data: &AcisData,
        base: Rgba8,
    ) -> HashMap<i32, Rgba8> {
        let mut colors = HashMap::new();
        for record in sat
            .records
            .iter()
            .filter(|record| record.entity_type == "material-adesk-attrib")
        {
            let Some(owner) = record.token_pointer(2).map(|pointer| pointer.0) else {
                continue;
            };
            let Some(reference) = record.token(3).and_then(|token| token.as_integer()) else {
                continue;
            };
            let handle = data
                .materials
                .iter()
                .find(|material| {
                    material.array_index == reference as i32
                        || material.absolute_reference == reference as i32
                })
                .and_then(|material| material.material_handle)
                .or_else(|| (reference > 0).then(|| Handle::new(reference as u64)));
            if let Some(color) = handle.and_then(|handle| self.material_color(handle, base)) {
                colors.insert(owner, color);
            }
        }
        for record in sat
            .records
            .iter()
            .filter(|record| record.entity_type == "color-adesk-attrib")
        {
            let Some(owner) = record.token_pointer(2).map(|pointer| pointer.0) else {
                continue;
            };
            let Some(value) = record.token(3).and_then(|token| token.as_integer()) else {
                continue;
            };
            let source = if (1..=255).contains(&value) {
                Some(Color::from_index(value as i16))
            } else if value > 257 {
                Some(Color::from_true_color_value(value as i32))
            } else {
                None
            };
            if let Some((red, green, blue)) = source.and_then(|value| value.rgb()) {
                colors.insert(
                    owner,
                    Rgba8 {
                        red,
                        green,
                        blue,
                        alpha: base.alpha,
                    },
                );
            }
        }
        colors
    }

    /// Records a categorized block-reference issue in the conversion statistics.
    fn record_reference_issue(&mut self, reason: &str, block_name: &str) {
        *self
            .stats
            .reference_issues
            .entry(format!("{reason}:{block_name}"))
            .or_default() += 1;
    }

    /// Collects geometry-loss conditions that cause strict conversion to fail.
    fn completeness_problems(&self) -> Vec<String> {
        let mut problems = Vec::new();
        push_problem(
            &mut problems,
            self.stats.missing_block_definitions,
            "missing block definitions",
        );
        push_problem(
            &mut problems,
            self.stats.unresolved_external_references,
            "unresolved external references",
        );
        push_problem(
            &mut problems,
            self.stats.empty_insert_expansions,
            "empty insert expansions",
        );
        push_problem(
            &mut problems,
            self.stats.missing_referenced_entity_handles,
            "missing block child handles",
        );
        push_problem(
            &mut problems,
            self.stats.cyclic_references,
            "cyclic/deep references",
        );
        push_problem(
            &mut problems,
            self.stats.acis_parse_failures,
            "unparsed ACIS payloads",
        );
        push_problem(
            &mut problems,
            self.stats.acis_entities_without_bodies,
            "ACIS entities without renderable bodies",
        );
        push_problem(
            &mut problems,
            self.stats.acis_lost_surfaces,
            "unsupported ACIS surfaces",
        );
        push_problem(
            &mut problems,
            self.stats.acis_lost_curves,
            "unsupported ACIS curves",
        );
        push_problem(
            &mut problems,
            self.stats.acis_broken_records,
            "broken ACIS records",
        );
        push_problem(
            &mut problems,
            self.stats.missing_faces,
            "untessellated B-rep faces",
        );
        push_problem(
            &mut problems,
            self.stats.invalid_native_faces,
            "malformed native mesh faces",
        );
        problems
    }
}

/// Calculates the number of transform placements represented by an INSERT or MINSERT.
fn insert_instance_count(insert: &Insert) -> Result<u64, ResourceLimitError> {
    let instances = if insert.is_minsert() {
        u64::from(insert.row_count)
            .checked_mul(u64::from(insert.column_count))
            .ok_or_else(|| {
                ResourceLimitError::new("block prototype placements", u64::MAX, u64::MAX - 1)
            })?
    } else {
        1
    };
    Ok(instances)
}

/// Returns the number of POSITION elements held by a geometry batch.
fn batch_position_element_count(object: &SceneObject) -> Option<usize> {
    object
        .line_groups
        .values()
        .try_fold(object.positions.len(), |total, lines| {
            total.checked_add(lines.len())
        })
}

/// Reports whether appending an object keeps the current mesh batch within its
/// target sizes. A single larger entity remains intact and is bounded by the
/// converter's global geometry and output limits.
fn batch_can_accept(batch: &SceneObject, object: &SceneObject) -> bool {
    let Some(batch_positions) = batch_position_element_count(batch) else {
        return false;
    };
    let Some(object_positions) = batch_position_element_count(object) else {
        return false;
    };
    batch_positions
        .checked_add(object_positions)
        .is_some_and(|count| count <= TARGET_BATCH_POSITION_ELEMENTS)
        && batch
            .triangle_count()
            .checked_add(object.triangle_count())
            .is_some_and(|count| count <= TARGET_BATCH_TRIANGLES)
        && batch
            .line_segment_count()
            .checked_add(object.line_segment_count())
            .is_some_and(|count| count <= TARGET_BATCH_LINE_SEGMENTS)
}

/// Merges one entity mesh into a batch while rebasing triangle indices and
/// preserving layer/material primitive groups.
fn append_scene_object(
    target: &mut SceneObject,
    source: SceneObject,
) -> Result<(), ConversionError> {
    let SceneObject {
        name: _,
        positions,
        normals,
        mut triangle_groups,
        line_groups,
    } = source;
    let position_base = u32::try_from(target.positions.len()).map_err(|_| {
        ConversionError::export("one geometry batch exceeds the GLB 32-bit index limit")
    })?;
    let added_positions = u32::try_from(positions.len()).map_err(|_| {
        ConversionError::export("one geometry object exceeds the GLB 32-bit index limit")
    })?;
    position_base.checked_add(added_positions).ok_or_else(|| {
        ConversionError::export("one geometry batch exceeds the GLB 32-bit index limit")
    })?;
    for triangles in triangle_groups.values_mut() {
        for triangle in triangles {
            for index in triangle {
                *index = index.checked_add(position_base).ok_or_else(|| {
                    ConversionError::export("one geometry batch exceeds the GLB 32-bit index limit")
                })?;
            }
        }
    }

    let target_positions_before = target.positions.len();
    let normals_are_complete =
        target.normals.len() == target_positions_before && normals.len() == positions.len();
    target.positions.try_reserve(positions.len()).map_err(|_| {
        ConversionError::resource("resource limit exceeded: unable to reserve batched positions")
    })?;
    if normals_are_complete {
        target.normals.try_reserve(normals.len()).map_err(|_| {
            ConversionError::resource("resource limit exceeded: unable to reserve batched normals")
        })?;
    } else {
        target.normals.clear();
    }
    target.positions.extend(positions);
    if normals_are_complete {
        target.normals.extend(normals);
    }

    for (key, triangles) in triangle_groups {
        let group = target.triangle_groups.entry(key).or_default();
        group.try_reserve(triangles.len()).map_err(|_| {
            ConversionError::resource(
                "resource limit exceeded: unable to reserve batched triangles",
            )
        })?;
        group.extend(triangles);
    }
    for (key, lines) in line_groups {
        let group = target.line_groups.entry(key).or_default();
        group.try_reserve(lines.len()).map_err(|_| {
            ConversionError::resource(
                "resource limit exceeded: unable to reserve batched line positions",
            )
        })?;
        group.extend(lines);
    }
    Ok(())
}

/// Computes the maximum source-space linear stretch using the largest
/// singular value of the transform's upper-left 3x3 matrix.
fn linear_stretch_bound(transform: AffineTransform) -> Result<f64, ConversionError> {
    if !transform.is_finite_affine() {
        return Err(ConversionError::export(
            "block reference contains a non-finite or non-affine transform",
        ));
    }
    let mut gram = [[0.0_f64; 3]; 3];
    for (row, target_row) in gram.iter_mut().enumerate() {
        for (column, target) in target_row.iter_mut().enumerate() {
            *target = (0..3)
                .map(|inner| transform.matrix[inner][row] * transform.matrix[inner][column])
                .sum();
        }
    }
    let eigenvalue = largest_symmetric_eigenvalue(gram).max(0.0);
    let stretch = eigenvalue.sqrt();
    if !stretch.is_finite() || stretch <= 0.0 {
        return Err(ConversionError::export(
            "block reference has an invalid zero or non-finite scale",
        ));
    }
    quantize_tessellation_scale(stretch)
}

/// Finds the largest eigenvalue of a finite real symmetric 3x3 matrix.
fn largest_symmetric_eigenvalue(matrix: [[f64; 3]; 3]) -> f64 {
    let off_diagonal =
        matrix[0][1] * matrix[0][1] + matrix[0][2] * matrix[0][2] + matrix[1][2] * matrix[1][2];
    if off_diagonal <= f64::EPSILON * f64::EPSILON {
        return matrix[0][0].max(matrix[1][1]).max(matrix[2][2]);
    }
    let mean = (matrix[0][0] + matrix[1][1] + matrix[2][2]) / 3.0;
    let spread = (matrix[0][0] - mean).powi(2)
        + (matrix[1][1] - mean).powi(2)
        + (matrix[2][2] - mean).powi(2)
        + 2.0 * off_diagonal;
    let scale = (spread / 6.0).sqrt();
    if scale <= f64::MIN_POSITIVE {
        return mean;
    }
    let normalized = [
        [
            (matrix[0][0] - mean) / scale,
            matrix[0][1] / scale,
            matrix[0][2] / scale,
        ],
        [
            matrix[1][0] / scale,
            (matrix[1][1] - mean) / scale,
            matrix[1][2] / scale,
        ],
        [
            matrix[2][0] / scale,
            matrix[2][1] / scale,
            (matrix[2][2] - mean) / scale,
        ],
    ];
    let determinant = normalized[0][0]
        * (normalized[1][1] * normalized[2][2] - normalized[1][2] * normalized[2][1])
        - normalized[0][1]
            * (normalized[1][0] * normalized[2][2] - normalized[1][2] * normalized[2][0])
        + normalized[0][2]
            * (normalized[1][0] * normalized[2][1] - normalized[1][1] * normalized[2][0]);
    let angle = (determinant / 2.0).clamp(-1.0, 1.0).acos() / 3.0;
    let first = mean + 2.0 * scale * angle.cos();
    let third = mean + 2.0 * scale * (angle + (2.0 * std::f64::consts::PI / 3.0)).cos();
    let second = 3.0 * mean - first - third;
    first.max(second).max(third)
}

/// Rounds a positive scale upward into a fine logarithmic bucket so rotations
/// with insignificant floating-point noise still share one block prototype.
fn quantize_tessellation_scale(scale: f64) -> Result<f64, ConversionError> {
    const BUCKETS_PER_OCTAVE: f64 = 256.0;
    let bucket_position = scale.log2() * BUCKETS_PER_OCTAVE;
    if !bucket_position.is_finite() {
        return Err(ConversionError::export(
            "block reference scale is outside the supported numeric range",
        ));
    }
    let nearest = bucket_position.round();
    let bucket = if (bucket_position - nearest).abs() <= 1.0e-9 {
        nearest
    } else {
        bucket_position.ceil()
    };
    let quantized = 2.0_f64.powf(bucket / BUCKETS_PER_OCTAVE);
    if !quantized.is_finite() || quantized <= 0.0 {
        return Err(ConversionError::export(
            "block reference scale is outside the supported numeric range",
        ));
    }
    Ok(quantized)
}

/// Multiplies and buckets parent and child stretch factors for world-space tessellation.
fn combined_tessellation_scale(parent: f64, child: f64) -> Result<f64, ConversionError> {
    let combined = parent * child;
    if !combined.is_finite() || combined <= 0.0 {
        return Err(ConversionError::export(
            "nested block reference scale is outside the supported numeric range",
        ));
    }
    quantize_tessellation_scale(combined)
}

/// Converts a world-space chord tolerance into local block-definition units.
fn scaled_linear_deflection(base: f64, tessellation_scale: f64) -> Result<f64, ConversionError> {
    let scaled = base / tessellation_scale;
    if !scaled.is_finite() || scaled <= 0.0 {
        return Err(ConversionError::export(
            "scaled tessellation tolerance is outside the supported numeric range",
        ));
    }
    Ok(scaled)
}

/// Converts the drawing reference span into local block-definition units.
fn scaled_reference_span(base: f64, tessellation_scale: f64) -> f64 {
    (base / tessellation_scale).max(f64::MIN_POSITIVE)
}

struct InsertDefinition {
    entities: Vec<EntityType>,
    missing_handles: usize,
}

/// Prepare block contents relative to their base point. The caller expands
/// MINSERT cells lazily so large entity payloads are never cloned for the
/// complete array at once. Missing definition members are counted explicitly
/// instead of being silently discarded by `filter_map`.
fn prepare_insert_definition(document: &CadDocument, record: &BlockRecord) -> InsertDefinition {
    if record.entity_handles.is_empty() {
        return InsertDefinition {
            entities: Vec::new(),
            missing_handles: 0,
        };
    }
    let base_point = document
        .get_entity(record.block_entity_handle)
        .and_then(|entity| match entity {
            EntityType::Block(block) => Some(block.base_point),
            _ => None,
        })
        .unwrap_or(record.base_point);
    let to_base = Transform::from_translation(base_point * -1.0);
    let mut definition_entities = Vec::with_capacity(record.entity_handles.len());
    let mut missing_handles = 0;
    for handle in &record.entity_handles {
        let Some(entity) = document.get_entity(*handle) else {
            missing_handles += 1;
            continue;
        };
        let mut entity = entity.clone();
        entity.as_entity_mut().apply_transform(&to_base);
        definition_entities.push(entity);
    }
    InsertDefinition {
        entities: definition_entities,
        missing_handles,
    }
}

/// Returns the ACIS payload carried by a supported solid or surface entity.
fn acis_data(entity: &EntityType) -> Option<&AcisData> {
    match entity {
        EntityType::Solid3D(value) => Some(&value.acis_data),
        EntityType::Region(value) => Some(&value.acis_data),
        EntityType::Body(value) => Some(&value.acis_data),
        EntityType::Surface(value) => Some(&value.acis_data),
        _ => None,
    }
}

/// Applies layer-zero and fully resolved ByBlock inheritance from an INSERT to a child entity.
fn inherit_block_properties(
    document: &CadDocument,
    common: &mut EntityCommon,
    insert: &EntityCommon,
) {
    let insert_layer = document.layers.get(&insert.layer);
    if common.layer == "0" {
        common.layer = insert.layer.clone();
        common.layer_handle = insert.layer_handle;
    }
    if common.color == Color::ByBlock {
        common.color = match insert.color {
            Color::ByLayer | Color::ByBlock | Color::None => insert_layer
                .map(|layer| layer.color)
                .unwrap_or(Color::WHITE),
            explicit => explicit,
        };
        common.color_name = insert.color_name.clone();
        common.color_book_handle = insert.color_book_handle;
    }
    if common.line_weight == LineWeight::ByBlock {
        common.line_weight = match insert.line_weight {
            LineWeight::ByLayer | LineWeight::ByBlock => insert_layer
                .map(|layer| layer.line_weight)
                .unwrap_or(LineWeight::Default),
            explicit => explicit,
        };
    }
    if common.transparency.is_by_block() {
        common.transparency = match insert.transparency {
            Transparency::ByLayer | Transparency::ByBlock => insert_layer
                .map(|layer| layer.transparency)
                .unwrap_or(Transparency::OPAQUE),
            explicit => explicit,
        };
    }
    if common.material_flags == 0b01 {
        common.material_flags = 0b11;
        common.material_handle = if insert.material_flags == 0b11 {
            insert.material_handle
        } else {
            insert_layer
                .map(|layer| layer.material)
                .filter(|handle| !handle.is_null())
        };
    }
    common.invisible |= insert.invisible;
}

/// Appends a counted completeness problem when its count is nonzero.
fn push_problem(problems: &mut Vec<String>, count: usize, label: &str) {
    if count > 0 {
        problems.push(format!("{count} {label}"));
    }
}

/// Converts an AutoCAD INSUNITS code into metres per drawing unit.
fn meters_per_drawing_unit(code: i16) -> Option<f64> {
    match code {
        1 => Some(0.0254),
        2 => Some(0.3048),
        3 => Some(1_609.344),
        4 => Some(0.001),
        5 => Some(0.01),
        6 => Some(1.0),
        7 => Some(1_000.0),
        8 => Some(2.54e-8),
        9 => Some(2.54e-5),
        10 => Some(0.9144),
        11 => Some(1.0e-10),
        12 => Some(1.0e-9),
        13 => Some(1.0e-6),
        14 => Some(0.1),
        15 => Some(10.0),
        16 => Some(100.0),
        17 => Some(1.0e9),
        18 => Some(149_597_870_700.0),
        19 => Some(9.460_730_472_580_8e15),
        20 => Some(3.085_677_581_491_367e16),
        21 => Some(1_200.0 / 3_937.0),
        22 => Some(100.0 / 3_937.0),
        23 => Some(3_600.0 / 3_937.0),
        24 => Some(6_336_000.0 / 3_937.0),
        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use opencadcodec::{Line, Vector3};

    #[test]
    /// Verifies that metre-based tolerances are converted correctly for millimetres.
    fn millimetres_convert_from_metre_tolerance() {
        let scale = meters_per_drawing_unit(4).unwrap();
        assert!((0.0005 / scale - 0.5).abs() < f64::EPSILON);
    }

    #[test]
    /// Verifies the exact metre ratios used for US survey units.
    fn us_survey_units_are_exact_ratios() {
        assert_eq!(meters_per_drawing_unit(21), Some(1_200.0 / 3_937.0));
        assert_eq!(meters_per_drawing_unit(24), Some(6_336_000.0 / 3_937.0));
    }

    #[test]
    /// Verifies non-explodable blocks render while missing child handles are counted.
    fn expands_non_explodable_blocks_and_counts_partial_missing_handles() {
        let mut document = CadDocument::new();
        let line = Line::from_points(Vector3::ZERO, Vector3::new(2.0, 0.0, 0.0));
        let line_handle = document.add_entity(EntityType::Line(line)).unwrap();
        let missing_handle = Handle::new(line_handle.value() + 10_000);
        let mut record = BlockRecord::new("LOCKED");
        record.handle = document.allocate_handle();
        record.explodable = false;
        record.entity_handles = vec![line_handle, missing_handle];
        document.block_records.add(record).unwrap();
        let insert = Insert::new("LOCKED", Vector3::new(10.0, 20.0, 0.0));

        let definition =
            prepare_insert_definition(&document, document.block_records.get("LOCKED").unwrap());
        let expansion = insert.explode(&definition.entities);

        assert_eq!(definition.missing_handles, 1);
        assert_eq!(expansion.len(), 1);
        let EntityType::Line(line) = &expansion[0] else {
            panic!("expected the surviving block child to be rendered");
        };
        assert_eq!(line.start, Vector3::new(10.0, 20.0, 0.0));
        assert_eq!(line.end, Vector3::new(12.0, 20.0, 0.0));
    }

    #[test]
    /// Verifies oversized MINSERT placement counts are rejected before allocation.
    fn minsert_fanout_is_rejected_before_placement_allocation() {
        let insert = Insert::new("ARRAY", Vector3::ZERO).with_array(500, 500, 1.0, 1.0);
        let placements = insert_instance_count(&insert).unwrap();
        assert_eq!(placements, 250_000);

        let mut budget = ResourceBudget::new(crate::limits::ResourceLimits {
            entities: 249_999,
            ..crate::limits::ResourceLimits::default()
        });
        let error = budget.charge_prototype_placements(placements).unwrap_err();

        assert!(error.to_string().contains("block prototype placements"));
    }

    #[test]
    /// Verifies empty MINSERT blocks avoid materializing their full array.
    fn empty_block_does_not_materialize_a_huge_minsert_array() {
        let mut document = CadDocument::new();
        let mut record = BlockRecord::new("EMPTY");
        record.handle = document.allocate_handle();
        document.block_records.add(record).unwrap();
        let insert = Insert::new("EMPTY", Vector3::ZERO).with_array(u16::MAX, u16::MAX, 1.0, 1.0);

        let definition =
            prepare_insert_definition(&document, document.block_records.get("EMPTY").unwrap());

        assert!(definition.entities.is_empty());
        assert_eq!(definition.missing_handles, 0);
        assert_eq!(insert.instance_count(), usize::from(u16::MAX).pow(2));
    }

    #[test]
    /// Verifies strict completeness reports partial blocks and malformed faces.
    fn strict_completeness_includes_partial_blocks_and_malformed_native_faces() {
        let document = CadDocument::new();
        let mut worker = Worker {
            document: &document,
            include_hidden: true,
            export_edge_wires: true,
            angular_deflection: 0.1,
            linear_deflection: 0.1,
            reference_span: 1.0,
            budget: ResourceBudget::new(crate::limits::ResourceLimits::default()),
            scene: Scene::default(),
            block_variants: BTreeMap::new(),
            block_prototypes: Vec::new(),
            stats: ConversionStats {
                missing_referenced_entity_handles: 2,
                invalid_native_faces: 3,
                ..Default::default()
            },
        };

        let problems = worker.completeness_problems();

        assert!(problems.contains(&"2 missing block child handles".to_owned()));
        assert!(problems.contains(&"3 malformed native mesh faces".to_owned()));
        worker.stats.degenerate_native_faces = 4;
        assert_eq!(worker.completeness_problems(), problems);
    }

    #[test]
    /// Verifies block children inherit layer, color, transparency, and material values.
    fn block_properties_inherit_layer_color_transparency_and_material() {
        let document = CadDocument::new();
        let mut child = EntityCommon {
            layer: "0".to_owned(),
            color: Color::ByBlock,
            line_weight: LineWeight::ByBlock,
            transparency: Transparency::ByBlock,
            material_flags: 0b01,
            invisible: false,
            ..Default::default()
        };
        let material = Handle::new(55);
        let parent = EntityCommon {
            layer: "A-WALL".to_owned(),
            layer_handle: Some(Handle::new(44)),
            color: Color::RED,
            line_weight: LineWeight::W0_50,
            transparency: Transparency::Explicit(64),
            material_flags: 0b11,
            material_handle: Some(material),
            invisible: true,
            ..Default::default()
        };

        inherit_block_properties(&document, &mut child, &parent);

        assert_eq!(child.layer, "A-WALL");
        assert_eq!(child.layer_handle, parent.layer_handle);
        assert_eq!(child.color, Color::RED);
        assert_eq!(child.line_weight, LineWeight::W0_50);
        assert_eq!(child.transparency, Transparency::Explicit(64));
        assert_eq!(child.material_flags, 0b11);
        assert_eq!(child.material_handle, Some(material));
        assert!(child.invisible);
    }

    #[test]
    /// Verifies ByBlock properties resolve against the INSERT layer before reaching a child layer.
    fn byblock_properties_resolve_from_insert_layer() {
        let mut document = CadDocument::new();
        let material = Handle::new(55);
        let mut red_layer = opencadcodec::tables::Layer::new("RED");
        red_layer.color = Color::RED;
        red_layer.line_weight = LineWeight::W0_50;
        red_layer.transparency = Transparency::Explicit(64);
        red_layer.material = material;
        document.layers.add(red_layer).unwrap();
        let mut blue_layer = opencadcodec::tables::Layer::new("BLUE");
        blue_layer.color = Color::BLUE;
        document.layers.add(blue_layer).unwrap();
        let mut child = EntityCommon {
            layer: "BLUE".to_owned(),
            color: Color::ByBlock,
            line_weight: LineWeight::ByBlock,
            transparency: Transparency::ByBlock,
            material_flags: 0b01,
            ..Default::default()
        };
        let insert = EntityCommon {
            layer: "RED".to_owned(),
            color: Color::ByLayer,
            line_weight: LineWeight::ByLayer,
            transparency: Transparency::ByLayer,
            material_flags: 0,
            ..Default::default()
        };

        inherit_block_properties(&document, &mut child, &insert);

        assert_eq!(child.layer, "BLUE");
        assert_eq!(child.color, Color::RED);
        assert_eq!(child.line_weight, LineWeight::W0_50);
        assert_eq!(child.transparency, Transparency::Explicit(64));
        assert_eq!(child.material_flags, 0b11);
        assert_eq!(child.material_handle, Some(material));
    }

    #[test]
    /// Verifies batching offsets triangle indices while retaining primitive groups.
    fn batches_geometry_with_rebased_indices_and_shared_primitive_groups() {
        let key = PrimitiveKey {
            layer: "A-WALL".to_owned(),
            color: Rgba8::WHITE,
        };
        let mut first = SceneObject {
            positions: vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0], [0.0, 1.0, 0.0]],
            normals: vec![[0.0, 0.0, 1.0]; 3],
            ..Default::default()
        };
        first.triangle_groups.insert(key.clone(), vec![[0, 1, 2]]);
        let mut second = SceneObject {
            positions: vec![[2.0, 0.0, 0.0], [3.0, 0.0, 0.0], [2.0, 1.0, 0.0]],
            normals: vec![[0.0, 0.0, 1.0]; 3],
            ..Default::default()
        };
        second.triangle_groups.insert(key.clone(), vec![[0, 1, 2]]);

        append_scene_object(&mut first, second).unwrap();

        assert_eq!(first.positions.len(), 6);
        assert_eq!(first.normals.len(), 6);
        assert_eq!(first.triangle_groups[&key], vec![[0, 1, 2], [3, 4, 5]]);
    }

    #[test]
    /// Verifies rotations do not create distinct tessellation scale variants.
    fn rotation_preserves_unit_tessellation_scale() {
        let transform = AffineTransform::from_matrix([
            [0.0, -1.0, 0.0, 0.0],
            [1.0, 0.0, 0.0, 0.0],
            [0.0, 0.0, 1.0, 0.0],
            [0.0, 0.0, 0.0, 1.0],
        ]);

        assert_eq!(linear_stretch_bound(transform).unwrap(), 1.0);
    }

    #[test]
    /// Verifies scale bucketing preserves the maximum non-uniform stretch.
    fn non_uniform_scale_uses_largest_singular_value() {
        let transform = AffineTransform::from_matrix([
            [0.0, -2.0, 0.0, 0.0],
            [3.0, 0.0, 0.0, 0.0],
            [0.0, 0.0, -4.0, 0.0],
            [0.0, 0.0, 0.0, 1.0],
        ]);

        assert_eq!(linear_stretch_bound(transform).unwrap(), 4.0);
    }

    #[test]
    /// Verifies repeated INSERTs compile one mesh and emit transformed nodes that share it.
    fn repeated_inserts_share_one_compiled_mesh() {
        let mut document = CadDocument::new();
        let line = Line::from_points(Vector3::ZERO, Vector3::new(2.0, 0.0, 0.0));
        let line_handle = document.add_entity(EntityType::Line(line)).unwrap();
        let mut record = BlockRecord::new("SHARED");
        record.handle = document.allocate_handle();
        record.entity_handles = vec![line_handle];
        document.block_records.add(record).unwrap();
        let first = Insert::new("SHARED", Vector3::new(10.0, 20.0, 0.0));
        let second = Insert::new("SHARED", Vector3::new(30.0, 40.0, 0.0));
        let mut worker = Worker {
            document: &document,
            include_hidden: true,
            export_edge_wires: true,
            angular_deflection: 0.1,
            linear_deflection: 0.1,
            reference_span: 1.0,
            budget: ResourceBudget::new(crate::limits::ResourceLimits::default()),
            scene: Scene::default(),
            block_variants: BTreeMap::new(),
            block_prototypes: Vec::new(),
            stats: ConversionStats::default(),
        };
        let mut geometry = GeometryBatches::default();
        let mut placements = Vec::new();

        worker
            .visit(
                EntityType::Insert(first),
                0,
                &mut Vec::new(),
                1.0,
                &mut geometry,
                &mut placements,
            )
            .unwrap();
        worker
            .visit(
                EntityType::Insert(second),
                0,
                &mut Vec::new(),
                1.0,
                &mut geometry,
                &mut placements,
            )
            .unwrap();
        worker.finish_model_scene(geometry, placements).unwrap();

        assert_eq!(worker.block_prototypes.len(), 1);
        assert_eq!(worker.scene.objects.len(), 1);
        assert_eq!(worker.scene.nodes.len(), 2);
        assert_eq!(worker.scene.nodes[0].mesh, worker.scene.nodes[1].mesh);
        assert_eq!(worker.scene.objects[0].line_segment_count(), 1);
        assert_eq!(
            worker.scene.nodes[0].transform.transform_point([0.0; 3]),
            [10.0, 20.0, 0.0]
        );
        assert_eq!(
            worker.scene.nodes[1].transform.transform_point([0.0; 3]),
            [30.0, 40.0, 0.0]
        );
    }
}
