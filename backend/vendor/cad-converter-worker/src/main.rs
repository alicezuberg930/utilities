mod cli;
mod converter;
mod geometry;
mod glb;
mod limits;
mod scene;

use std::panic::{catch_unwind, AssertUnwindSafe};
use std::process::ExitCode;

use cli::Cli;

/// Parses the CLI, runs one isolated conversion, and maps expected failures or
/// panics to the worker's stable process exit codes.
fn main() -> ExitCode {
    let cli = match Cli::parse_env() {
        Ok(Some(value)) => value,
        Ok(None) => return ExitCode::SUCCESS,
        Err(error) => {
            eprintln!("{error}");
            return ExitCode::from(2);
        }
    };

    match catch_unwind(AssertUnwindSafe(|| converter::convert(&cli))) {
        Ok(Ok(())) => ExitCode::SUCCESS,
        Ok(Err(error)) => {
            eprintln!("{}", error.message);
            ExitCode::from(error.exit_code)
        }
        Err(_) => {
            eprintln!("internal converter failure");
            ExitCode::from(6)
        }
    }
}
