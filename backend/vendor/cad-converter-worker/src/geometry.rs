use std::f64::consts::TAU;

use opencadcodec::entities::{
    Face3D, Helix, Mesh, Point, PolyfaceMesh, PolygonMeshEntity, Solid, Spline,
};
use opencadcodec::types::Matrix3;
use opencadcodec::{EntityType, Vector3};
use opencadkernel::space::{
    clamped_uniform_knots, HelixCurve, HelixDirection, NurbsCurve3, Parameterization,
};

use crate::limits::{ensure_within, ResourceLimitError};

const MAX_CURVE_POINTS: usize = 1_000_000;
const MAX_CURVE_REFINEMENT_DEPTH: usize = 16;
const MAX_NURBS_DEGREE: usize = 64;

#[derive(Debug, Default)]
pub struct Geometry {
    pub positions: Vec<[f64; 3]>,
    pub normals: Vec<[f64; 3]>,
    pub triangles: Vec<[u32; 3]>,
    /// Consecutive pairs form independent line segments.
    pub line_positions: Vec<[f64; 3]>,
    /// Faces that referenced missing vertices or otherwise had malformed data.
    pub invalid_faces: usize,
    /// Faces whose supplied vertices have no finite, non-zero surface area.
    pub degenerate_faces: usize,
}

#[derive(Debug, Clone, Copy)]
pub struct GeometryCapacity {
    pub position_elements: u64,
    pub triangles: u64,
    pub line_segments: u64,
}

impl Geometry {
    /// Returns whether the geometry contains no triangle or line primitives.
    pub fn is_empty(&self) -> bool {
        self.triangles.is_empty() && self.line_positions.is_empty()
    }

    /// Counts all position elements used by triangle and line primitives.
    pub fn position_element_count(&self) -> usize {
        self.positions
            .len()
            .saturating_add(self.line_positions.len())
    }

    /// Verifies that the accumulated geometry stays within the configured limits.
    fn ensure_within(&self, capacity: GeometryCapacity) -> Result<(), ResourceLimitError> {
        ensure_within(
            "POSITION elements",
            u64::try_from(self.position_element_count()).unwrap_or(u64::MAX),
            capacity.position_elements,
        )?;
        ensure_within(
            "source triangles",
            u64::try_from(self.triangles.len()).unwrap_or(u64::MAX),
            capacity.triangles,
        )?;
        ensure_within(
            "line segments",
            u64::try_from(self.line_positions.len() / 2).unwrap_or(u64::MAX),
            capacity.line_segments,
        )
    }
}

