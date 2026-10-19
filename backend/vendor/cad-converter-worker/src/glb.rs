use std::collections::BTreeMap;
use std::fmt;
use std::fs::OpenOptions;
use std::io::{self, BufWriter, Write};
use std::path::{Path, PathBuf};

use serde_json::{json, Value};

use crate::limits::{ensure_within, ResourceLimitError};
use crate::scene::{AffineTransform, PrimitiveKey, Rgba8, Scene};

const ARRAY_BUFFER: u32 = 34_962;
const ELEMENT_ARRAY_BUFFER: u32 = 34_963;
const FLOAT: u32 = 5_126;
const UNSIGNED_INT: u32 = 5_125;
const MODE_LINES: u32 = 1;
const MODE_TRIANGLES: u32 = 4;

#[derive(Debug, Default)]
pub struct GlbStats {
    pub mesh_count: usize,
    pub node_count: usize,
    /// Drawable triangle occurrences after applying shared-mesh nodes.
    pub triangle_count: usize,
    /// Drawable line occurrences after applying shared-mesh nodes.
    pub line_segment_count: usize,
    /// Triangles encoded once across unique mesh definitions.
    pub unique_triangle_count: usize,
    /// Line segments encoded once across unique mesh definitions.
    pub unique_line_segment_count: usize,
    pub dropped_degenerate_triangles: usize,
    pub dropped_source_degenerate_triangles: usize,
    pub dropped_precision_collapsed_triangles: usize,
}

#[derive(Debug)]
pub enum GlbWriteError {
    Export(String),
    Resource(ResourceLimitError),
}

impl fmt::Display for GlbWriteError {
    /// Formats either an export failure or a resource-limit failure for diagnostics.
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Export(message) => formatter.write_str(message),
            Self::Resource(error) => error.fmt(formatter),
        }
    }
}

impl From<String> for GlbWriteError {
    /// Wraps a textual export failure as a GLB write error.
    fn from(error: String) -> Self {
        Self::Export(error)
    }
}

impl From<ResourceLimitError> for GlbWriteError {
    /// Wraps a resource-limit failure as a GLB write error.
    fn from(error: ResourceLimitError) -> Self {
        Self::Resource(error)
    }
}

/// Removes only an output file that this process successfully created and has
/// not yet finished. This deliberately lives beside `create_new`: checking for
/// existence in the caller would introduce a race that could delete another
/// process's file.
struct CreatedOutputGuard {
    path: PathBuf,
    committed: bool,
}

impl CreatedOutputGuard {
    /// Creates a guard for an output file that should be removed unless committed.
    fn new(path: &Path) -> Self {
        Self {
            path: path.to_owned(),
            committed: false,
        }
    }

    /// Marks the guarded output as complete so it is preserved when the guard drops.
    fn commit(&mut self) {
        self.committed = true;
    }
}

