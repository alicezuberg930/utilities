use std::fmt;

pub const DEFAULT_MAX_INPUT_BYTES: u64 = 256 * 1024 * 1024;
pub const DEFAULT_MAX_ENTITIES: u64 = 250_000;
pub const DEFAULT_MAX_POSITION_ELEMENTS: u64 = 5_000_000;
pub const DEFAULT_MAX_TRIANGLES: u64 = 2_000_000;
pub const DEFAULT_MAX_LINE_SEGMENTS: u64 = 2_000_000;
pub const DEFAULT_MAX_OUTPUT_BYTES: u64 = 256 * 1024 * 1024;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ResourceLimits {
    pub input_bytes: u64,
    pub entities: u64,
    pub position_elements: u64,
    pub triangles: u64,
    pub line_segments: u64,
    pub output_bytes: u64,
}

impl Default for ResourceLimits {
    /// Returns the worker's default limits for input, geometry, and output resources.
    fn default() -> Self {
        Self {
            input_bytes: DEFAULT_MAX_INPUT_BYTES,
            entities: DEFAULT_MAX_ENTITIES,
            position_elements: DEFAULT_MAX_POSITION_ELEMENTS,
            triangles: DEFAULT_MAX_TRIANGLES,
            line_segments: DEFAULT_MAX_LINE_SEGMENTS,
            output_bytes: DEFAULT_MAX_OUTPUT_BYTES,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ResourceLimitError {
    resource: &'static str,
    attempted: u64,
    limit: u64,
}

impl ResourceLimitError {
    /// Creates an error describing a resource count that exceeds its configured limit.
    pub fn new(resource: &'static str, attempted: u64, limit: u64) -> Self {
        Self {
            resource,
            attempted,
            limit,
        }
    }

    /// Creates a synthetic over-limit error for a failed memory allocation.
    pub fn allocation(resource: &'static str, limit: u64) -> Self {
        Self::new(resource, limit.saturating_add(1), limit)
    }
}

impl fmt::Display for ResourceLimitError {
    /// Formats the resource name, attempted count, and configured limit for diagnostics.
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "resource limit exceeded: {} would be {} (limit {})",
            self.resource, self.attempted, self.limit
        )
    }
}

#[derive(Debug)]
pub struct ResourceBudget {
    limits: ResourceLimits,
    entities: u64,
    prototype_placements: u64,
    logical_block_instances: u64,
    scene_nodes: u64,
    position_elements: u64,
    triangles: u64,
    line_segments: u64,
}

impl ResourceBudget {
    /// Creates an empty cumulative resource budget with the supplied limits.
    pub fn new(limits: ResourceLimits) -> Self {
        Self {
            limits,
            entities: 0,
            prototype_placements: 0,
            logical_block_instances: 0,
            scene_nodes: 0,
            position_elements: 0,
            triangles: 0,
            line_segments: 0,
        }
    }

    /// Checks a document's entity count against the configured entity limit.
    pub fn ensure_document_entities(&self, count: usize) -> Result<(), ResourceLimitError> {
        ensure_within(
            "document entities",
            u64::try_from(count).unwrap_or(u64::MAX),
            self.limits.entities,
        )
    }

    /// Checks whether additional entity visits can fit without charging them yet.
    pub fn ensure_additional_entities(&self, additional: u64) -> Result<(), ResourceLimitError> {
        ensure_addition(
            "visited/expanded entities",
            self.entities,
            additional,
            self.limits.entities,
        )
    }

    /// Charges one visited or expanded entity to the cumulative budget.
    pub fn charge_entity(&mut self) -> Result<(), ResourceLimitError> {
        self.ensure_additional_entities(1)?;
        self.entities += 1;
        Ok(())
    }

    /// Charges reusable block-prototype placements without treating their
    /// shared geometry as newly processed entities.
    pub fn charge_prototype_placements(
        &mut self,
        additional: u64,
    ) -> Result<(), ResourceLimitError> {
        charge_counter(
            "block prototype placements",
            &mut self.prototype_placements,
            additional,
            self.limits.entities,
        )
    }

    /// Charges logical INSERT and MINSERT occurrences independently from the
    /// number of unique entities compiled into reusable geometry.
    pub fn charge_logical_block_instances(
        &mut self,
        additional: u64,
    ) -> Result<(), ResourceLimitError> {
        charge_counter(
            "logical block instances",
            &mut self.logical_block_instances,
            additional,
            self.limits.entities,
        )
    }

    /// Charges nodes that will be emitted into the GLB scene graph so shared
    /// meshes cannot be paired with an unbounded amount of JSON metadata.
    pub fn charge_scene_nodes(&mut self, additional: u64) -> Result<(), ResourceLimitError> {
        charge_counter(
            "emitted scene nodes",
            &mut self.scene_nodes,
            additional,
            self.limits.entities,
        )
    }

    /// Returns the number of position elements still available in the geometry budget.
    pub fn remaining_position_elements(&self) -> u64 {
        self.limits
            .position_elements
            .saturating_sub(self.position_elements)
    }