/// Convert native non-ACIS entities that have an unambiguous geometric
/// representation. Annotation and proxy entities deliberately return `None`;
/// the caller records those in the completeness report.
pub fn extract(
    entity: &EntityType,
    angular_deflection: f64,
    linear_deflection: f64,
    reference_span: f64,
    capacity: GeometryCapacity,
) -> Result<Option<Geometry>, ResourceLimitError> {
    let mut geometry = Geometry::default();
    match entity {
        EntityType::Point(value) => {
            append_point_marker(
                &mut geometry.line_positions,
                value,
                reference_span,
                linear_deflection,
            );
        }
        EntityType::Line(value) => {
            push_segment(&mut geometry.line_positions, value.start, value.end);
        }
        EntityType::Ray(value) => {
            let Some(direction) = finite_unit(value.direction) else {
                return Ok(None);
            };
            push_segment(
                &mut geometry.line_positions,
                value.base_point,
                value.base_point + direction * valid_reference_span(reference_span),
            );
        }
        EntityType::XLine(value) => {
            let Some(direction) = finite_unit(value.direction) else {
                return Ok(None);
            };
            let reach = direction * valid_reference_span(reference_span);
            push_segment(
                &mut geometry.line_positions,
                value.base_point - reach,
                value.base_point + reach,
            );
        }
        EntityType::Arc(value) => {
            let points = sample_parametric(
                value.start_angle,
                value.sweep_angle(),
                value.radius,
                angular_deflection,
                linear_deflection,
                false,
                maximum_curve_points(capacity, false)?,
                |angle| value.point_at_angle_wcs(angle),
            )?;
            push_polyline(&mut geometry.line_positions, &points, false);
        }
        EntityType::Circle(value) => {
            let points = sample_parametric(
                0.0,
                TAU,
                value.radius,
                angular_deflection,
                linear_deflection,
                true,
                maximum_curve_points(capacity, true)?,
                |angle| value.point_at_angle_wcs(angle),
            )?;
            push_polyline(&mut geometry.line_positions, &points, true);
        }
        EntityType::Ellipse(value) => {
            let major_length = value.major_axis.length();
            if major_length.is_finite() && major_length > f64::EPSILON {
                let normal = value.normal.normalize();
                let minor_axis = normal.cross(&value.major_axis.normalize())
                    * (major_length * value.minor_axis_ratio);
                let mut sweep = value.end_parameter - value.start_parameter;
                if sweep <= 0.0 {
                    sweep += TAU;
                }
                let closed = value.is_full() || (sweep - TAU).abs() <= 1.0e-10;
                let points = sample_parametric(
                    value.start_parameter,
                    sweep,
                    major_length.max(value.minor_axis_length()),
                    angular_deflection,
                    linear_deflection,
                    closed,
                    maximum_curve_points(capacity, closed)?,
                    |angle| {
                        value.center + value.major_axis * angle.cos() + minor_axis * angle.sin()
                    },
                )?;
                push_polyline(&mut geometry.line_positions, &points, closed);
            }
        }
        EntityType::Polyline(value) => {
            let points = value
                .vertices
                .iter()
                .map(|vertex| vertex.location)
                .collect::<Vec<_>>();
            push_polyline(&mut geometry.line_positions, &points, value.is_closed());
        }
        EntityType::Polyline3D(value) => {
            let points = value
                .vertices
                .iter()
                .map(|vertex| vertex.position)
                .collect::<Vec<_>>();
            push_polyline(&mut geometry.line_positions, &points, value.is_closed());
        }
        EntityType::Polyline2D(value) => {
            let basis = Matrix3::arbitrary_axis(value.normal);
            let vertices = value
                .vertices
                .iter()
                .map(|vertex| ([vertex.location.x, vertex.location.y], vertex.bulge))
                .collect::<Vec<_>>();
            let points = sample_bulged_polyline(
                &vertices,
                value.is_closed(),
                angular_deflection,
                linear_deflection,
                maximum_curve_points(capacity, false)?,
            )?;
            let points = points
                .into_iter()
                .map(|point| basis * Vector3::new(point[0], point[1], value.elevation))
                .collect::<Vec<_>>();
            push_polyline(&mut geometry.line_positions, &points, false);
        }
        EntityType::LwPolyline(value) => {
            let basis = Matrix3::arbitrary_axis(value.normal);
            let vertices = value
                .vertices
                .iter()
                .map(|vertex| ([vertex.location.x, vertex.location.y], vertex.bulge))
                .collect::<Vec<_>>();
            let points = sample_bulged_polyline(
                &vertices,
                value.is_closed,
                angular_deflection,
                linear_deflection,
                maximum_curve_points(capacity, false)?,
            )?;
            let points = points
                .into_iter()
                .map(|point| basis * Vector3::new(point[0], point[1], value.elevation))
                .collect::<Vec<_>>();
            push_polyline(&mut geometry.line_positions, &points, false);
        }
        EntityType::Spline(value) => {
            let Some(curve) = spline_curve(value) else {
                return Ok(None);
            };
            let Some(points) = sample_nurbs(&curve, angular_deflection, linear_deflection) else {
                return Ok(None);
            };
            push_polyline(
                &mut geometry.line_positions,
                &points,
                value.flags.closed || value.flags.periodic || curve.is_closed(),
            );
        }
        EntityType::Helix(value) => {
            let Some(curve) = spline_curve(&value.spline).or_else(|| generated_helix_curve(value))
            else {
                return Ok(None);
            };
            let Some(points) = sample_nurbs(&curve, angular_deflection, linear_deflection) else {
                return Ok(None);
            };
            push_polyline(&mut geometry.line_positions, &points, curve.is_closed());
        }
        EntityType::Face3D(value) => append_face3d(&mut geometry, value),
        EntityType::Solid(value) => append_solid(&mut geometry, value),
        EntityType::Mesh(value) => append_mesh(&mut geometry, value),
        EntityType::PolyfaceMesh(value) => append_polyface_mesh(&mut geometry, value),
        EntityType::PolygonMesh(value) => append_polygon_mesh(&mut geometry, value),
        _ => return Ok(None),
    }
    geometry.ensure_within(capacity)?;
    Ok(Some(geometry))
}

/// Calculates the largest permitted point count for a sampled native curve.
fn maximum_curve_points(
    capacity: GeometryCapacity,
    closed: bool,
) -> Result<usize, ResourceLimitError> {
    let by_positions = capacity.position_elements / 2;
    let maximum_segments = capacity.line_segments.min(by_positions);
    let points = maximum_segments
        .saturating_add(if closed { 0 } else { 1 })
        .min(MAX_CURVE_POINTS as u64);
    if points == 0 {
        return Err(ResourceLimitError::new(
            "line segments",
            1,
            capacity.line_segments,
        ));
    }
    Ok(usize::try_from(points).unwrap_or(MAX_CURVE_POINTS))
}

/// Choose a finite display span for CAD reference geometry. `$EXTMIN` and
/// `$EXTMAX` normally describe model space without requiring an expensive
/// traversal, so rays and construction lines use their diagonal. Drawings
/// with absent/invalid/zero extents fall back to at least one drawing unit or
/// one thousand chord tolerances. The same value sizes point markers.
pub fn reference_span_from_extents(
    minimum: Vector3,
    maximum: Vector3,
    linear_deflection: f64,
) -> f64 {
    let valid_extents = finite_vector(minimum)
        && finite_vector(maximum)
        && minimum.x <= maximum.x
        && minimum.y <= maximum.y
        && minimum.z <= maximum.z;
    if valid_extents {
        let diagonal = (maximum - minimum).length();
        if diagonal.is_finite() && diagonal > f64::EPSILON {
            return diagonal;
        }
    }
    (linear_deflection * 1_000.0).max(1.0).min(f64::MAX.sqrt())
}

