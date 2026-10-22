#!/usr/bin/env python3
"""Isolated PDF-to-DOCX process used by the Utilities API."""

from __future__ import annotations

import argparse
from contextlib import redirect_stdout
import json
import logging
import os
from pathlib import Path
import sys
import tempfile
from typing import NoReturn, Sequence
import zipfile


VERSION = "2.0.0"
DEFAULT_MAX_INPUT_BYTES = 256 * 1024 * 1024
DEFAULT_MAX_OUTPUT_BYTES = 256 * 1024 * 1024
MAX_PAGES = 1_000
COPY_BUFFER_BYTES = 1024 * 1024

EXIT_INVALID_INPUT = 2
EXIT_UNAVAILABLE = 3
EXIT_READ_FAILED = 4
EXIT_WRITE_FAILED = 5
EXIT_INTERNAL_ERROR = 6
EXIT_RESOURCE_LIMIT = 7


class WorkerError(Exception):
    """Expected conversion failure with a stable process exit code."""

    def __init__(self, exit_code: int, message: str) -> None:
        super().__init__(message)
        self.exit_code = exit_code


class OnceAction(argparse.Action):
    """Reject duplicate security-sensitive scalar options."""

    def __call__(
        self,
        parser: argparse.ArgumentParser,
        namespace: argparse.Namespace,
        values: object,
        option_string: str | None = None,
    ) -> None:
        seen_options = getattr(namespace, "_seen_options", set())
        if self.dest in seen_options:
            parser.error(f"{option_string} may only be supplied once")
        seen_options.add(self.dest)
        setattr(namespace, "_seen_options", seen_options)
        setattr(namespace, self.dest, values)


def positive_integer(value: str) -> int:
    try:
        parsed = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("requires an integer") from error
    if parsed <= 0:
        raise argparse.ArgumentTypeError("must be greater than zero")
    return parsed


def create_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="utilities-pdf-docx-converter",
        description="Convert one PDF file into an editable DOCX file.",
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {VERSION}")
    parser.add_argument("--input", action=OnceAction, required=True)
    parser.add_argument("--output", action=OnceAction, required=True)
    parser.add_argument(
        "--max-input-bytes",
        action=OnceAction,
        type=positive_integer,
        default=DEFAULT_MAX_INPUT_BYTES,
    )
    parser.add_argument(
        "--max-output-bytes",
        action=OnceAction,
        type=positive_integer,
        default=DEFAULT_MAX_OUTPUT_BYTES,
    )
    return parser


def emit_progress(progress: int, stage: str, **stats: int) -> None:
    event: dict[str, object] = {"progress": progress, "stage": stage}
    if stats:
        event["stats"] = stats
    print(json.dumps(event, separators=(",", ":")), flush=True)


def fail(exit_code: int, message: str) -> NoReturn:
    raise WorkerError(exit_code, message)


def normalized_path(path: Path) -> str:
    return os.path.normcase(os.path.abspath(path))


def validate_paths(input_path: Path, output_path: Path) -> None:
    if not input_path.is_absolute() or not output_path.is_absolute():
        fail(EXIT_INVALID_INPUT, "--input and --output must be absolute paths")
    if not input_path.is_file():
        fail(EXIT_INVALID_INPUT, f"input file does not exist: {input_path}")
    if normalized_path(input_path) == normalized_path(output_path):
        fail(EXIT_INVALID_INPUT, "input and output paths must be different")
    if output_path.exists():
        fail(EXIT_INVALID_INPUT, f"output path already exists: {output_path}")
    if input_path.suffix.lower() != ".pdf":
        fail(EXIT_INVALID_INPUT, "input path must have a .pdf extension")
    if output_path.suffix.lower() != ".docx":
        fail(EXIT_INVALID_INPUT, "output path must have a .docx extension")


def copy_bounded_input(source: Path, destination: Path, limit: int) -> int:
    total = 0
    try:
        with source.open("rb") as reader, destination.open("xb") as writer:
            while chunk := reader.read(COPY_BUFFER_BYTES):
                total += len(chunk)
                if total > limit:
                    fail(
                        EXIT_RESOURCE_LIMIT,
                        f"resource limit exceeded: input bytes would exceed {limit}",
                    )
                writer.write(chunk)
    except WorkerError:
        raise
    except OSError as error:
        fail(EXIT_READ_FAILED, f"cannot copy PDF input: {error}")
    return total


def inspect_pdf(input_path: Path) -> int:
    try:
        import pymupdf
    except ImportError as error:
        fail(EXIT_UNAVAILABLE, f"PyMuPDF is unavailable: {error}")

    # PyMuPDF captures its message stream during import. Route later `fitz`
    # compatibility warnings away from the JSON-only stdout protocol.
    pymupdf.set_messages(stream=sys.stderr)

    try:
        # Opening from bounded bytes avoids a PyMuPDF 1.28 Windows error path
        # that can retain an OS file handle after a malformed PDF is rejected.
        input_bytes = input_path.read_bytes()
        with pymupdf.open(stream=input_bytes, filetype="pdf") as document:
            if document.needs_pass:
                fail(EXIT_READ_FAILED, "password-protected PDFs are not supported")
            page_count = document.page_count
    except WorkerError:
        raise
    except Exception as error:
        fail(EXIT_READ_FAILED, f"PDF import failed: {error}")

    if page_count <= 0:
        fail(EXIT_READ_FAILED, "PDF import failed: the document has no pages")
    if page_count > MAX_PAGES:
        fail(
            EXIT_RESOURCE_LIMIT,
            f"resource limit exceeded: PDF pages would be {page_count} (limit {MAX_PAGES})",
        )
    return page_count