impl Drop for CreatedOutputGuard {
    /// Removes an incomplete output file when the guard leaves scope without a commit.
    fn drop(&mut self) {
        if !self.committed {
            let _ = std::fs::remove_file(&self.path);
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
struct MaterialKey {
    rgba: Rgba8,
    unlit: bool,
}

struct BinaryBuilder {
    data: Vec<u8>,
    views: Vec<Value>,
    accessors: Vec<Value>,
    maximum_bytes: u64,
}

impl BinaryBuilder {
    /// Creates an empty binary-buffer builder governed by the output byte limit.
    fn new(maximum_bytes: u64) -> Self {
        Self {
            data: Vec::new(),
            views: Vec::new(),
            accessors: Vec::new(),
            maximum_bytes,
        }
    }

    /// Reserves binary payload capacity after verifying the configured byte limit.
    fn reserve_data(&mut self, additional: usize) -> Result<(), ResourceLimitError> {
        let attempted = u64::try_from(self.data.len())
            .ok()
            .and_then(|length| {
                u64::try_from(additional)
                    .ok()
                    .and_then(|additional| length.checked_add(additional))
            })
            .ok_or_else(|| {
                ResourceLimitError::new("GLB binary bytes", u64::MAX, self.maximum_bytes)
            })?;
        ensure_within("GLB binary bytes", attempted, self.maximum_bytes)?;
        self.data
            .try_reserve(additional)
            .map_err(|_| ResourceLimitError::allocation("GLB binary bytes", self.maximum_bytes))
    }

    /// Pads the binary payload to the four-byte alignment required by glTF.
    fn align4(&mut self) -> Result<(), ResourceLimitError> {
        let padding = (4 - (self.data.len() % 4)) % 4;
        self.reserve_data(padding)?;
        while !self.data.len().is_multiple_of(4) {
            self.data.push(0);
        }
        Ok(())
    }

    /// Appends `VEC3` data and returns its accessor index, including bounds for positions.
    fn add_vec3(&mut self, values: &[[f32; 3]], position: bool) -> Result<usize, GlbWriteError> {
        debug_assert!(!values.is_empty());
        self.align4()?;
        let byte_length = values
            .len()
            .checked_mul(3)
            .and_then(|count| count.checked_mul(std::mem::size_of::<f32>()))
            .ok_or_else(|| {
                ResourceLimitError::new("GLB binary bytes", u64::MAX, self.maximum_bytes)
            })?;
        self.reserve_data(byte_length)?;
        let offset = self.data.len();
        for value in values {
            for component in value {
                self.data.extend_from_slice(&component.to_le_bytes());
            }
        }
        let length = self.data.len() - offset;
        let view = self.views.len();
        self.views
            .try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB metadata", self.maximum_bytes))?;
        self.views.push(json!({
            "buffer": 0,
            "byteOffset": offset,
            "byteLength": length,
            "target": ARRAY_BUFFER
        }));

        let mut accessor = json!({
            "bufferView": view,
            "byteOffset": 0,
            "componentType": FLOAT,
            "count": values.len(),
            "type": "VEC3"
        });
        if position {
            let (minimum, maximum) = bounds(values);
            accessor["min"] = json!(minimum);
            accessor["max"] = json!(maximum);
        }
        let index = self.accessors.len();
        self.accessors
            .try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB metadata", self.maximum_bytes))?;
        self.accessors.push(accessor);
        Ok(index)
    }

    /// Appends unsigned integer indices and returns their glTF accessor index.
    fn add_indices(&mut self, values: &[u32]) -> Result<usize, GlbWriteError> {
        debug_assert!(!values.is_empty());
        self.align4()?;
        let byte_length = values
            .len()
            .checked_mul(std::mem::size_of::<u32>())
            .ok_or_else(|| {
                ResourceLimitError::new("GLB binary bytes", u64::MAX, self.maximum_bytes)
            })?;
        self.reserve_data(byte_length)?;
        let offset = self.data.len();
        for value in values {
            self.data.extend_from_slice(&value.to_le_bytes());
        }
        let length = self.data.len() - offset;
        let view = self.views.len();
        self.views
            .try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB metadata", self.maximum_bytes))?;
        self.views.push(json!({
            "buffer": 0,
            "byteOffset": offset,
            "byteLength": length,
            "target": ELEMENT_ARRAY_BUFFER
        }));
        let minimum = values.iter().copied().min().unwrap_or(0);
        let maximum = values.iter().copied().max().unwrap_or(0);
        let index = self.accessors.len();
        self.accessors
            .try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB metadata", self.maximum_bytes))?;
        self.accessors.push(json!({
            "bufferView": view,
            "byteOffset": 0,
            "componentType": UNSIGNED_INT,
            "count": values.len(),
            "type": "SCALAR",
            "min": [minimum],
            "max": [maximum]
        }));
        Ok(index)
    }
}

struct CappedBuffer {
    data: Vec<u8>,
    maximum_bytes: u64,
    failure: Option<ResourceLimitError>,
}

impl CappedBuffer {
    /// Creates an in-memory buffer that refuses writes beyond the supplied byte limit.
    fn new(maximum_bytes: u64) -> Self {
        Self {
            data: Vec::new(),
            maximum_bytes,
            failure: None,
        }
    }
}

impl Write for CappedBuffer {
    /// Appends bytes while recording resource-limit and allocation failures.
    fn write(&mut self, bytes: &[u8]) -> io::Result<usize> {
        let attempted = u64::try_from(self.data.len())
            .ok()
            .and_then(|length| {
                u64::try_from(bytes.len())
                    .ok()
                    .and_then(|additional| length.checked_add(additional))
            })
            .unwrap_or(u64::MAX);
        if let Err(error) = ensure_within("GLB JSON bytes", attempted, self.maximum_bytes) {
            self.failure = Some(error);
            return Err(io::Error::other("GLB JSON resource limit exceeded"));
        }
        if self.data.try_reserve(bytes.len()).is_err() {
            self.failure = Some(ResourceLimitError::allocation(
                "GLB JSON bytes",
                self.maximum_bytes,
            ));
            return Err(io::Error::other("cannot allocate GLB JSON buffer"));
        }
        self.data.extend_from_slice(bytes);
        Ok(bytes.len())
    }

    /// Completes the `Write` contract because this memory buffer has nothing to flush.
    fn flush(&mut self) -> io::Result<()> {
        Ok(())
    }
}

/// Serializes a scene as a new, resource-bounded GLB file and returns export statistics.
pub fn write(
    path: &Path,
    scene: &Scene,
    maximum_output_bytes: u64,
) -> Result<GlbStats, GlbWriteError> {
    if !scene.meters_per_unit.is_finite() || scene.meters_per_unit <= 0.0 {
        return Err("drawing unit scale must be finite and positive"
            .to_owned()
            .into());
    }

    let mut binary = BinaryBuilder::new(maximum_output_bytes);
    let mut material_map = BTreeMap::<MaterialKey, usize>::new();
    let mut material_keys = Vec::<MaterialKey>::new();
    let mut json_meshes = Vec::<Value>::new();
    let mut nodes = Vec::<Value>::new();
    let mut scene_nodes = Vec::<usize>::new();
    let mut statistics = GlbStats::default();
    let mut uses_unlit = false;

    /// Returns the stable index of a deduplicated material, inserting it when absent.
    fn material_index(
        key: MaterialKey,
        map: &mut BTreeMap<MaterialKey, usize>,
        keys: &mut Vec<MaterialKey>,
        maximum_output_bytes: u64,
    ) -> Result<usize, GlbWriteError> {
        if let Some(index) = map.get(&key) {
            return Ok(*index);
        }
        let index = keys.len();
        keys.try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB metadata", maximum_output_bytes))?;
        keys.push(key);
        map.insert(key, index);
        Ok(index)
    }

    let mut source_mesh_exports = Vec::<Option<(usize, [f64; 3], usize, usize)>>::new();
    source_mesh_exports
        .try_reserve_exact(scene.objects.len())
        .map_err(|_| ResourceLimitError::allocation("GLB mesh index map", maximum_output_bytes))?;

    for source in &scene.objects {
        let mut primitives = Vec::<Value>::new();
        let anchor = mesh_anchor(source)?;
        let mut mesh_triangle_count = 0_usize;
        let mut mesh_line_segment_count = 0_usize;

        let has_triangles = source
            .triangle_groups
            .values()
            .any(|triangles| !triangles.is_empty());
        if has_triangles {
            if source.positions.is_empty() {
                return Err(format!("{} has triangles but no positions", source.name).into());
            }
            let mut positions = Vec::new();
            positions
                .try_reserve_exact(source.positions.len())
                .map_err(|_| {
                    ResourceLimitError::allocation(
                        "GLB mapped POSITION elements",
                        maximum_output_bytes,
                    )
                })?;
            for point in &source.positions {
                positions.push(map_local_position(*point, anchor, scene.meters_per_unit)?);
            }
            let mut grouped_indices = Vec::<(&PrimitiveKey, Vec<u32>)>::new();
            grouped_indices
                .try_reserve(source.triangle_groups.len())
                .map_err(|_| {
                    ResourceLimitError::allocation("GLB triangle groups", maximum_output_bytes)
                })?;
            for (key, triangles) in &source.triangle_groups {
                let index_count = triangles.len().checked_mul(3).ok_or_else(|| {
                    ResourceLimitError::new("GLB triangle indices", u64::MAX, maximum_output_bytes)
                })?;
                let mut indices = Vec::<u32>::new();
                indices.try_reserve_exact(index_count).map_err(|_| {
                    ResourceLimitError::allocation("GLB triangle indices", maximum_output_bytes)
                })?;
                for triangle in triangles {
                    if triangle
                        .iter()
                        .any(|index| *index as usize >= source.positions.len())
                    {
                        return Err(format!(
                            "{} contains an out-of-range triangle index",
                            source.name
                        )
                        .into());
                    }
                    if triangle[0] == triangle[1]
                        || triangle[1] == triangle[2]
                        || triangle[2] == triangle[0]
                        || triangle_area_is_zero_f64(*triangle, &source.positions)
                    {
                        statistics.dropped_degenerate_triangles += 1;
                        statistics.dropped_source_degenerate_triangles += 1;
                        continue;
                    }
                    if triangle_area_is_zero_f32(*triangle, &positions) {
                        statistics.dropped_degenerate_triangles += 1;
                        statistics.dropped_precision_collapsed_triangles += 1;
                        continue;
                    }
                    indices.extend_from_slice(triangle);
                }
                if indices.is_empty() {
                    continue;
                }
                grouped_indices.push((key, indices));
            }

            if !grouped_indices.is_empty() {
                let source_normals = source_normals(source)?;
                let mut normals = Vec::new();
                normals
                    .try_reserve_exact(source_normals.len())
                    .map_err(|_| {
                        ResourceLimitError::allocation(
                            "GLB mapped NORMAL elements",
                            maximum_output_bytes,
                        )
                    })?;
                for normal in source_normals {
                    normals.push(map_normal(normal)?);
                }
                let position_accessor = binary.add_vec3(&positions, true)?;
                let normal_accessor = binary.add_vec3(&normals, false)?;
                for (key, indices) in grouped_indices {
                    mesh_triangle_count += indices.len() / 3;
                    let index_accessor = binary.add_indices(&indices)?;
                    let material = material_index(
                        MaterialKey {
                            rgba: key.color,
                            unlit: false,
                        },
                        &mut material_map,
                        &mut material_keys,
                        maximum_output_bytes,
                    )?;
                    primitives.push(json!({
                        "attributes": {
                            "POSITION": position_accessor,
                            "NORMAL": normal_accessor
                        },
                        "indices": index_accessor,
                        "material": material,
                        "mode": MODE_TRIANGLES,
                        "extras": { "layer": key.layer }
                    }));
                }
            }
        }

        for (key, line_positions) in &source.line_groups {
            if line_positions.len() % 2 != 0 {
                return Err(format!(
                    "{} has an unmatched line endpoint on layer {}",
                    source.name, key.layer
                )
                .into());
            }
            let mut positions = Vec::<[f32; 3]>::new();
            positions
                .try_reserve_exact(line_positions.len())
                .map_err(|_| {
                    ResourceLimitError::allocation(
                        "GLB mapped line POSITION elements",
                        maximum_output_bytes,
                    )
                })?;
            for index in (0..line_positions.len()).step_by(2) {
                let from =
                    map_local_position(line_positions[index], anchor, scene.meters_per_unit)?;
                let to =
                    map_local_position(line_positions[index + 1], anchor, scene.meters_per_unit)?;
                if from == to {
                    continue;
                }
                positions.push(from);
                positions.push(to);
            }
            if !positions.is_empty() {
                uses_unlit = true;
                mesh_line_segment_count += positions.len() / 2;
                let position_accessor = binary.add_vec3(&positions, true)?;
                let material = material_index(
                    MaterialKey {
                        rgba: key.color.darkened(),
                        unlit: true,
                    },
                    &mut material_map,
                    &mut material_keys,
                    maximum_output_bytes,
                )?;
                primitives.push(json!({
                    "attributes": { "POSITION": position_accessor },
                    "material": material,
                    "mode": MODE_LINES,
                    "extras": { "layer": key.layer }
                }));
            }
        }

        if primitives.is_empty() {
            source_mesh_exports.push(None);
            continue;
        }
        let mesh_index = json_meshes.len();
        json_meshes.push(json!({
            "name": source.name,
            "primitives": primitives
        }));
        statistics.unique_triangle_count = statistics
            .unique_triangle_count
            .checked_add(mesh_triangle_count)
            .ok_or_else(|| {
                ResourceLimitError::new("unique GLB triangles", u64::MAX, maximum_output_bytes)
            })?;
        statistics.unique_line_segment_count = statistics
            .unique_line_segment_count
            .checked_add(mesh_line_segment_count)
            .ok_or_else(|| {
                ResourceLimitError::new("unique GLB line segments", u64::MAX, maximum_output_bytes)
            })?;
        source_mesh_exports.push(Some((
            mesh_index,
            anchor,
            mesh_triangle_count,
            mesh_line_segment_count,
        )));
        statistics.mesh_count += 1;
    }

    for source in &scene.nodes {
        if !source.transform.is_finite_affine() {
            return Err(format!(
                "{} contains a non-finite or non-affine transform",
                source.name
            )
            .into());
        }
        let Some(mesh_export) = source_mesh_exports.get(source.mesh) else {
            return Err(format!(
                "{} references out-of-range mesh {}",
                source.name, source.mesh
            )
            .into());
        };
        let Some((mesh, anchor, mesh_triangles, mesh_lines)) = *mesh_export else {
            continue;
        };
        let matrix = map_node_matrix(
            source.transform,
            anchor,
            scene.source_origin,
            scene.meters_per_unit,
        )?;
        let node_index = nodes.len();
        nodes
            .try_reserve(1)
            .map_err(|_| ResourceLimitError::allocation("GLB nodes", maximum_output_bytes))?;
        let mut node = json!({
            "name": source.name,
            "mesh": mesh
        });
        if matrix
            != [
                1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0,
            ]
        {
            node["matrix"] = json!(matrix);
        }
        nodes.push(node);
        scene_nodes.push(node_index);
        statistics.node_count += 1;
        statistics.triangle_count = statistics
            .triangle_count
            .checked_add(mesh_triangles)
            .ok_or_else(|| {
                ResourceLimitError::new("instanced GLB triangles", u64::MAX, maximum_output_bytes)
            })?;
        statistics.line_segment_count = statistics
            .line_segment_count
            .checked_add(mesh_lines)
            .ok_or_else(|| {
                ResourceLimitError::new(
                    "instanced GLB line segments",
                    u64::MAX,
                    maximum_output_bytes,
                )
            })?;
    }

    if json_meshes.is_empty() || scene_nodes.is_empty() {
        return Err("conversion produced no renderable geometry"
            .to_owned()
            .into());
    }

    let materials = material_keys.iter().map(material_json).collect::<Vec<_>>();
    let unpadded_binary_length = binary.data.len();
    let mut root = json!({
        "asset": {
            "version": "2.0",
            "generator": "Utilities Open CAD worker",
            "extras": {
                "sourceUpAxis": "Z",
                "sourceOrigin": scene.source_origin,
                "sourceInsertionUnits": scene.source_units,
                "unitToMeters": scene.meters_per_unit,
                "coordinatesRebased": true
            }
        },
        "scene": 0,
        "scenes": [{ "nodes": scene_nodes }],
        "nodes": nodes,
        "meshes": json_meshes,
        "materials": materials,
        "buffers": [{ "byteLength": unpadded_binary_length }],
        "bufferViews": binary.views,
        "accessors": binary.accessors
    });
    if uses_unlit {
        root["extensionsUsed"] = json!(["KHR_materials_unlit"]);
    }

    let mut json_buffer = CappedBuffer::new(maximum_output_bytes);
    if let Err(error) = serde_json::to_writer(&mut json_buffer, &root) {
        if let Some(resource_error) = json_buffer.failure {
            return Err(resource_error.into());
        }
        return Err(error.to_string().into());
    }
    let mut json_bytes = json_buffer.data;
    pad_to_alignment(
        &mut json_bytes,
        b' ',
        "GLB JSON bytes",
        maximum_output_bytes,
    )?;
    binary.align4()?;

    let total_length = 12usize
        .checked_add(8)
        .and_then(|value| value.checked_add(json_bytes.len()))
        .and_then(|value| value.checked_add(8))
        .and_then(|value| value.checked_add(binary.data.len()))
        .ok_or_else(|| {
            ResourceLimitError::new("GLB output bytes", u64::MAX, maximum_output_bytes)
        })?;
    ensure_within(
        "GLB output bytes",
        u64::try_from(total_length).unwrap_or(u64::MAX),
        maximum_output_bytes,
    )?;
    let total_length = u32::try_from(total_length).map_err(|_| "GLB exceeds 4 GiB".to_owned())?;
    let json_length =
        u32::try_from(json_bytes.len()).map_err(|_| "GLB JSON chunk is too large".to_owned())?;
    let binary_length =
        u32::try_from(binary.data.len()).map_err(|_| "GLB binary chunk is too large".to_owned())?;

    let file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| error.to_string())?;
    let mut output_guard = CreatedOutputGuard::new(path);
    let mut writer = BufWriter::new(file);
    write_u32(&mut writer, 0x4654_6C67)?;
    write_u32(&mut writer, 2)?;
    write_u32(&mut writer, total_length)?;
    write_u32(&mut writer, json_length)?;
    write_u32(&mut writer, 0x4E4F_534A)?;
    writer
        .write_all(&json_bytes)
        .map_err(|error| error.to_string())?;
    write_u32(&mut writer, binary_length)?;
    write_u32(&mut writer, 0x004E_4942)?;
    writer
        .write_all(&binary.data)
        .map_err(|error| error.to_string())?;
    writer.flush().map_err(|error| error.to_string())?;
    output_guard.commit();
    Ok(statistics)
}

/// Pads a byte vector to a four-byte boundary without exceeding its resource limit.
fn pad_to_alignment(
    bytes: &mut Vec<u8>,
    padding_byte: u8,
    resource: &'static str,
    maximum_bytes: u64,
) -> Result<(), ResourceLimitError> {
    let padding = (4 - (bytes.len() % 4)) % 4;
    let attempted = u64::try_from(bytes.len())
        .ok()
        .and_then(|length| {
            u64::try_from(padding)
                .ok()
                .and_then(|padding| length.checked_add(padding))
        })
        .ok_or_else(|| ResourceLimitError::new(resource, u64::MAX, maximum_bytes))?;
    ensure_within(resource, attempted, maximum_bytes)?;
    bytes
        .try_reserve(padding)
        .map_err(|_| ResourceLimitError::allocation(resource, maximum_bytes))?;
    bytes.resize(bytes.len() + padding, padding_byte);
    Ok(())
}

/// Writes a 32-bit integer in the little-endian byte order used by GLB headers.
fn write_u32(writer: &mut impl Write, value: u32) -> Result<(), String> {
    writer
        .write_all(&value.to_le_bytes())
        .map_err(|error| error.to_string())
}

/// Rebases, scales, and rotates a mesh-local source Z-up position into finite glTF coordinates.
fn map_local_position(point: [f64; 3], anchor: [f64; 3], scale: f64) -> Result<[f32; 3], String> {
    if !point.iter().all(|value| value.is_finite()) || !anchor.iter().all(|value| value.is_finite())
    {
        return Err("geometry contains a non-finite position".to_owned());
    }
    // A right-handed -90 degree rotation around +X maps DWG Z-up to glTF Y-up.
    let local = [
        point[0] - anchor[0],
        point[1] - anchor[1],
        point[2] - anchor[2],
    ];
    let mapped = [local[0] * scale, local[2] * scale, -local[1] * scale];
    if mapped
        .iter()
        .any(|value| !value.is_finite() || value.abs() > f32::MAX as f64)
    {
        return Err("mapped position is outside finite f32 range".to_owned());
    }
    Ok(mapped.map(|value| value as f32))
}

/// Conjugates a source-space affine transform into glTF axes and rebases its
/// translation before serializing the result in glTF's column-major order.
fn map_node_matrix(
    transform: AffineTransform,
    anchor: [f64; 3],
    origin: [f64; 3],
    scale: f64,
) -> Result<[f32; 16], String> {
    if !transform.is_finite_affine()
        || !anchor.iter().all(|value| value.is_finite())
        || !origin.iter().all(|value| value.is_finite())
    {
        return Err("scene contains a non-finite or non-affine transform".to_owned());
    }

    // R maps source (x, y, z) to glTF (x, z, -y). Because unit scale is
    // uniform, C*L*C^-1 simplifies from (sR)*L*(sR)^-1 to R*L*R^T.
    let rotation = [[1.0, 0.0, 0.0], [0.0, 0.0, 1.0], [0.0, -1.0, 0.0]];
    let mut temporary = [[0.0; 3]; 3];
    for (row, target_row) in temporary.iter_mut().enumerate() {
        for (column, target) in target_row.iter_mut().enumerate() {
            *target = (0..3)
                .map(|inner| rotation[row][inner] * transform.matrix[inner][column])
                .sum();
        }
    }
    let mut linear = [[0.0; 3]; 3];
    for (row, target_row) in linear.iter_mut().enumerate() {
        for (column, target) in target_row.iter_mut().enumerate() {
            *target = (0..3)
                .map(|inner| temporary[row][inner] * rotation[column][inner])
                .sum();
        }
    }

    let world_anchor = transform.transform_point(anchor);
    let source_translation = [
        world_anchor[0] - origin[0],
        world_anchor[1] - origin[1],
        world_anchor[2] - origin[2],
    ];
    let translation = [
        source_translation[0] * scale,
        source_translation[2] * scale,
        -source_translation[1] * scale,
    ];
    let values = [
        linear[0][0],
        linear[1][0],
        linear[2][0],
        0.0,
        linear[0][1],
        linear[1][1],
        linear[2][1],
        0.0,
        linear[0][2],
        linear[1][2],
        linear[2][2],
        0.0,
        translation[0],
        translation[1],
        translation[2],
        1.0,
    ];
    if values
        .iter()
        .any(|value| !value.is_finite() || value.abs() > f32::MAX as f64)
    {
        return Err("mapped node transform is outside finite f32 range".to_owned());
    }
    Ok(values.map(|value| value as f32))
}

/// Computes a finite local anchor at the center of all triangle and line positions.
fn mesh_anchor(source: &crate::scene::SceneObject) -> Result<[f64; 3], String> {
    let mut minimum = [f64::INFINITY; 3];
    let mut maximum = [f64::NEG_INFINITY; 3];
    let mut found = false;
    for point in source
        .positions
        .iter()
        .chain(source.line_groups.values().flatten())
    {
        if !point.iter().all(|value| value.is_finite()) {
            return Err(format!("{} contains a non-finite position", source.name));
        }
        found = true;
        for axis in 0..3 {
            minimum[axis] = minimum[axis].min(point[axis]);
            maximum[axis] = maximum[axis].max(point[axis]);
        }
    }
    if !found {
        return Ok([0.0; 3]);
    }
    let anchor = [
        minimum[0] * 0.5 + maximum[0] * 0.5,
        minimum[1] * 0.5 + maximum[1] * 0.5,
        minimum[2] * 0.5 + maximum[2] * 0.5,
    ];
    if anchor.iter().all(|value| value.is_finite()) {
        Ok(anchor)
    } else {
        Err(format!(
            "{} has bounds outside the finite range",
            source.name
        ))
    }
}

/// Rotates and normalizes a source-space normal for glTF output.
fn map_normal(normal: [f64; 3]) -> Result<[f32; 3], String> {
    let mapped = [normal[0], normal[2], -normal[1]];
    let length = mapped.iter().map(|value| value * value).sum::<f64>().sqrt();
    if !length.is_finite() || length <= f64::EPSILON {
        return Err("geometry contains an invalid normal".to_owned());
    }
    Ok(mapped.map(|value| (value / length) as f32))
}

/// Returns valid source normals or reconstructs smooth vertex normals from triangle faces.
fn source_normals(source: &crate::scene::SceneObject) -> Result<Vec<[f64; 3]>, String> {
    if source.normals.len() == source.positions.len()
        && source.normals.iter().all(|normal| {
            normal.iter().all(|value| value.is_finite())
                && normal.iter().map(|value| value * value).sum::<f64>() > f64::EPSILON
        })
    {
        return Ok(source.normals.clone());
    }
    let mut normals = vec![[0.0; 3]; source.positions.len()];
    for triangle in source.triangle_groups.values().flatten() {
        if triangle
            .iter()
            .any(|index| *index as usize >= source.positions.len())
        {
            return Err(format!(
                "{} contains an out-of-range triangle index",
                source.name
            ));
        }
        let a = source.positions[triangle[0] as usize];
        let b = source.positions[triangle[1] as usize];
        let c = source.positions[triangle[2] as usize];
        let face = cross(subtract(b, a), subtract(c, a));
        for index in triangle {
            for (target, component) in normals[*index as usize].iter_mut().zip(face) {
                *target += component;
            }
        }
    }
    for normal in &mut normals {
        let length = normal.iter().map(|value| value * value).sum::<f64>().sqrt();
        if !length.is_finite() || length <= f64::EPSILON {
            // Some native meshes retain vertices that no surviving triangle
            // references. A valid fallback keeps the shared accessor legal;
            // it cannot affect rendering because no index selects the vertex.
            *normal = [0.0, 0.0, 1.0];
            continue;
        }
        for component in normal {
            *component /= length;
        }
    }
    Ok(normals)
}

/// Detects a triangle that becomes degenerate after conversion to `f32` positions.
fn triangle_area_is_zero_f32(triangle: [u32; 3], positions: &[[f32; 3]]) -> bool {
    let a = positions[triangle[0] as usize].map(f64::from);
    let b = positions[triangle[1] as usize].map(f64::from);
    let c = positions[triangle[2] as usize].map(f64::from);
    triangle_area_is_zero(a, b, c)
}

/// Detects a triangle that is already degenerate in source `f64` positions.
fn triangle_area_is_zero_f64(triangle: [u32; 3], positions: &[[f64; 3]]) -> bool {
    let a = positions[triangle[0] as usize];
    let b = positions[triangle[1] as usize];
    let c = positions[triangle[2] as usize];
    triangle_area_is_zero(a, b, c)
}

/// Tests whether three points span effectively zero squared area.
fn triangle_area_is_zero(a: [f64; 3], b: [f64; 3], c: [f64; 3]) -> bool {
    let area = cross(subtract(b, a), subtract(c, a));
    area.iter().map(|value| value * value).sum::<f64>() <= f64::MIN_POSITIVE
}

/// Subtracts one three-dimensional vector from another component by component.
fn subtract(left: [f64; 3], right: [f64; 3]) -> [f64; 3] {
    [left[0] - right[0], left[1] - right[1], left[2] - right[2]]
}

/// Computes the three-dimensional cross product of two vectors.
fn cross(left: [f64; 3], right: [f64; 3]) -> [f64; 3] {
    [
        left[1] * right[2] - left[2] * right[1],
        left[2] * right[0] - left[0] * right[2],
        left[0] * right[1] - left[1] * right[0],
    ]
}

/// Computes component-wise minimum and maximum bounds for non-empty positions.
fn bounds(values: &[[f32; 3]]) -> ([f32; 3], [f32; 3]) {
    let mut minimum = [f32::INFINITY; 3];
    let mut maximum = [f32::NEG_INFINITY; 3];
    for value in values {
        for axis in 0..3 {
            minimum[axis] = minimum[axis].min(value[axis]);
            maximum[axis] = maximum[axis].max(value[axis]);
        }
    }
    (minimum, maximum)
}

/// Converts an 8-bit sRGB channel value to its linear-light representation.
fn srgb_to_linear(value: u8) -> f64 {
    let value = f64::from(value) / 255.0;
    if value <= 0.04045 {
        value / 12.92
    } else {
        ((value + 0.055) / 1.055).powf(2.4)
    }
}

/// Builds the glTF material JSON for a face or unlit edge color.
fn material_json(key: &MaterialKey) -> Value {
    let Rgba8 {
        red,
        green,
        blue,
        alpha,
    } = key.rgba;
    let mut material = json!({
        "name": format!(
            "{}_{red:02X}{green:02X}{blue:02X}{alpha:02X}",
            if key.unlit { "edge" } else { "face" }
        ),
        "pbrMetallicRoughness": {
            "baseColorFactor": [
                srgb_to_linear(red),
                srgb_to_linear(green),
                srgb_to_linear(blue),
                f64::from(alpha) / 255.0
            ],
            "metallicFactor": 0.0,
            "roughnessFactor": 0.8
        },
        "doubleSided": true
    });
    if alpha < 255 {
        material["alphaMode"] = json!("BLEND");
    }
    if key.unlit {
        material["extensions"] = json!({ "KHR_materials_unlit": {} });
    }
    material
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::limits::DEFAULT_MAX_OUTPUT_BYTES;
    use crate::scene::{AffineTransform, PrimitiveKey, Rgba8, SceneNode, SceneObject};

    /// Creates a material/layer grouping key used by test geometry.
    fn key(layer: &str, color: Rgba8) -> PrimitiveKey {
        PrimitiveKey {
            layer: layer.to_owned(),
            color,
        }
    }

    /// Builds a scene containing one identity occurrence of a mesh definition.
    fn single_node_scene(object: SceneObject) -> Scene {
        let mut scene = Scene {
            objects: vec![object],
            nodes: vec![SceneNode {
                name: "occurrence".to_owned(),
                mesh: 0,
                transform: AffineTransform::identity(),
            }],
            meters_per_unit: 1.0,
            source_units: 6,
            ..Default::default()
        };
        scene.calculate_origin();
        scene
    }

    /// Reads the JSON chunk from a GLB generated by a test.
    fn read_json(path: &Path) -> Value {
        let bytes = std::fs::read(path).unwrap();
        let json_length = u32::from_le_bytes(bytes[12..16].try_into().unwrap()) as usize;
        serde_json::from_slice(&bytes[20..20 + json_length]).unwrap()
    }

    /// Applies a glTF column-major affine matrix to a point.
    fn transform_gltf_point(matrix: [f32; 16], point: [f32; 3]) -> [f32; 3] {
        [
            matrix[0] * point[0] + matrix[4] * point[1] + matrix[8] * point[2] + matrix[12],
            matrix[1] * point[0] + matrix[5] * point[1] + matrix[9] * point[2] + matrix[13],
            matrix[2] * point[0] + matrix[6] * point[1] + matrix[10] * point[2] + matrix[14],
        ]
    }

    #[test]
    /// Verifies mixed triangle and line geometry produces an aligned, well-sized GLB.
    fn writes_aligned_glb_with_triangles_and_lines() {
        let output = std::env::temp_dir().join("utilities-glb-writer-test.glb");
        let _ = std::fs::remove_file(&output);
        let mut object = SceneObject {
            name: "triangle".to_owned(),
            positions: vec![
                [1_000_000.0, 2_000_000.0, 0.0],
                [1_000_001.0, 2_000_000.0, 0.0],
                [1_000_000.0, 2_000_001.0, 0.0],
            ],
            normals: vec![[0.0, 0.0, 1.0]; 3],
            line_groups: BTreeMap::from([(
                key("A-WALL", Rgba8::WHITE),
                vec![
                    [1_000_000.0, 2_000_000.0, 0.0],
                    [1_000_001.0, 2_000_000.0, 0.0],
                ],
            )]),
            ..Default::default()
        };
        object
            .triangle_groups
            .insert(key("A-WALL", Rgba8::WHITE), vec![[0, 1, 2]]);
        let scene = single_node_scene(object);
        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();
        assert_eq!(stats.mesh_count, 1);
        assert_eq!(stats.node_count, 1);
        assert_eq!(stats.triangle_count, 1);
        assert_eq!(stats.line_segment_count, 1);
        let bytes = std::fs::read(output).unwrap();
        assert_eq!(&bytes[..4], b"glTF");
        assert!(bytes.len().is_multiple_of(4));
        assert_eq!(
            u32::from_le_bytes(bytes[8..12].try_into().unwrap()) as usize,
            bytes.len()
        );
    }

    #[test]
    /// Verifies a scene containing only wire geometry remains valid and uses line mode.
    fn writes_line_only_glb() {
        let output = std::env::temp_dir().join("utilities-glb-writer-lines-test.glb");
        let _ = std::fs::remove_file(&output);
        let object = SceneObject {
            name: "wire".to_owned(),
            line_groups: BTreeMap::from([(
                key("REFERENCE", Rgba8::WHITE),
                vec![[10.0, 20.0, 30.0], [11.0, 20.0, 30.0]],
            )]),
            ..Default::default()
        };
        let scene = single_node_scene(object);
        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();
        assert_eq!(stats.triangle_count, 0);
        assert_eq!(stats.line_segment_count, 1);
        let bytes = std::fs::read(&output).unwrap();
        assert_eq!(&bytes[..4], b"glTF");
        let root = read_json(&output);
        assert_eq!(root["meshes"][0]["primitives"][0]["mode"], MODE_LINES);
        assert_eq!(
            root["meshes"][0]["primitives"][0]["extras"]["layer"],
            "REFERENCE"
        );
    }

    #[test]
    /// Verifies repeated occurrences share one emitted mesh and accessor set.
    fn writes_shared_mesh_once_for_multiple_nodes() {
        let output = std::env::temp_dir().join("utilities-glb-writer-shared-test.glb");
        let _ = std::fs::remove_file(&output);
        let object = SceneObject {
            name: "shared-wire".to_owned(),
            line_groups: BTreeMap::from([(
                key("0", Rgba8::WHITE),
                vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]],
            )]),
            ..Default::default()
        };
        let mut scene = Scene {
            objects: vec![object],
            nodes: vec![
                SceneNode {
                    name: "first".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::identity(),
                },
                SceneNode {
                    name: "second".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::translation([10.0, 0.0, 0.0]),
                },
            ],
            meters_per_unit: 1.0,
            source_units: 6,
            ..Default::default()
        };
        scene.calculate_origin();

        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();
        let root = read_json(&output);

        assert_eq!(stats.mesh_count, 1);
        assert_eq!(stats.node_count, 2);
        assert_eq!(stats.line_segment_count, 2);
        assert_eq!(stats.unique_line_segment_count, 1);
        assert_eq!(root["meshes"].as_array().unwrap().len(), 1);
        assert_eq!(root["accessors"].as_array().unwrap().len(), 1);
        assert_eq!(root["nodes"].as_array().unwrap().len(), 2);
        assert_eq!(root["nodes"][0]["mesh"], 0);
        assert_eq!(root["nodes"][1]["mesh"], 0);
    }

