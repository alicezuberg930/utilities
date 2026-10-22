# Utilities API

This solution is the ASP.NET Core API replacement for the NestJS YouTube module. It uses a layered Clean Architecture structure based on the C# Corner guide, adapted for an API-only service and deliberately organized by technical type rather than feature.

## Structure

```text
backend/
|-- Utilities.sln
|-- src/
|   |-- Utilities.Api/                 # HTTP controllers, filters, middleware, API configuration
|   |   |-- Controllers/V1/
|   |   |-- Extensions/
|   |   |-- Filters/
|   |   |-- Middleware/
|   |   `-- Models/Common/
|   |-- Utilities.Application/         # Use cases, DTOs, interfaces, validation
|   |   |-- DTOs/Requests/
|   |   |-- DTOs/Responses/
|   |   |-- Exceptions/
|   |   |-- Interfaces/
|   |   `-- Services/
|   |-- Utilities.Domain/              # Business entities and constants; no dependencies
|   |   |-- Constants/
|   |   `-- Entities/
|   `-- Utilities.Infrastructure/      # PostgreSQL, yt-dlp, files, queues, SSE broker, worker
|       |-- BackgroundServices/
|       |-- Configuration/
|       |-- Data/
|       |   |-- Configurations/
|       |   |-- Context/
|       |   `-- Migrations/
|       |-- Extensions/
|       |-- External/Youtube/
|       |-- Messaging/
|       |-- Realtime/
|       |-- Repositories/
|       `-- Services/
|-- vendor/
|   |-- cad-converter-worker/          # Isolated open-source DWG/DXF -> GLB worker (Rust)
|   `-- pdf-docx-converter-worker/     # Isolated PDF -> DOCX worker (Python)
|-- Dockerfile
`-- Dockerfile.dockerignore
```

Project dependencies are enforced as follows:

```text
Utilities.Api ----------> Utilities.Application ----------> Utilities.Domain
      |                                                       ^
      `--------------> Utilities.Infrastructure -------------'
```

`Utilities.Domain` has no project dependencies. Infrastructure implements the interfaces owned by the Application layer. The API project is the composition root and contains no website, Razor Pages, MVC views, or static frontend.

References:

- [C# Corner structure guide](https://www.c-sharpcorner.com/article/best-practices-for-structuring-large-asp-net-projects-a-simple-guide)
- [Common web application architectures](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
- [ASP.NET Core controllers](https://learn.microsoft.com/aspnet/core/web-api/?view=aspnetcore-10.0)
- [EF Core DbContext configuration](https://learn.microsoft.com/ef/core/dbcontext-configuration/)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)

## Run locally

PostgreSQL, `yt-dlp`, a Rust toolchain, and Python 3.10 or newer must be available. The checked-in development connection string matches the PostgreSQL service in the repository's `docker-compose.yml`.
On Windows, the Development configuration resolves `programs/yt-dlp.exe` from the repository automatically, so it does not need to be added to `PATH`.

```powershell
docker compose up -d postgres
.\backend\scripts\build-converter-workers.cmd
dotnet run --project backend/src/Utilities.Api/Utilities.Api.csproj
```

The Development configuration resolves the CAD worker from
`backend/vendor/cad-converter-worker/target/release` and the PDF-to-DOCX worker
from `backend/vendor/pdf-docx-converter-worker/.venv` plus `worker.py`.
`dotnet build` does not bootstrap either worker. To set up only the PDF-to-DOCX
worker, run `.\backend\scripts\build-converter-workers.cmd -Worker PdfDocx`.
The script installs hash-locked Python dependencies into the worker-local
virtual environment and runs its tests. The production Docker image builds and
installs both workers automatically; rebuild it instead of restarting a stale
image:

```powershell
docker compose up --build -d app
```

The development profile listens at `http://localhost:4000`. Development startup applies pending EF Core migrations. Production leaves automatic migration disabled by default; if the commented application service in Docker Compose is enabled, its example configuration explicitly enables migrations for that single-instance setup.

To build the entire solution:

```powershell
dotnet build backend/Utilities.sln
```

To create or apply migrations manually:

```powershell
dotnet ef migrations add MigrationName --project backend/src/Utilities.Infrastructure/Utilities.Infrastructure.csproj --startup-project backend/src/Utilities.Api/Utilities.Api.csproj --output-dir Data/Migrations
dotnet ef database update --project backend/src/Utilities.Infrastructure/Utilities.Infrastructure.csproj --startup-project backend/src/Utilities.Api/Utilities.Api.csproj
```

