# Utilities PDF to DOCX Converter

This directory contains the isolated Python worker used by the Utilities
backend to convert PDF documents into editable Office Open XML (`.docx`) files.
It runs out of process so the host can enforce a timeout and treat converter
failures as process failures.

The official `pdf2docx` v0.5.13 repository is vendored at this directory root.
Its original paths and filenames are retained, including the `pdf2docx/`
package, `docs/`, `test/`, `README.md`, `setup.py`, and `version.txt`. See
`VENDORED_FROM.md` for the immutable upstream revision. The Utilities-specific
entry point is `worker.py`; it imports the adjacent source tree directly and
does not install the `pdf2docx` wheel from PyPI.

## Licensing requirement

**Do not deploy this worker until the chosen PyMuPDF licence has been
reviewed.** `pdf2docx` is MIT-licensed, but PyMuPDF and MuPDF are available
under the **GNU AGPL or an Artifex commercial licence**. A deployment that
cannot satisfy the AGPL requirements needs an applicable commercial licence.
Review `THIRD_PARTY_NOTICES.md`, `THIRD_PARTY_LICENSES.txt`, and the upstream
terms before distribution or production network use. This is a technical
notice, not legal advice.

## Local setup

Install Python 3.10 or newer, then run from the repository root on Windows:

```powershell
.\backend\scripts\build-converter-workers.cmd -Worker PdfDocx
```

The script creates `.venv`, removes any previously installed `pdf2docx` wheel,
installs the hash-locked runtime dependencies, and runs the worker tests. The
API automatically discovers `.venv/Scripts/python.exe` and `worker.py`; no
globally installed Python packages are used at runtime.

Regenerate `requirements.lock` whenever `requirements.in` changes:

```text
python -m pip install pip-tools
python -m piptools compile --generate-hashes --strip-extras --no-emit-index-url requirements.in
```

## Command-line contract

```text
python worker.py \
  --input <absolute.pdf> \
  --output <absolute.partial.docx> \
  [--max-input-bytes <integer>] \
  [--max-output-bytes <integer>]
```

Both paths must be absolute. The input must exist and the output must not
exist. Progress is emitted as newline-delimited JSON on stdout with a numeric
`progress` property. Diagnostics and failures go to stderr. Exit codes are:

- `0`: success
- `2`: invalid input path or arguments
- `3`: runtime dependency unavailable
- `4`: PDF read or import failure
- `5`: DOCX conversion or export failure
- `6`: unexpected internal failure
- `7`: deterministic resource limit exceeded

## Resource and fidelity limits

Input and output default to 256 MiB limits. The worker rejects PDFs over 1,000
pages with exit code `7`. It copies the input through a bounded temporary file
and publishes the completed DOCX only after validating its size and OOXML
package structure.

Conversion quality varies with PDF complexity. Text-based PDFs work best;
scanned PDFs require a separate OCR stage. Production output should be reviewed
against representative documents.