    #[test]
    /// Verifies source affine transforms are conjugated and serialized column-major.
    fn maps_source_transform_to_gltf_matrix() {
        let transform = AffineTransform::from_matrix([
            [0.0, -2.0, 0.0, 100.0],
            [3.0, 0.0, 0.0, 200.0],
            [0.0, 0.0, 4.0, 300.0],
            [0.0, 0.0, 0.0, 1.0],
        ]);
        let anchor = [1.0, 1.0, 1.0];
        let matrix = map_node_matrix(transform, anchor, [90.0, 180.0, 270.0], 0.001).unwrap();
        let point = map_local_position([2.0, 3.0, 4.0], anchor, 0.001).unwrap();
        let mapped = transform_gltf_point(matrix, point);

        assert!((mapped[0] - 0.004).abs() < 1.0e-6);
        assert!((mapped[1] - 0.046).abs() < 1.0e-6);
        assert!((mapped[2] + 0.026).abs() < 1.0e-6);
    }

    #[test]
    /// Verifies per-mesh anchors preserve small geometry at very large CAD coordinates.
    fn preserves_large_coordinate_geometry_before_f32_conversion() {
        let output = std::env::temp_dir().join("utilities-glb-writer-large-origin-test.glb");
        let _ = std::fs::remove_file(&output);
        let base = 1.0e12;
        let mut object = SceneObject {
            name: "large-origin".to_owned(),
            positions: vec![
                [base, base, base],
                [base + 1.0, base, base],
                [base, base + 1.0, base],
            ],
            normals: vec![[0.0, 0.0, 1.0]; 3],
            line_groups: BTreeMap::from([(
                key("SURVEY", Rgba8::WHITE),
                vec![[base, base, base], [base + 1.0, base, base]],
            )]),
            ..Default::default()
        };
        object
            .triangle_groups
            .insert(key("SURVEY", Rgba8::WHITE), vec![[0, 1, 2]]);
        let mut scene = Scene {
            objects: vec![object],
            nodes: vec![
                SceneNode {
                    name: "first".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::identity(),
                },
                SceneNode {
                    name: "second".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::translation([10.0, 0.0, 0.0]),
                },
            ],
            meters_per_unit: 1.0,
            source_units: 6,
            ..Default::default()
        };
        scene.calculate_origin().unwrap();

        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();

        assert_eq!(stats.mesh_count, 1);
        assert_eq!(stats.node_count, 2);
        assert_eq!(stats.triangle_count, 2);
        assert_eq!(stats.line_segment_count, 2);
        assert_eq!(stats.unique_triangle_count, 1);
        assert_eq!(stats.unique_line_segment_count, 1);
        assert_eq!(stats.dropped_precision_collapsed_triangles, 0);
    }