Configuration can be overridden with standard ASP.NET Core environment variables, for example:

```text
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=utilities;Username=admin;Password=...
Youtube__ExecutablePath=/usr/local/bin/yt-dlp
Youtube__JobsDirectory=/tmp/jobs
FileConversion__FfmpegExecutablePath=/usr/bin/ffmpeg
FileConversion__FfprobeExecutablePath=/usr/bin/ffprobe
FileConversion__MaxUploadFileBytes=268435456
FileConversion__MaxUploadRequestBytes=269484032
FileConversion__CadConverterExecutablePath=/usr/local/bin/utilities-cad-converter
FileConversion__PdfDocxConverterExecutablePath=/usr/local/bin/python3
FileConversion__PdfDocxConverterScriptPath=/opt/utilities-pdf-docx-converter/worker.py
FileConversion__JobsDirectory=/tmp/convert-jobs
CORS_ORIGINS=https://example.com,https://admin.example.com
```

## YouTube API

- `GET /api/v1/youtube/info?url=...`
- `POST /api/v1/youtube/job`
- `GET /api/v1/youtube/progress/{jobId}` (server-sent events)
- `GET /api/v1/youtube/file/{jobId}`

Example job request:

```json
{
  "url": "https://www.youtube.com/watch?v=...",
  "format": "video",
  "videoQuality": "720p"
}
```

## File conversion API

The conversion API accepts audio, video, single-file image, DWG, DXF, DWT, DWS, and PDF uploads as `multipart/form-data`. FFmpeg and FFprobe handle audio, video, and image conversions. CAD and PDF-to-DOCX conversion run in separate, time-bounded worker processes, so worker failures and ordinary process crashes are isolated from the API process. This is crash isolation rather than a complete security sandbox; deployments accepting untrusted files should also enforce container memory, CPU, request-size, and storage limits.

- `POST /api/v1/convert/job` with form fields `file` and `convertedFormat`
- `GET /api/v1/convert/progress/{jobId}` (server-sent events)
- `GET /api/v1/convert/file/{jobId}`

The upload action applies a finite endpoint-specific request limit before multipart model binding. `MaxUploadFileBytes` defaults to 256 MiB, while `MaxUploadRequestBytes` defaults to 257 MiB so multipart boundaries, headers, and the `convertedFormat` field have 1 MiB of headroom. Startup validation requires the file limit to cover every configured converter input limit and requires at least 1 MiB of request overhead. PDF and CAD sizes are checked again against their format-specific limits before a job file is stored. Any reverse proxy in front of the API must allow at least `MaxUploadRequestBytes` as well.

Supported audio outputs are `aac`, `flac`, `m4a`, `mp3`, `ogg`, `opus`, and `wav`. Supported video outputs are `avi`, `mkv`, `mov`, `mp4`, and `webm`. Video inputs can be converted to either audio or video; audio inputs can be converted to another audio format.

Image inputs cover the single-file image extensions recognized by the container's FFmpeg build, including common raster, camera, vector-rasterized, scientific, and legacy formats. Image outputs verified with that build are `apng`, `avif`, `bmp`, `dpx`, `exr`, `fits`, `gif`, `hdr`, `ico`, `im1`, `im8`, `im24`, `j2c`, `j2k`, `jls`, `jp2`, `jpeg`, `jpg`, `jxl`, `ljpg`, `pam`, `pbm`, `pcx`, `pfm`, `pgm`, `pgmyuv`, `phm`, `pix`, `png`, `ppm`, `qoi`, `ras`, `rs`, `sgi`, `sun`, `sunras`, `tga`, `tif`, `tiff`, `vbn`, `wbmp`, `webp`, `xbm`, `xface`, and `xwd`. Animated APNG, AVIF, GIF, and WebP outputs retain available frames; still-image outputs use the first decoded frame. Actual external-codec availability remains dependent on the installed FFmpeg build. FFmpeg's multi-file `.Y.U.V` image format is excluded because this endpoint accepts and returns one file.

DWG, DXF, DWT, and DWS inputs can be converted to `glb`.

PDF inputs can be converted to `docx`. The converter reconstructs editable Word content from the PDF's text, images, and detected layout; complex page layouts may not reproduce exactly, and image-only scanned PDFs require OCR before conversion. The API validates the returned ZIP/OOXML package before publishing it for download.