def create_docx(input_path: Path, output_path: Path) -> None:
    # pdf2docx imports the deprecated `fitz` compatibility module, which can
    # print warnings to stdout in recent PyMuPDF releases. Keep stdout reserved
    # for the API's newline-delimited JSON progress protocol throughout its
    # lifecycle.
    with redirect_stdout(sys.stderr):
        try:
            from pdf2docx import Converter
        except ImportError as error:
            fail(EXIT_UNAVAILABLE, f"pdf2docx is unavailable: {error}")

        converter = None
        try:
            converter = Converter(str(input_path))
            converter.convert(str(output_path))
        except Exception as error:
            fail(EXIT_WRITE_FAILED, f"DOCX conversion failed: {error}")
        finally:
            if converter is not None:
                try:
                    converter.close()
                except Exception:
                    logging.exception("Failed to close the PDF converter")


def validate_docx(output_path: Path, output_limit: int) -> int:
    try:
        output_bytes = output_path.stat().st_size
    except OSError as error:
        fail(EXIT_WRITE_FAILED, f"cannot inspect generated DOCX: {error}")

    if output_bytes > output_limit:
        fail(
            EXIT_RESOURCE_LIMIT,
            f"resource limit exceeded: output bytes would be {output_bytes} "
            f"(limit {output_limit})",
        )

    try:
        with zipfile.ZipFile(output_path) as archive:
            names = set(archive.namelist())
            if "[Content_Types].xml" not in names or "word/document.xml" not in names:
                fail(EXIT_WRITE_FAILED, "DOCX conversion produced an invalid OOXML package")
            archive.getinfo("word/document.xml")
    except WorkerError:
        raise
    except (OSError, zipfile.BadZipFile, KeyError) as error:
        fail(EXIT_WRITE_FAILED, f"DOCX conversion produced an invalid OOXML package: {error}")

    return output_bytes


def publish_output(source: Path, destination: Path, output_limit: int) -> None:
    try:
        total = 0
        with source.open("rb") as reader, destination.open("xb") as writer:
            while chunk := reader.read(COPY_BUFFER_BYTES):
                total += len(chunk)
                if total > output_limit:
                    fail(
                        EXIT_RESOURCE_LIMIT,
                        f"resource limit exceeded: output bytes would exceed {output_limit}",
                    )
                writer.write(chunk)
            writer.flush()
            os.fsync(writer.fileno())
    except WorkerError:
        destination.unlink(missing_ok=True)
        raise
    except OSError as error:
        destination.unlink(missing_ok=True)
        fail(EXIT_WRITE_FAILED, f"cannot write DOCX output: {error}")


def convert(arguments: argparse.Namespace) -> dict[str, int]:
    input_path = Path(arguments.input)
    output_path = Path(arguments.output)
    validate_paths(input_path, output_path)
    emit_progress(1, "validating")

    try:
        input_size = input_path.stat().st_size
    except OSError as error:
        fail(EXIT_INVALID_INPUT, f"cannot inspect PDF input: {error}")
    if input_size > arguments.max_input_bytes:
        fail(
            EXIT_RESOURCE_LIMIT,
            f"resource limit exceeded: input bytes would be {input_size} "
            f"(limit {arguments.max_input_bytes})",
        )

    try:
        output_path.parent.mkdir(parents=True, exist_ok=True)
    except OSError as error:
        fail(EXIT_WRITE_FAILED, f"cannot create output directory: {error}")

    with tempfile.TemporaryDirectory(
        prefix=".pdf-docx-",
        dir=output_path.parent,
    ) as work_directory:
        work_path = Path(work_directory)
        bounded_input = work_path / "source.pdf"
        generated_output = work_path / "generated.docx"

        emit_progress(5, "reading")
        input_bytes = copy_bounded_input(
            input_path,
            bounded_input,
            arguments.max_input_bytes,
        )

        emit_progress(20, "importing")
        page_count = inspect_pdf(bounded_input)
        emit_progress(35, "imported")

        emit_progress(40, "converting")
        create_docx(bounded_input, generated_output)
        output_bytes = validate_docx(generated_output, arguments.max_output_bytes)

        emit_progress(90, "writing")
        publish_output(generated_output, output_path, arguments.max_output_bytes)

    stats = {
        "inputBytes": input_bytes,
        "outputBytes": output_bytes,
        "pageCount": page_count,
    }
    emit_progress(100, "complete", **stats)
    return stats


def main(argv: Sequence[str] | None = None) -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="[%(levelname)s] %(message)s",
        stream=sys.stderr,
        force=True,
    )
    arguments = create_parser().parse_args(argv)
    try:
        convert(arguments)
        return 0
    except WorkerError as error:
        print(error, file=sys.stderr, flush=True)
        return error.exit_code
    except (MemoryError, RecursionError) as error:
        print(f"internal converter failure: {type(error).__name__}", file=sys.stderr, flush=True)
        return EXIT_INTERNAL_ERROR
    except Exception as error:
        logging.exception("Internal converter failure")
        print(f"internal converter failure: {error}", file=sys.stderr, flush=True)
        return EXIT_INTERNAL_ERROR


if __name__ == "__main__":
    raise SystemExit(main())
