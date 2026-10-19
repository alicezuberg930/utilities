use std::collections::BTreeMap;

use serde::Serialize;

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Rgba8 {
    pub red: u8,
    pub green: u8,
    pub blue: u8,
    pub alpha: u8,
}

impl Default for Rgba8 {
    /// Uses opaque white as the default CAD display color.
    fn default() -> Self {
        Self::WHITE
    }
}

impl Rgba8 {
    pub const WHITE: Self = Self {
        red: 255,
        green: 255,
        blue: 255,
        alpha: 255,
    };

    /// Produces a darker shade for unlit edge wires while preserving alpha.
    pub fn darkened(self) -> Self {
        Self {
            red: ((self.red as u16 * 3) / 5) as u8,
            green: ((self.green as u16 * 3) / 5) as u8,
            blue: ((self.blue as u16 * 3) / 5) as u8,
            alpha: self.alpha,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct PrimitiveKey {
    pub layer: String,
    pub color: Rgba8,
}

/// Stores a source-space affine transform as a row-major matrix that acts on
/// column vectors. Translation therefore occupies the fourth column.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct AffineTransform {
    pub matrix: [[f64; 4]; 4],
}

impl Default for AffineTransform {
    /// Returns the identity transform.
    fn default() -> Self {
        Self::identity()
    }
}

impl AffineTransform {
    /// Constructs a transform from a row-major affine matrix.
    pub const fn from_matrix(matrix: [[f64; 4]; 4]) -> Self {
        Self { matrix }
    }

    /// Constructs an identity transform.
    pub const fn identity() -> Self {
        Self::from_matrix([
            [1.0, 0.0, 0.0, 0.0],
            [0.0, 1.0, 0.0, 0.0],
            [0.0, 0.0, 1.0, 0.0],
            [0.0, 0.0, 0.0, 1.0],
        ])
    }

    /// Constructs a source-space translation transform.
    pub const fn translation(offset: [f64; 3]) -> Self {
        Self::from_matrix([
            [1.0, 0.0, 0.0, offset[0]],
            [0.0, 1.0, 0.0, offset[1]],
            [0.0, 0.0, 1.0, offset[2]],
            [0.0, 0.0, 0.0, 1.0],
        ])
    }

    /// Composes a child transform beneath this parent and returns `parent * child`.
    pub fn compose(self, child: Self) -> Self {
        let mut matrix = [[0.0; 4]; 4];
        for (row, target_row) in matrix.iter_mut().enumerate() {
            for (column, target) in target_row.iter_mut().enumerate() {
                *target = (0..4)
                    .map(|inner| self.matrix[row][inner] * child.matrix[inner][column])
                    .sum();
            }
        }
        Self::from_matrix(matrix)
    }

    /// Applies this affine transform to one source-space point.
    pub fn transform_point(self, point: [f64; 3]) -> [f64; 3] {
        [
            self.matrix[0][0] * point[0]
                + self.matrix[0][1] * point[1]
                + self.matrix[0][2] * point[2]
                + self.matrix[0][3],
            self.matrix[1][0] * point[0]
                + self.matrix[1][1] * point[1]
                + self.matrix[1][2] * point[2]
                + self.matrix[1][3],
            self.matrix[2][0] * point[0]
                + self.matrix[2][1] * point[1]
                + self.matrix[2][2] * point[2]
                + self.matrix[2][3],
        ]
    }

    /// Reports whether every matrix component is finite and the homogeneous row is affine.
    pub fn is_finite_affine(self) -> bool {
        self.matrix.iter().flatten().all(|value| value.is_finite())
            && self.matrix[3] == [0.0, 0.0, 0.0, 1.0]
    }
}

#[derive(Debug, Clone)]
pub struct SceneNode {
    pub name: String,
    pub mesh: usize,
    pub transform: AffineTransform,
}

#[derive(Debug, Default)]
pub struct Scene {
    /// Unique mesh definitions. Multiple nodes may reference the same mesh.
    pub objects: Vec<SceneObject>,
    /// Flat drawable occurrences whose transforms place mesh definitions in source space.
    pub nodes: Vec<SceneNode>,
    pub source_origin: [f64; 3],
    pub meters_per_unit: f64,
    pub source_units: i16,
}

impl Scene {
    /// Computes the center of all finite transformed mesh bounds and stores it
    /// as the origin used to rebase coordinates before `f32` GLB export.
    pub fn calculate_origin(&mut self) -> Option<[f64; 3]> {
        let mut minimum = [f64::INFINITY; 3];
        let mut maximum = [f64::NEG_INFINITY; 3];
        let mut found = false;

        for node in &self.nodes {
            let object = self.objects.get(node.mesh)?;
            if object
                .points()
                .any(|point| !point.iter().all(|value| value.is_finite()))
            {
                return None;
            }
            let Some((local_minimum, local_maximum)) = object.bounds() else {
                continue;
            };
            for corner in bounds_corners(local_minimum, local_maximum) {
                let point = node.transform.transform_point(corner);
                if !point.iter().all(|value| value.is_finite()) {
                    return None;
                }
                found = true;
                for axis in 0..3 {
                    minimum[axis] = minimum[axis].min(point[axis]);
                    maximum[axis] = maximum[axis].max(point[axis]);
                }
            }
        }

        if !found {
            return None;
        }
        self.source_origin = [
            minimum[0] * 0.5 + maximum[0] * 0.5,
            minimum[1] * 0.5 + maximum[1] * 0.5,
            minimum[2] * 0.5 + maximum[2] * 0.5,
        ];
        self.source_origin
            .iter()
            .all(|value| value.is_finite())
            .then_some(self.source_origin)
    }
}

#[derive(Debug, Default)]
pub struct SceneObject {
    pub name: String,
    pub positions: Vec<[f64; 3]>,
    pub normals: Vec<[f64; 3]>,
    pub triangle_groups: BTreeMap<PrimitiveKey, Vec<[u32; 3]>>,
    /// Each color/layer group stores independent line segments as consecutive endpoint pairs.
    pub line_groups: BTreeMap<PrimitiveKey, Vec<[f64; 3]>>,
}

impl SceneObject {
    /// Iterates over every triangle and line position owned by this mesh definition.
    fn points(&self) -> impl Iterator<Item = &[f64; 3]> {
        self.positions
            .iter()
            .chain(self.line_groups.values().flatten())
    }

    /// Computes finite local-space bounds for this mesh definition.
    fn bounds(&self) -> Option<([f64; 3], [f64; 3])> {
        let mut minimum = [f64::INFINITY; 3];
        let mut maximum = [f64::NEG_INFINITY; 3];
        let mut found = false;
        for point in self.points() {
            if !point.iter().all(|value| value.is_finite()) {
                continue;
            }
            found = true;
            for axis in 0..3 {
                minimum[axis] = minimum[axis].min(point[axis]);
                maximum[axis] = maximum[axis].max(point[axis]);
            }
        }
        found.then_some((minimum, maximum))
    }

    /// Counts all triangles across the mesh's material/layer groups.
    pub fn triangle_count(&self) -> usize {
        self.triangle_groups.values().map(Vec::len).sum()
    }

    /// Counts all independent line segments across the mesh's material/layer groups.
    pub fn line_segment_count(&self) -> usize {
        self.line_groups.values().map(|lines| lines.len() / 2).sum()
    }

    /// Reports whether the mesh definition contains no drawable triangles or lines.
    pub fn is_empty(&self) -> bool {
        self.triangle_count() == 0 && self.line_segment_count() == 0
    }
}

/// Returns the eight corners of an axis-aligned bounding box.
fn bounds_corners(minimum: [f64; 3], maximum: [f64; 3]) -> [[f64; 3]; 8] {
    [
        [minimum[0], minimum[1], minimum[2]],
        [minimum[0], minimum[1], maximum[2]],
        [minimum[0], maximum[1], minimum[2]],
        [minimum[0], maximum[1], maximum[2]],
        [maximum[0], minimum[1], minimum[2]],
        [maximum[0], minimum[1], maximum[2]],
        [maximum[0], maximum[1], minimum[2]],
        [maximum[0], maximum[1], maximum[2]],
    ]
}

#[derive(Debug, Default, Serialize)]
pub struct ConversionStats {
    pub source_units: i16,
    pub meters_per_unit: f64,
    pub linear_deflection_drawing_units: f64,
    pub document_entities: usize,
    pub model_space_entities: usize,
    pub visited_entities: usize,
    pub expanded_inserts: usize,
    pub skipped_hidden_entities: usize,
    pub missing_references: usize,
    pub missing_block_definitions: usize,
    pub non_explodable_insert_flags_ignored: usize,
    pub missing_referenced_entity_handles: usize,
    pub unresolved_external_references: usize,
    pub empty_path_reference_inserts: usize,
    pub empty_insert_expansions: usize,
    pub cyclic_references: usize,
    pub acis_entities: usize,
    pub acis_payloads_parsed: usize,
    pub acis_parse_failures: usize,
    pub acis_bodies_lifted: usize,
    pub acis_entities_without_bodies: usize,
    pub acis_faces: usize,
    pub acis_lost_surfaces: usize,
    pub acis_lost_curves: usize,
    pub acis_broken_records: usize,
    pub missing_faces: usize,
    pub invalid_native_faces: usize,
    pub degenerate_native_faces: usize,
    pub triangle_count: usize,
    pub edge_segment_count: usize,
    pub reference_issues: BTreeMap<String, usize>,
    pub unsupported_entities: BTreeMap<String, usize>,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    /// Verifies parent-child composition follows row-major column-vector semantics.
    fn composes_parent_and_child_transforms() {
        let parent = AffineTransform::translation([10.0, 20.0, 30.0]);
        let child = AffineTransform::from_matrix([
            [0.0, -1.0, 0.0, 2.0],
            [1.0, 0.0, 0.0, 3.0],
            [0.0, 0.0, 1.0, 4.0],
            [0.0, 0.0, 0.0, 1.0],
        ]);

        assert_eq!(
            parent.compose(child).transform_point([1.0, 0.0, 0.0]),
            [12.0, 24.0, 34.0]
        );
    }

    #[test]
    /// Verifies origin calculation includes every transformed occurrence of shared geometry.
    fn calculates_origin_from_transformed_mesh_bounds() {
        let object = SceneObject {
            name: "shared".to_owned(),
            line_groups: BTreeMap::from([(
                PrimitiveKey {
                    layer: "0".to_owned(),
                    color: Rgba8::WHITE,
                },
                vec![[0.0, 0.0, 0.0], [2.0, 4.0, 6.0]],
            )]),
            ..Default::default()
        };
        let mut scene = Scene {
            objects: vec![object],
            nodes: vec![
                SceneNode {
                    name: "left".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::translation([-10.0, 0.0, 0.0]),
                },
                SceneNode {
                    name: "right".to_owned(),
                    mesh: 0,
                    transform: AffineTransform::translation([20.0, 10.0, 0.0]),
                },
            ],
            ..Default::default()
        };

        assert_eq!(scene.calculate_origin(), Some([6.0, 7.0, 3.0]));
    }
}
