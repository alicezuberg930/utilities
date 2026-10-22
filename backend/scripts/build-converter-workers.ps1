[CmdletBinding()]
param(
    [ValidateSet("All", "Cad", "PdfDocx")]
    [string]$Worker = "All"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Executable,
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
        [string]$FailureMessage
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)."
    }
}

function Test-SupportedPython {
    param(
        [Parameter(Mandatory)]
        [string]$Executable,
        [string[]]$PrefixArguments = @()
    )

    try {
        & $Executable @PrefixArguments -c "import sys; raise SystemExit(0 if sys.version_info >= (3, 10) and sys.version_info.releaselevel == 'final' else 1)" 2>$null |
            Out-Null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Find-PythonLauncher {
    foreach ($commandName in @("python3", "python")) {
        $commands = @(Get-Command $commandName -All -CommandType Application -ErrorAction SilentlyContinue)
        foreach ($command in $commands) {
            if (Test-SupportedPython -Executable $command.Source) {
                return [pscustomobject]@{
                    Executable = $command.Source
                    PrefixArguments = @()
                }
            }
        }
    }

    if ($isWindows) {
        $launchers = @(Get-Command py.exe -All -CommandType Application -ErrorAction SilentlyContinue)
        foreach ($launcher in $launchers) {
            foreach ($versionArgument in @("-3.13", "-3.12", "-3.11", "-3.10", "-3")) {
                if (Test-SupportedPython `
                    -Executable $launcher.Source `
                    -PrefixArguments @($versionArgument)) {
                    return [pscustomobject]@{
                        Executable = $launcher.Source
                        PrefixArguments = @($versionArgument)
                    }
                }
            }
        }
    }

    throw @"
Python 3.10 or newer was not found. Install Python from https://www.python.org/downloads/,
open a new terminal, and run this script again.
"@
}

function Build-CadWorker {
    $cargo = Get-Command cargo -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $cargo) {
        throw @"
Cargo was not found on PATH. Install the stable Rust toolchain from https://rustup.rs,
open a new terminal, and run this script again.
"@
    }

    $workerRoot = Join-Path $backendRoot "vendor\cad-converter-worker"
    $targetDirectory = Join-Path $workerRoot "target"
    $executableName = if ($isWindows) {
        "utilities-cad-converter.exe"
    } else {
        "utilities-cad-converter"
    }
    $artifactPath = Join-Path $targetDirectory "release\$executableName"

    Write-Host "Building CAD converter worker..."
    Invoke-CheckedCommand `
        -Executable $cargo.Source `
        -Arguments @(
            "build",
            "--release",
            "--locked",
            "--manifest-path",
            (Join-Path $workerRoot "Cargo.toml"),
            "--target-dir",
            $targetDirectory
        ) `
        -FailureMessage "Cargo failed to build the CAD converter worker"

    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Cargo completed without producing the expected worker executable: $artifactPath"
    }

    Write-Host "Ready: $artifactPath"
}

function Build-PdfDocxWorker {
    $workerRoot = Join-Path $backendRoot "vendor\pdf-docx-converter-worker"
    $virtualEnvironment = Join-Path $workerRoot ".venv"
    $virtualEnvironmentPython = if ($isWindows) {
        Join-Path $virtualEnvironment "Scripts\python.exe"
    } else {
        Join-Path $virtualEnvironment "bin\python"
    }

    if ((Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf) -and
        -not (Test-SupportedPython -Executable $virtualEnvironmentPython)) {
        $resolvedWorkerRoot = [System.IO.Path]::GetFullPath($workerRoot)
        $resolvedVirtualEnvironment = [System.IO.Path]::GetFullPath($virtualEnvironment)
        if (-not $resolvedVirtualEnvironment.StartsWith(
            $resolvedWorkerRoot + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to replace a virtual environment outside the worker directory."
        }

        Write-Host "Replacing unsupported PDF to DOCX Python environment..."
        Remove-Item -LiteralPath $resolvedVirtualEnvironment -Recurse -Force
    }

    if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf)) {
        $launcher = Find-PythonLauncher
        Write-Host "Creating PDF to DOCX worker virtual environment..."
        Invoke-CheckedCommand `
            -Executable $launcher.Executable `
            -Arguments (@($launcher.PrefixArguments) + @("-m", "venv", $virtualEnvironment)) `
            -FailureMessage "Python failed to create the PDF to DOCX virtual environment"
    }

    Write-Host "Removing any previously installed pdf2docx wheel..."
    Invoke-CheckedCommand `
        -Executable $virtualEnvironmentPython `
        -Arguments @("-m", "pip", "uninstall", "--yes", "pdf2docx") `
        -FailureMessage "pip failed to remove the packaged pdf2docx dependency"

    Write-Host "Installing hash-locked PDF to DOCX dependencies..."
    Invoke-CheckedCommand `
        -Executable $virtualEnvironmentPython `
        -Arguments @(
            "-m",
            "pip",
            "install",
            "--disable-pip-version-check",
            "--require-hashes",
            "--requirement",
            (Join-Path $workerRoot "requirements.lock")
        ) `
        -FailureMessage "pip failed to install the PDF to DOCX worker dependencies"

    Write-Host "Testing PDF to DOCX converter worker..."
    Invoke-CheckedCommand `
        -Executable $virtualEnvironmentPython `
        -Arguments @(
            "-m",
            "unittest",
            "discover",
            "-s",
            (Join-Path $workerRoot "tests"),
            "-p",
            "test_*.py"
        ) `
        -FailureMessage "PDF to DOCX worker tests failed"

    Invoke-CheckedCommand `
        -Executable $virtualEnvironmentPython `
        -Arguments @((Join-Path $workerRoot "worker.py"), "--version") `
        -FailureMessage "PDF to DOCX worker could not start"

    Write-Host "Ready: $virtualEnvironmentPython"
    Write-Host "Worker: $(Join-Path $workerRoot 'worker.py')"
}

if ($Worker -in @("All", "Cad")) {
    Build-CadWorker
}

if ($Worker -in @("All", "PdfDocx")) {
    Build-PdfDocxWorker
}