The Python worker vendors the official MIT-licensed `pdf2docx` v0.5.13 source tree and depends on PyMuPDF/MuPDF. PyMuPDF/MuPDF are available under the GNU AGPL or a separate Artifex commercial licence. Review `vendor/pdf-docx-converter-worker/WORKER.md` and `THIRD_PARTY_NOTICES.md` before deploying the worker; deployments that cannot satisfy AGPL obligations need an applicable commercial licence.

The worker uses pinned [opencadcodec](https://github.com/HakanSeven12/opencadcodec) and [opencadkernel](https://github.com/HakanSeven12/opencadkernel) revisions (MPL-2.0). It reads native DWG/DWS/DWT and DXF files, compiles each reusable block variant once, and represents every INSERT/MINSERT occurrence with a transformed glTF node that references shared mesh data. Nested transforms are composed; inherited appearance or scale-dependent tessellation creates a separate variant only when required. Non-repeated geometry is combined into bounded target-size batches without losing layer/color primitive groups. The worker exports analytic ACIS `3DSOLID`, `BODY`, `REGION`, and surface B-reps through adaptive kernel tessellation. B-rep boundary edges are emitted as glTF line primitives alongside shaded triangles. Common CAD wire and mesh entities are also exported, including rational/fit splines, helixes, point markers, and finite model-extent visualizations of rays and construction lines. Known drawing units are converted to glTF metres, per-mesh anchors preserve large-coordinate precision, and AutoCAD Z-up coordinates are rotated to glTF Y-up coordinates. Unitless or unknown `INSUNITS` values fall back to one metre per drawing unit and emit a warning.

Completeness is strict by default: an ACIS payload that cannot be parsed or lifted, a lost B-rep record, or a face that cannot be tessellated fails the job instead of publishing a deceptively sparse model. Hidden/off/frozen geometry and edge wires are included by default. Unsupported annotation and proxy entities are counted in the worker's final CLI statistics; strict conversion failures are written to worker diagnostics and server logs.

Third-party licensing and exact source revisions are documented in `vendor/cad-converter-worker/THIRD_PARTY_NOTICES.md`. The complete resolved Rust dependency inventory and license texts are in `vendor/cad-converter-worker/THIRD_PARTY_LICENSES.txt`; the production image installs both files under `/usr/share/doc/utilities-cad-converter/`.

The upload API currently accepts one source file. Embedded block references work; external XREF files are not imported by this endpoint. Supporting arbitrary XREF sets requires a future ZIP or multipart bundle endpoint, and external references are reported rather than silently treated as converted geometry.

CAD conversion limits can be overridden with standard configuration keys:

```text
FileConversion__CadConverterExecutablePath=/usr/local/bin/utilities-cad-converter
FileConversion__CadConversionTimeoutSeconds=900
FileConversion__CadLinearDeflectionMeters=0.0005
FileConversion__CadAngularDeflectionDegrees=10
FileConversion__CadIncludeHiddenObjects=true
FileConversion__CadExportEdgeWires=true
FileConversion__CadRequireCompleteGeometry=true
FileConversion__CadMaxInputBytes=268435456
FileConversion__CadMaxEntities=250000
FileConversion__CadMaxVertices=5000000
FileConversion__CadMaxTriangles=2000000
FileConversion__CadMaxLineSegments=2000000
FileConversion__CadMaxOutputBytes=268435456
```

The byte limits default to 256 MiB. `CadMaxEntities` independently caps stored
document entities, unique compilation visits, prototype placements, logical
block occurrences, and emitted glTF nodes. Geometry limits count unique encoded
`POSITION` elements, source triangles, and source line segments, so repeated
nodes do not repay the same mesh cost. Exceeding any limit fails the conversion
without publishing a partial model; limits are never handled by silently
truncating geometry. The output limit must fit the GLB format's 32-bit
total-length field. The worker's final JSON distinguishes rendered occurrence
totals from once-encoded unique triangle/line totals.

PDF-to-DOCX conversion limits can be overridden with standard configuration keys:

```text
FileConversion__PdfDocxConverterExecutablePath=/usr/local/bin/python3
FileConversion__PdfDocxConverterScriptPath=/opt/utilities-pdf-docx-converter/worker.py
FileConversion__PdfDocxConversionTimeoutSeconds=600
FileConversion__PdfDocxMaxInputBytes=134217728
FileConversion__PdfDocxMaxOutputBytes=268435456
```

The API checks the PDF header and input size before starting the worker, bounds captured worker diagnostics, terminates the process tree on cancellation or timeout, and validates the resulting DOCX package before atomically publishing it. The worker rejects PDFs above its 1,000-page conversion limit instead of returning a truncated document.