/// Replaces an invalid or zero reference span with a safe default length.
fn valid_reference_span(value: f64) -> f64 {
    if value.is_finite() && value > f64::EPSILON {
        value
    } else {
        1.0
    }
}

/// Adds a small three-axis wire marker for a finite CAD point entity.
fn append_point_marker(
    output: &mut Vec<[f64; 3]>,
    point: &Point,
    reference_span: f64,
    linear_deflection: f64,
) {
    if !finite_vector(point.location) {
        return;
    }
    let span = valid_reference_span(reference_span);
    let tolerance_size = if linear_deflection.is_finite() && linear_deflection > 0.0 {
        linear_deflection * 2.0
    } else {
        0.0
    };
    let size = (span * 0.005)
        .max(tolerance_size)
        .min(span * 0.05)
        .max(f64::EPSILON);
    let normal = finite_unit(point.normal).unwrap_or(Vector3::UNIT_Z);
    let basis = Matrix3::arbitrary_axis(normal);
    let (sin, cos) = point.x_axis_angle.sin_cos();
    let mut x_axis = basis * Vector3::new(cos, sin, 0.0);
    if finite_unit(x_axis).is_none() {
        x_axis = basis * Vector3::UNIT_X;
    }
    let x_axis = finite_unit(x_axis).unwrap_or(Vector3::UNIT_X);
    let y_axis = finite_unit(normal.cross(&x_axis)).unwrap_or(Vector3::UNIT_Y);
    for axis in [x_axis, y_axis, normal] {
        push_segment(
            output,
            point.location - axis * size,
            point.location + axis * size,
        );
    }
}

/// Builds a NURBS curve from a spline's control-point or fit-point data.
fn spline_curve(spline: &Spline) -> Option<NurbsCurve3> {
    let stored_degree = usize::try_from(spline.degree.max(1)).ok()?;
    let uses_fit_method =
        !spline.fit_points.is_empty() && spline.control_points.len() <= stored_degree;
    if uses_fit_method {
        return fit_spline_curve(spline);
    }

    let count = spline.control_points.len();
    if count < 2 {
        return None;
    }
    let degree = stored_degree.min(count - 1);
    if degree == 0 || degree > MAX_NURBS_DEGREE || count > MAX_CURVE_POINTS {
        return None;
    }
    let control_points = spline
        .control_points
        .iter()
        .copied()
        .map(array)
        .collect::<Vec<_>>();
    if control_points
        .iter()
        .flatten()
        .any(|component| !component.is_finite())
    {
        return None;
    }
    let weights = if spline.weights.len() == count
        && spline
            .weights
            .iter()
            .all(|weight| weight.is_finite() && *weight > 0.0)
    {
        spline.weights.clone()
    } else {
        vec![1.0; count]
    };
    let expected_knots = count.checked_add(degree)?.checked_add(1)?;
    let knots_are_valid = spline.knots.len() == expected_knots
        && spline.knots.iter().all(|knot| knot.is_finite())
        && spline.knots.windows(2).all(|pair| pair[0] <= pair[1])
        && spline.knots.first() < spline.knots.last();
    let knots = if knots_are_valid {
        spline.knots.clone()
    } else {
        clamped_uniform_knots(degree, count)
    };
    NurbsCurve3::new_strict(degree, control_points, knots, weights)
        .map(|curve| curve.with_periodicity(spline.flags.closed || spline.flags.periodic))
}

/// Interpolates a NURBS curve through a spline's fit points and tangents.
fn fit_spline_curve(spline: &Spline) -> Option<NurbsCurve3> {
    if spline.fit_points.len() < 2 || spline.fit_points.len() > MAX_CURVE_POINTS / 3 {
        return None;
    }
    let mut points = spline
        .fit_points
        .iter()
        .copied()
        .map(array)
        .collect::<Vec<_>>();
    if points
        .iter()
        .flatten()
        .any(|component| !component.is_finite())
    {
        return None;
    }
    let parameterization = match spline.knot_parameterization {
        1 => Parameterization::Centripetal,
        2 => Parameterization::Uniform,
        _ => Parameterization::Chord,
    };
    if spline.flags.periodic {
        return NurbsCurve3::interpolate_periodic(&points, parameterization);
    }
    if spline.flags.closed && points.first() != points.last() {
        points.push(*points.first()?);
    }
    let tangent = |value: Vector3| {
        (finite_vector(value) && value.length_squared() > 1.0e-18).then_some(array(value))
    };
    NurbsCurve3::interpolate_fit(
        &points,
        tangent(spline.begin_tangent),
        tangent(spline.end_tangent),
        parameterization,
    )
    .map(|curve| curve.with_periodicity(spline.flags.closed))
}