    #[test]
    /// Verifies source-space zero-area triangles are dropped and reported separately.
    fn reports_source_zero_area_triangles_without_rejecting_them() {
        let output = std::env::temp_dir().join("utilities-glb-writer-degenerate-test.glb");
        let _ = std::fs::remove_file(&output);
        let mut object = SceneObject {
            name: "mixed".to_owned(),
            positions: vec![
                [0.0, 0.0, 0.0],
                [1.0, 0.0, 0.0],
                [0.0, 1.0, 0.0],
                [2.0, 0.0, 0.0],
            ],
            ..Default::default()
        };
        object
            .triangle_groups
            .insert(key("0", Rgba8::WHITE), vec![[0, 1, 2], [0, 1, 3]]);
        let scene = single_node_scene(object);

        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();

        assert_eq!(stats.triangle_count, 1);
        assert_eq!(stats.dropped_source_degenerate_triangles, 1);
        assert_eq!(stats.dropped_precision_collapsed_triangles, 0);
    }

    #[test]
    /// Verifies triangles collapsed only by `f32` mapping are dropped and reported.
    fn reports_triangles_that_only_collapse_during_f32_mapping() {
        let output = std::env::temp_dir().join("utilities-glb-writer-collapse-test.glb");
        let _ = std::fs::remove_file(&output);
        let mut object = SceneObject {
            name: "tiny".to_owned(),
            positions: vec![[0.0, 0.0, 0.0], [1.0e-50, 0.0, 0.0], [0.0, 1.0e-50, 0.0]],
            line_groups: BTreeMap::from([(
                key("0", Rgba8::WHITE),
                vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]],
            )]),
            ..Default::default()
        };
        object
            .triangle_groups
            .insert(key("0", Rgba8::WHITE), vec![[0, 1, 2]]);
        let scene = single_node_scene(object);

        let stats = write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).unwrap();

        assert_eq!(stats.triangle_count, 0);
        assert_eq!(stats.dropped_source_degenerate_triangles, 0);
        assert_eq!(stats.dropped_precision_collapsed_triangles, 1);
        assert!(output.exists());
    }

    #[test]
    /// Verifies an output-size violation occurs before any destination file is created.
    fn output_limit_fails_before_creating_a_file() {
        let output = std::env::temp_dir().join("utilities-glb-writer-limit-test.glb");
        let _ = std::fs::remove_file(&output);
        let object = SceneObject {
            name: "wire".to_owned(),
            line_groups: BTreeMap::from([(
                key("REFERENCE", Rgba8::WHITE),
                vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]],
            )]),
            ..Default::default()
        };
        let scene = single_node_scene(object);

        let error = write(&output, &scene, 64).unwrap_err();

        assert!(matches!(error, GlbWriteError::Resource(_)));
        assert!(!output.exists());
    }

    #[test]
    /// Verifies the output guard deletes partial files but preserves committed files.
    fn created_output_guard_removes_only_uncommitted_files() {
        let incomplete = std::env::temp_dir().join("utilities-glb-guard-incomplete.glb");
        let complete = std::env::temp_dir().join("utilities-glb-guard-complete.glb");
        let _ = std::fs::remove_file(&incomplete);
        let _ = std::fs::remove_file(&complete);
        std::fs::write(&incomplete, b"partial").unwrap();
        std::fs::write(&complete, b"complete").unwrap();

        {
            let _guard = CreatedOutputGuard::new(&incomplete);
        }
        {
            let mut guard = CreatedOutputGuard::new(&complete);
            guard.commit();
        }

        assert!(!incomplete.exists());
        assert_eq!(std::fs::read(&complete).unwrap(), b"complete");
        std::fs::remove_file(complete).unwrap();
    }

    #[test]
    /// Verifies a failed exclusive create never deletes a pre-existing destination file.
    fn preexisting_output_is_never_removed_when_create_new_fails() {
        let output = std::env::temp_dir().join("utilities-glb-existing-output.glb");
        let _ = std::fs::remove_file(&output);
        std::fs::write(&output, b"belongs to another process").unwrap();
        let object = SceneObject {
            name: "wire".to_owned(),
            line_groups: BTreeMap::from([(
                key("REFERENCE", Rgba8::WHITE),
                vec![[0.0, 0.0, 0.0], [1.0, 0.0, 0.0]],
            )]),
            ..Default::default()
        };
        let scene = single_node_scene(object);

        assert!(write(&output, &scene, DEFAULT_MAX_OUTPUT_BYTES).is_err());
        assert_eq!(
            std::fs::read(&output).unwrap(),
            b"belongs to another process"
        );
        std::fs::remove_file(output).unwrap();
    }
}