    /// Returns the number of triangles still available in the geometry budget.
    pub fn remaining_triangles(&self) -> u64 {
        self.limits.triangles.saturating_sub(self.triangles)
    }

    /// Returns the number of line segments still available in the geometry budget.
    pub fn remaining_line_segments(&self) -> u64 {
        self.limits.line_segments.saturating_sub(self.line_segments)
    }

    /// Validates and cumulatively charges positions, triangles, and line segments.
    pub fn charge_geometry(
        &mut self,
        position_elements: usize,
        triangles: usize,
        line_segments: usize,
    ) -> Result<(), ResourceLimitError> {
        let position_elements = u64::try_from(position_elements).unwrap_or(u64::MAX);
        let triangles = u64::try_from(triangles).unwrap_or(u64::MAX);
        let line_segments = u64::try_from(line_segments).unwrap_or(u64::MAX);
        ensure_addition(
            "POSITION elements",
            self.position_elements,
            position_elements,
            self.limits.position_elements,
        )?;
        ensure_addition(
            "source triangles",
            self.triangles,
            triangles,
            self.limits.triangles,
        )?;
        ensure_addition(
            "line segments",
            self.line_segments,
            line_segments,
            self.limits.line_segments,
        )?;
        self.position_elements += position_elements;
        self.triangles += triangles;
        self.line_segments += line_segments;
        Ok(())
    }
}

/// Checks and applies an increment to one independently bounded resource counter.
fn charge_counter(
    resource: &'static str,
    current: &mut u64,
    additional: u64,
    limit: u64,
) -> Result<(), ResourceLimitError> {
    ensure_addition(resource, *current, additional, limit)?;
    *current += additional;
    Ok(())
}

/// Ensures an attempted resource count does not exceed its configured limit.
pub fn ensure_within(
    resource: &'static str,
    attempted: u64,
    limit: u64,
) -> Result<(), ResourceLimitError> {
    if attempted > limit {
        return Err(ResourceLimitError::new(resource, attempted, limit));
    }
    Ok(())
}

/// Checks a resource increment using overflow-safe addition before enforcing its limit.
pub fn ensure_addition(
    resource: &'static str,
    current: u64,
    additional: u64,
    limit: u64,
) -> Result<(), ResourceLimitError> {
    let Some(attempted) = current.checked_add(additional) else {
        return Err(ResourceLimitError::new(resource, u64::MAX, limit));
    };
    ensure_within(resource, attempted, limit)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    /// Verifies exact budget limits succeed and every subsequent charge is rejected.
    fn budget_accepts_limit_and_rejects_next_element() {
        let mut budget = ResourceBudget::new(ResourceLimits {
            entities: 1,
            position_elements: 3,
            triangles: 1,
            line_segments: 1,
            ..ResourceLimits::default()
        });
        budget.charge_entity().unwrap();
        budget.charge_geometry(3, 1, 1).unwrap();

        assert!(budget.charge_entity().is_err());
        assert!(budget.charge_geometry(1, 0, 0).is_err());
        assert!(budget.charge_geometry(0, 1, 0).is_err());
        assert!(budget.charge_geometry(0, 0, 1).is_err());
    }

    #[test]
    /// Verifies instance and scene-graph counters are independent from unique entities.
    fn instance_counters_have_independent_entity_sized_limits() {
        let mut budget = ResourceBudget::new(ResourceLimits {
            entities: 2,
            ..ResourceLimits::default()
        });

        budget.charge_entity().unwrap();
        budget.charge_entity().unwrap();
        budget.charge_prototype_placements(2).unwrap();
        budget.charge_logical_block_instances(2).unwrap();
        budget.charge_scene_nodes(2).unwrap();

        assert!(budget.charge_entity().is_err());
        assert!(budget.charge_prototype_placements(1).is_err());
        assert!(budget.charge_logical_block_instances(1).is_err());
        assert!(budget.charge_scene_nodes(1).is_err());
    }

    #[test]
    /// Verifies failed multi-item charges leave their counters unchanged.
    fn rejected_instance_charge_does_not_consume_budget() {
        let mut budget = ResourceBudget::new(ResourceLimits {
            entities: 3,
            ..ResourceLimits::default()
        });

        budget.charge_logical_block_instances(2).unwrap();
        assert!(budget.charge_logical_block_instances(2).is_err());
        budget.charge_logical_block_instances(1).unwrap();
        assert!(budget.charge_logical_block_instances(1).is_err());
    }

    #[test]
    /// Verifies an overflowing resource addition is reported as an over-limit attempt.
    fn checked_addition_rejects_overflow() {
        let error = ensure_addition("entities", u64::MAX, 1, u64::MAX).unwrap_err();
        assert_eq!(
            error.to_string(),
            "resource limit exceeded: entities would be 18446744073709551615 (limit 18446744073709551615)"
        );
    }
}