/// Reconstructs a NURBS helix when no usable embedded spline is available.
fn generated_helix_curve(helix: &Helix) -> Option<NurbsCurve3> {
    let axis = finite_unit(helix.axis_vector)?;
    let tangent = finite_unit(helix.spline.begin_tangent).or_else(|| perpendicular(axis))?;
    let (axis, radial, base_radius) = HelixCurve::frame_from_points(
        array(helix.axis_base_point),
        array(axis),
        array(helix.start_point),
        array(tangent),
    )?;
    HelixCurve {
        base_center: array(helix.axis_base_point),
        axis_direction: axis,
        start_direction: radial,
        base_radius,
        top_radius: helix.radius,
        height: helix.turns * helix.turn_height,
        turns: helix.turns,
        direction: if helix.handedness {
            HelixDirection::CounterClockwise
        } else {
            HelixDirection::Clockwise
        },
    }
    .nurbs()
}

/// Finds a finite unit vector perpendicular to the supplied direction.
fn perpendicular(direction: Vector3) -> Option<Vector3> {
    let seed = if direction.x.abs() <= direction.y.abs() && direction.x.abs() <= direction.z.abs() {
        Vector3::UNIT_X
    } else if direction.y.abs() <= direction.z.abs() {
        Vector3::UNIT_Y
    } else {
        Vector3::UNIT_Z
    };
    finite_unit(direction.cross(&seed))
}

/// Adaptively samples a NURBS curve across its knot spans.
fn sample_nurbs(
    curve: &NurbsCurve3,
    angular_deflection: f64,
    linear_deflection: f64,
) -> Option<Vec<Vector3>> {
    let (start, end) = curve.domain();
    if !start.is_finite() || !end.is_finite() || end <= start {
        return None;
    }
    let angular = if angular_deflection.is_finite() && angular_deflection > 0.0 {
        angular_deflection.min(std::f64::consts::PI)
    } else {
        10.0_f64.to_radians()
    };
    let linear = if linear_deflection.is_finite() && linear_deflection > 0.0 {
        linear_deflection
    } else {
        1.0e-3
    };
    let mut boundaries = vec![start];
    for knot in curve.knots().iter().copied() {
        if knot > start && knot < end && boundaries.last().copied() != Some(knot) {
            boundaries.push(knot);
        }
    }
    boundaries.push(end);

    let first = vector(curve.point_at_knot(start))?;
    let mut output = vec![first];
    for pair in boundaries.windows(2) {
        let from = pair[0];
        let to = pair[1];
        let from_point = output.last().copied()?;
        let to_point = vector(curve.point_at_knot(to))?;
        refine_nurbs(
            curve,
            from,
            from_point,
            to,
            to_point,
            angular,
            linear,
            0,
            &mut output,
        )?;
    }
    Some(output)
}

#[allow(clippy::too_many_arguments)]
/// Recursively subdivides a NURBS interval until chord and angle tolerances pass.
fn refine_nurbs(
    curve: &NurbsCurve3,
    from: f64,
    from_point: Vector3,
    to: f64,
    to_point: Vector3,
    angular: f64,
    linear: f64,
    depth: usize,
    output: &mut Vec<Vector3>,
) -> Option<()> {
    let width = to - from;
    let samples =
        [0.25, 0.5, 0.75].map(|fraction| vector(curve.point_at_knot(from + width * fraction)));
    let [quarter, middle, three_quarters] = samples;
    let quarter = quarter?;
    let middle = middle?;
    let three_quarters = three_quarters?;
    let deviation = [quarter, middle, three_quarters]
        .into_iter()
        .map(|point| distance_to_segment(point, from_point, to_point))
        .fold(0.0, f64::max);
    let directions = [
        quarter - from_point,
        middle - quarter,
        three_quarters - middle,
        to_point - three_quarters,
    ];
    let turning = directions
        .windows(2)
        .map(|pair| direction_angle(pair[0], pair[1]))
        .fold(0.0, f64::max);
    let split = depth < 2 || deviation > linear || turning > angular;
    if split && depth < MAX_CURVE_REFINEMENT_DEPTH {
        refine_nurbs(
            curve,
            from,
            from_point,
            0.5 * (from + to),
            middle,
            angular,
            linear,
            depth + 1,
            output,
        )?;
        refine_nurbs(
            curve,
            0.5 * (from + to),
            middle,
            to,
            to_point,
            angular,
            linear,
            depth + 1,
            output,
        )?;
    } else {
        if output.len() >= MAX_CURVE_POINTS {
            return None;
        }
        output.push(to_point);
    }
    Some(())
}

/// Computes the shortest distance from a point to a finite line segment.
fn distance_to_segment(point: Vector3, start: Vector3, end: Vector3) -> f64 {
    let delta = end - start;
    let length_squared = delta.length_squared();
    if !length_squared.is_finite() || length_squared <= f64::EPSILON {
        return point.distance(&start);
    }
    let parameter = ((point - start).dot(&delta) / length_squared).clamp(0.0, 1.0);
    point.distance(&(start + delta * parameter))
}

