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

PostgreSQL and `yt-dlp` must be available. The checked-in development connection string matches the PostgreSQL service in the repository's `docker-compose.yml`.
On Windows, the Development configuration resolves `programs/yt-dlp.exe` from the repository automatically, so it does not need to be added to `PATH`.

```powershell
docker compose up -d postgres
dotnet run --project backend/src/Utilities.Api/Utilities.Api.csproj
```

The development profile listens at `http://localhost:4000`. Development startup applies pending EF Core migrations. Production leaves automatic migration disabled by default; Docker Compose explicitly enables it for this single-instance setup.

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

The conversion API accepts audio and video uploads as `multipart/form-data`. FFmpeg and FFprobe must be available at the configured executable paths.

- `POST /api/v1/convert/job` with form fields `file` and `convertedFormat`
- `GET /api/v1/convert/progress/{jobId}` (server-sent events)
- `GET /api/v1/convert/file/{jobId}`

Supported audio outputs are `aac`, `flac`, `m4a`, `mp3`, `ogg`, `opus`, and `wav`. Supported video outputs are `avi`, `mkv`, `mov`, `mp4`, and `webm`. Video inputs can be converted to either audio or video; audio inputs can be converted to another audio format.
