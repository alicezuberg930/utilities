use std::env;
use std::path::PathBuf;

use crate::limits::ResourceLimits;

#[derive(Debug)]
pub struct Cli {
    pub input: PathBuf,
    pub output: PathBuf,
    pub format: String,
    pub linear_deflection_meters: f64,
    pub angular_deflection_degrees: f64,
    pub include_hidden: bool,
    pub export_edge_wires: bool,
    pub require_complete_geometry: bool,
    pub limits: ResourceLimits,
}

impl Cli {
    /// Parses command-line arguments into converter settings or handles a
    /// help/version request without starting a conversion.
    pub fn parse_env() -> Result<Option<Self>, String> {
        let mut input = None;
        let mut output = None;
        let mut format = None;
        let mut linear_deflection_meters = 0.0005;
        let mut angular_deflection_degrees = 10.0;
        let mut include_hidden = false;
        let mut export_edge_wires = false;
        let mut require_complete_geometry = false;
        let mut limits = ResourceLimits::default();
        let mut arguments = env::args().skip(1);

        while let Some(argument) = arguments.next() {
            match argument.as_str() {
                "--input" => input = Some(PathBuf::from(next_value(&mut arguments, "--input")?)),
                "--output" => output = Some(PathBuf::from(next_value(&mut arguments, "--output")?)),
                "--format" => format = Some(next_value(&mut arguments, "--format")?),
                "--linear-deflection-meters" => {
                    linear_deflection_meters = parse_number(
                        next_value(&mut arguments, "--linear-deflection-meters")?,
                        "--linear-deflection-meters",
                    )?;
                }
                "--angular-deflection-degrees" => {
                    angular_deflection_degrees = parse_number(
                        next_value(&mut arguments, "--angular-deflection-degrees")?,
                        "--angular-deflection-degrees",
                    )?;
                }
                "--include-hidden" => include_hidden = true,
                "--export-edge-wires" => export_edge_wires = true,
                "--require-complete-geometry" => require_complete_geometry = true,
                "--max-input-bytes" => {
                    limits.input_bytes = parse_count(
                        next_value(&mut arguments, "--max-input-bytes")?,
                        "--max-input-bytes",
                    )?;
                }
                "--max-entities" => {
                    limits.entities = parse_count(
                        next_value(&mut arguments, "--max-entities")?,
                        "--max-entities",
                    )?;
                }
                "--max-vertices" => {
                    limits.position_elements = parse_count(
                        next_value(&mut arguments, "--max-vertices")?,
                        "--max-vertices",
                    )?;
                }
                "--max-triangles" => {
                    limits.triangles = parse_count(
                        next_value(&mut arguments, "--max-triangles")?,
                        "--max-triangles",
                    )?;
                }
                "--max-line-segments" => {
                    limits.line_segments = parse_count(
                        next_value(&mut arguments, "--max-line-segments")?,
                        "--max-line-segments",
                    )?;
                }
                "--max-output-bytes" => {
                    limits.output_bytes = parse_count(
                        next_value(&mut arguments, "--max-output-bytes")?,
                        "--max-output-bytes",
                    )?;
                }
                "--help" | "-h" => {
                    println!("{}", usage());
                    return Ok(None);
                }
                "--version" | "-V" => {
                    println!("utilities-cad-converter {}", env!("CARGO_PKG_VERSION"));
                    return Ok(None);
                }
                unknown => return Err(format!("unknown argument '{unknown}'\n{}", usage())),
            }
        }

        Ok(Some(Self {
            input: input.ok_or_else(|| format!("--input is required\n{}", usage()))?,
            output: output.ok_or_else(|| format!("--output is required\n{}", usage()))?,
            format: format.ok_or_else(|| format!("--format is required\n{}", usage()))?,
            linear_deflection_meters,
            angular_deflection_degrees,
            include_hidden,
            export_edge_wires,
            require_complete_geometry,
            limits,
        }))
    }
}

/// Returns the value following an option and rejects a missing value or the
/// next option token.
fn next_value(
    arguments: &mut impl Iterator<Item = String>,
    option: &str,
) -> Result<String, String> {
    arguments
        .next()
        .filter(|value| !value.starts_with("--"))
        .ok_or_else(|| format!("{option} requires a value"))
}

/// Parses a floating-point command-line value and reports the owning option
/// in validation errors.
fn parse_number(value: String, option: &str) -> Result<f64, String> {
    value
        .parse::<f64>()
        .map_err(|_| format!("{option} requires a number, got '{value}'"))
}

/// Parses a non-negative integer resource limit and reports an option-specific
/// error when the value is invalid.
fn parse_count(value: String, option: &str) -> Result<u64, String> {
    value
        .parse::<u64>()
        .map_err(|_| format!("{option} requires a non-negative integer, got '{value}'"))
}

/// Returns the complete command-line usage string displayed for help and
/// argument errors.
fn usage() -> &'static str {
    "Usage: utilities-cad-converter --input <path> --output <path> --format <dwg|dxf|dws|dwt> [--linear-deflection-meters <number>] [--angular-deflection-degrees <number>] [--include-hidden] [--export-edge-wires] [--require-complete-geometry] [--max-input-bytes <integer>] [--max-entities <integer>] [--max-vertices <integer>] [--max-triangles <integer>] [--max-line-segments <integer>] [--max-output-bytes <integer>]"
}