/// Returns the unsigned angle between two directions, handling zero lengths safely.
fn direction_angle(first: Vector3, second: Vector3) -> f64 {
    let denominator = first.length() * second.length();
    if !denominator.is_finite() || denominator <= f64::EPSILON {
        return 0.0;
    }
    (first.dot(&second) / denominator).clamp(-1.0, 1.0).acos()
}

/// Converts a finite coordinate array into a CAD vector.
fn vector(point: [f64; 3]) -> Option<Vector3> {
    let value = Vector3::new(point[0], point[1], point[2]);
    finite_vector(value).then_some(value)
}

/// Normalizes a finite, non-zero vector or rejects it when normalization is unsafe.
fn finite_unit(value: Vector3) -> Option<Vector3> {
    if !finite_vector(value) {
        return None;
    }
    let length = value.length();
    (length.is_finite() && length > 1.0e-12).then(|| value / length)
}

/// Reports whether every component of a vector is finite.
fn finite_vector(value: Vector3) -> bool {
    value.x.is_finite() && value.y.is_finite() && value.z.is_finite()
}

/// Appends a 3D face's triangulation and its visible boundary edges.
fn append_face3d(output: &mut Geometry, face: &Face3D) {
    let corners = face.corners();
    append_planar_face(output, &corners);
    let mut visible = vec![
        !face.invisible_edges.is_first_invisible(),
        !face.invisible_edges.is_second_invisible(),
        !face.invisible_edges.is_third_invisible(),
        !face.invisible_edges.is_fourth_invisible(),
    ];
    visible.truncate(corners.len());
    push_face_edges(&mut output.line_positions, &corners, &visible);
}

/// Transforms and appends a SOLID entity as a planar face with outline wires.
fn append_solid(output: &mut Geometry, solid: &Solid) {
    let basis = Matrix3::arbitrary_axis(solid.normal);
    let corners = solid
        .boundary_corners()
        .into_iter()
        .map(|point| basis * point)
        .collect::<Vec<_>>();
    append_planar_face(output, &corners);
    push_polyline(&mut output.line_positions, &corners, true);
}

/// Appends every valid face and outline from a native mesh entity.
fn append_mesh(output: &mut Geometry, mesh: &Mesh) {
    for face in &mesh.faces {
        let points = face
            .vertices
            .iter()
            .filter_map(|index| mesh.vertices.get(*index).copied())
            .collect::<Vec<_>>();
        if points.len() != face.vertices.len() {
            output.invalid_faces += 1;
            continue;
        }
        append_planar_face(output, &points);
        push_polyline(&mut output.line_positions, &points, true);
    }
}

/// Appends polyface faces while preserving their per-edge visibility flags.
fn append_polyface_mesh(output: &mut Geometry, mesh: &PolyfaceMesh) {
    for face in &mesh.faces {
        let points = mesh.face_vertices(face);
        if points.len() != face.vertex_count() {
            output.invalid_faces += 1;
            continue;
        }
        append_planar_face(output, &points);
        let visible = if face.is_triangle() {
            vec![
                !face.is_edge1_invisible(),
                !face.is_edge2_invisible(),
                !face.is_edge3_invisible(),
            ]
        } else {
            vec![
                !face.is_edge1_invisible(),
                !face.is_edge2_invisible(),
                !face.is_edge3_invisible(),
                !face.is_edge4_invisible(),
            ]
        };
        push_face_edges(&mut output.line_positions, &points, &visible);
    }
}

/// Expands a polygon mesh grid into triangulated cells and closed edge wires.
fn append_polygon_mesh(output: &mut Geometry, mesh: &PolygonMeshEntity) {
    let m = usize::try_from(mesh.m_vertex_count).unwrap_or(0);
    let n = usize::try_from(mesh.n_vertex_count).unwrap_or(0);
    if m < 2 || n < 2 || mesh.vertices.len() < m.saturating_mul(n) {
        output.invalid_faces += 1;
        return;
    }
    let m_cells = if mesh.is_closed_m() { m } else { m - 1 };
    let n_cells = if mesh.is_closed_n() { n } else { n - 1 };
    for mi in 0..m_cells {
        for ni in 0..n_cells {
            let next_m = (mi + 1) % m;
            let next_n = (ni + 1) % n;
            let index = |m_index: usize, n_index: usize| m_index * n + n_index;
            let points = [
                mesh.vertices[index(mi, ni)].location,
                mesh.vertices[index(next_m, ni)].location,
                mesh.vertices[index(next_m, next_n)].location,
                mesh.vertices[index(mi, next_n)].location,
            ];
            append_planar_face(output, &points);
            push_polyline(&mut output.line_positions, &points, true);
        }
    }
}

/// Triangulates a finite planar polygon as a fan with a shared face normal.
fn append_planar_face(output: &mut Geometry, points: &[Vector3]) {
    if points.len() < 3 {
        output.degenerate_faces += 1;
        return;
    }
    if !points
        .iter()
        .all(|point| point.x.is_finite() && point.y.is_finite() && point.z.is_finite())
    {
        output.invalid_faces += 1;
        return;
    }
    let Some(normal) = face_normal(points) else {
        output.degenerate_faces += 1;
        return;
    };
    let Ok(base) = u32::try_from(output.positions.len()) else {
        output.invalid_faces += 1;
        return;
    };
    if u32::try_from(points.len()).is_err() || base.checked_add(points.len() as u32).is_none() {
        output.invalid_faces += 1;
        return;
    }
    output.positions.extend(points.iter().copied().map(array));
    output
        .normals
        .extend(std::iter::repeat_n(array(normal), points.len()));
    for index in 1..points.len() - 1 {
        output
            .triangles
            .push([base, base + index as u32, base + index as u32 + 1]);
    }
}

/// Finds the first stable non-zero normal for a polygon's vertices.
fn face_normal(points: &[Vector3]) -> Option<Vector3> {
    let origin = *points.first()?;
    for pair in points[1..].windows(2) {
        let normal = (pair[0] - origin).cross(&(pair[1] - origin));
        let length = normal.length();
        if length.is_finite() && length > 1.0e-14 {
            return Some(normal / length);
        }
    }
    None
}

/// Emits only the visible boundary segments of a face.
fn push_face_edges(output: &mut Vec<[f64; 3]>, points: &[Vector3], visible: &[bool]) {
    if points.len() < 2 {
        return;
    }
    for index in 0..points.len() {
        if visible.get(index).copied().unwrap_or(true) {
            push_segment(output, points[index], points[(index + 1) % points.len()]);
        }
    }
}

/// Emits consecutive polyline segments and an optional closing segment.
fn push_polyline(output: &mut Vec<[f64; 3]>, points: &[Vector3], closed: bool) {
    for pair in points.windows(2) {
        push_segment(output, pair[0], pair[1]);
    }
    if closed && points.len() > 2 {
        push_segment(output, *points.last().unwrap(), points[0]);
    }
}

/// Adds a finite, non-zero-length line segment to the output buffer.
fn push_segment(output: &mut Vec<[f64; 3]>, from: Vector3, to: Vector3) {
    if finite_vector(from) && finite_vector(to) && from != to {
        output.push(array(from));
        output.push(array(to));
    }
}

/// Converts a CAD vector into the coordinate-array representation used by geometry buffers.
fn array(point: Vector3) -> [f64; 3] {
    [point.x, point.y, point.z]
}

/// Chooses a curve segment angle that satisfies angular and chord tolerances.
fn segment_angle(radius: f64, angular: f64, linear: f64) -> f64 {
    let angular = if angular.is_finite() && angular > 0.0 {
        angular.min(TAU)
    } else {
        10.0_f64.to_radians()
    };
    if !radius.is_finite() || radius <= f64::EPSILON || !linear.is_finite() || linear <= 0.0 {
        return angular;
    }
    let ratio = (1.0 - linear / radius).clamp(-1.0, 1.0);
    let chordal = 2.0 * ratio.acos();
    if chordal.is_finite() && chordal > 1.0e-8 {
        angular.min(chordal)
    } else {
        angular
    }
}

#[allow(clippy::too_many_arguments)]
/// Samples a parametric curve while enforcing point and allocation limits.
fn sample_parametric<F>(
    start: f64,
    sweep: f64,
    radius: f64,
    angular: f64,
    linear: f64,
    closed: bool,
    maximum_points: usize,
    point: F,
) -> Result<Vec<Vector3>, ResourceLimitError>
where
    F: Fn(f64) -> Vector3,
{
    if !start.is_finite() || !sweep.is_finite() || sweep.abs() <= f64::EPSILON {
        return Ok(Vec::new());
    }
    let step = segment_angle(radius.abs(), angular, linear);
    let minimum = if closed { 8 } else { 1 };
    let segments = ((sweep.abs() / step).ceil() as usize).clamp(minimum, 65_536);
    let point_count = segments.saturating_add(1);
    if point_count > maximum_points {
        return Err(ResourceLimitError::new(
            "native curve sample points",
            point_count as u64,
            maximum_points as u64,
        ));
    }
    let mut output = Vec::new();
    output.try_reserve_exact(point_count).map_err(|_| {
        ResourceLimitError::allocation("native curve sample points", maximum_points as u64)
    })?;
    for index in 0..=segments {
        output.push(point(start + sweep * index as f64 / segments as f64));
    }
    Ok(output)
}

/// Expand bulge segments in the entity's two-dimensional OCS. The returned
/// polyline already contains its closing segment when `closed` is true.
fn sample_bulged_polyline(
    vertices: &[([f64; 2], f64)],
    closed: bool,
    angular: f64,
    linear: f64,
    maximum_points: usize,
) -> Result<Vec<[f64; 2]>, ResourceLimitError> {
    if vertices.is_empty() {
        return Ok(Vec::new());
    }
    let segment_count = if closed {
        vertices.len()
    } else {
        vertices.len().saturating_sub(1)
    };
    if maximum_points == 0 {
        return Err(ResourceLimitError::new("native curve sample points", 1, 0));
    }
    let mut output = Vec::new();
    output
        .try_reserve_exact(vertices.len().min(maximum_points))
        .map_err(|_| {
            ResourceLimitError::allocation("native curve sample points", maximum_points as u64)
        })?;
    output.push(vertices[0].0);
    for index in 0..segment_count {
        let (from, bulge) = vertices[index];
        let to = vertices[(index + 1) % vertices.len()].0;
        if bulge.abs() <= 1.0e-12 {
            ensure_curve_growth(&output, 1, maximum_points)?;
            output.push(to);
            continue;
        }
        let dx = to[0] - from[0];
        let dy = to[1] - from[1];
        let chord = dx.hypot(dy);
        if !chord.is_finite() || chord <= f64::EPSILON {
            continue;
        }
        let center_offset = chord * (1.0 - bulge * bulge) / (4.0 * bulge);
        let center = [
            (from[0] + to[0]) * 0.5 - dy / chord * center_offset,
            (from[1] + to[1]) * 0.5 + dx / chord * center_offset,
        ];
        let radius = chord * (1.0 + bulge * bulge) / (4.0 * bulge.abs());
        let sweep = 4.0 * bulge.atan();
        let start = (from[1] - center[1]).atan2(from[0] - center[0]);
        let step = segment_angle(radius, angular, linear);
        let samples = ((sweep.abs() / step).ceil() as usize).clamp(1, 65_536);
        ensure_curve_growth(&output, samples, maximum_points)?;
        output.try_reserve(samples).map_err(|_| {
            ResourceLimitError::allocation("native curve sample points", maximum_points as u64)
        })?;
        for sample in 1..=samples {
            let angle = start + sweep * sample as f64 / samples as f64;
            output.push([
                center[0] + radius * angle.cos(),
                center[1] + radius * angle.sin(),
            ]);
        }
    }
    Ok(output)
}

/// Ensures that extending a sampled curve will not exceed its point limit.
fn ensure_curve_growth(
    output: &[[f64; 2]],
    additional: usize,
    maximum_points: usize,
) -> Result<(), ResourceLimitError> {
    let attempted = output.len().saturating_add(additional);
    if attempted > maximum_points {
        return Err(ResourceLimitError::new(
            "native curve sample points",
            u64::try_from(attempted).unwrap_or(u64::MAX),
            maximum_points as u64,
        ));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Creates effectively unlimited geometry capacity for focused unit tests.
    fn test_capacity() -> GeometryCapacity {
        GeometryCapacity {
            position_elements: u64::MAX,
            triangles: u64::MAX,
            line_segments: u64::MAX,
        }
    }

    /// Verifies that a positive bulge produces a semicircular chord expansion.
    #[test]
    fn positive_bulge_reaches_left_side_of_chord() {
        let points = sample_bulged_polyline(
            &[([0.0, 0.0], 1.0), ([2.0, 0.0], 0.0)],
            false,
            10.0_f64.to_radians(),
            0.01,
            MAX_CURVE_POINTS,
        )
        .unwrap();
        assert!(points.iter().any(|point| point[1] < -0.9 || point[1] > 0.9));
        assert_eq!(points.first().copied(), Some([0.0, 0.0]));
        let last = points.last().copied().unwrap();
        assert!((last[0] - 2.0).abs() < 1.0e-10 && last[1].abs() < 1.0e-10);
    }

    /// Verifies that degenerate faces and malformed coordinates are counted separately.
    #[test]
    fn distinguishes_zero_area_faces_from_malformed_coordinates() {
        let mut geometry = Geometry::default();
        append_planar_face(
            &mut geometry,
            &[
                Vector3::ZERO,
                Vector3::new(1.0, 0.0, 0.0),
                Vector3::new(2.0, 0.0, 0.0),
            ],
        );
        append_planar_face(
            &mut geometry,
            &[
                Vector3::ZERO,
                Vector3::new(f64::NAN, 0.0, 0.0),
                Vector3::new(0.0, 1.0, 0.0),
            ],
        );

        assert_eq!(geometry.degenerate_faces, 1);
        assert_eq!(geometry.invalid_faces, 1);
        assert!(geometry.triangles.is_empty());
    }

    /// Verifies fallback reference spans and valid extent diagonals.
    #[test]
    fn invalid_extents_use_a_tolerance_scaled_reference_span() {
        let span = reference_span_from_extents(
            Vector3::new(1.0e20, 1.0e20, 1.0e20),
            Vector3::new(-1.0e20, -1.0e20, -1.0e20),
            0.5,
        );
        assert_eq!(span, 500.0);
        let span = reference_span_from_extents(Vector3::ZERO, Vector3::new(3.0, 4.0, 0.0), 0.1);
        assert_eq!(span, 5.0);
    }

    /// Verifies that points, rays, and construction lines emit finite wire geometry.
    #[test]
    fn points_rays_and_xlines_emit_finite_reference_wires() {
        let point = EntityType::Point(Point::at(Vector3::new(2.0, 3.0, 4.0)));
        let point_geometry = extract(&point, 0.1, 0.01, 20.0, test_capacity())
            .unwrap()
            .unwrap();
        assert_eq!(point_geometry.line_positions.len(), 6);
        assert!(point_geometry
            .line_positions
            .iter()
            .flatten()
            .all(|value| value.is_finite()));

        let ray = EntityType::Ray(opencadcodec::entities::Ray::new(
            Vector3::new(1.0, 2.0, 3.0),
            Vector3::new(2.0, 0.0, 0.0),
        ));
        let ray_geometry = extract(&ray, 0.1, 0.01, 20.0, test_capacity())
            .unwrap()
            .unwrap();
        assert_eq!(
            ray_geometry.line_positions,
            vec![[1.0, 2.0, 3.0], [21.0, 2.0, 3.0]]
        );

        let xline = EntityType::XLine(opencadcodec::entities::XLine::new(
            Vector3::new(1.0, 2.0, 3.0),
            Vector3::new(0.0, 4.0, 0.0),
        ));
        let xline_geometry = extract(&xline, 0.1, 0.01, 20.0, test_capacity())
            .unwrap()
            .unwrap();
        assert_eq!(
            xline_geometry.line_positions,
            vec![[1.0, -18.0, 3.0], [1.0, 22.0, 3.0]]
        );
    }

    /// Verifies that rational control splines and fit splines are sampled correctly.
    #[test]
    fn rational_and_fit_splines_are_sampled_as_curves() {
        let mut rational = Spline::from_control_points(
            2,
            vec![
                Vector3::new(1.0, 0.0, 0.0),
                Vector3::new(1.0, 1.0, 0.0),
                Vector3::new(0.0, 1.0, 0.0),
            ],
        );
        rational.weights = vec![1.0, std::f64::consts::FRAC_1_SQRT_2, 1.0];
        rational.flags.rational = true;
        let rational_geometry = extract(
            &EntityType::Spline(rational),
            5.0_f64.to_radians(),
            1.0e-4,
            10.0,
            test_capacity(),
        )
        .unwrap()
        .unwrap();
        assert!(rational_geometry.line_positions.len() > 16);
        assert_eq!(
            rational_geometry.line_positions.first(),
            Some(&[1.0, 0.0, 0.0])
        );
        assert_eq!(
            rational_geometry.line_positions.last(),
            Some(&[0.0, 1.0, 0.0])
        );

        let fit = Spline::from_fit_points(vec![
            Vector3::new(0.0, 0.0, 0.0),
            Vector3::new(1.0, 2.0, 1.0),
            Vector3::new(3.0, 0.0, 2.0),
        ]);
        let fit_geometry = extract(
            &EntityType::Spline(fit),
            5.0_f64.to_radians(),
            1.0e-3,
            10.0,
            test_capacity(),
        )
        .unwrap()
        .unwrap();
        assert!(fit_geometry.line_positions.len() > 4);
        assert_eq!(fit_geometry.line_positions.first(), Some(&[0.0, 0.0, 0.0]));
        assert_eq!(fit_geometry.line_positions.last(), Some(&[3.0, 0.0, 2.0]));
    }

    /// Verifies that helix parameters generate geometry without an embedded spline.
    #[test]
    fn helix_without_embedded_spline_uses_its_generating_parameters() {
        let mut helix = Helix::new();
        helix.axis_base_point = Vector3::ZERO;
        helix.start_point = Vector3::UNIT_X;
        helix.axis_vector = Vector3::UNIT_Z;
        helix.radius = 1.0;
        helix.turns = 1.0;
        helix.turn_height = 2.0;
        helix.handedness = true;
        let geometry = extract(
            &EntityType::Helix(Box::new(helix)),
            5.0_f64.to_radians(),
            1.0e-3,
            10.0,
            test_capacity(),
        )
        .unwrap()
        .unwrap();
        assert!(geometry.line_positions.len() > 16);
        let first = geometry.line_positions.first().unwrap();
        let last = geometry.line_positions.last().unwrap();
        assert!((first[0] - 1.0).abs() < 1.0e-12 && first[1].abs() < 1.0e-12);
        assert!((last[0] - 1.0).abs() < 1.0e-9 && last[1].abs() < 1.0e-9);
        assert!((last[2] - 2.0).abs() < 1.0e-9);
    }

    /// Verifies that reference geometry observes position and segment limits.
    #[test]
    fn reference_geometry_respects_position_and_segment_limits() {
        let point = EntityType::Point(Point::at(Vector3::ZERO));
        let error = extract(
            &point,
            0.1,
            0.01,
            20.0,
            GeometryCapacity {
                position_elements: 5,
                triangles: u64::MAX,
                line_segments: u64::MAX,
            },
        )
        .unwrap_err();

        assert!(error.to_string().contains("POSITION elements"));
    }
}
