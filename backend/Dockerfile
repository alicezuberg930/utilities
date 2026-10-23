# Build the Rust CAD conversion worker in an isolated stage.
FROM rust:1.89-bookworm AS cad-build
WORKDIR /src/backend/vendor/cad-converter-worker

# Copy dependency metadata first, then source code, to improve Docker layer caching.
COPY backend/vendor/cad-converter-worker/Cargo.toml backend/vendor/cad-converter-worker/Cargo.lock backend/vendor/cad-converter-worker/LICENSE backend/vendor/cad-converter-worker/THIRD_PARTY_NOTICES.md backend/vendor/cad-converter-worker/THIRD_PARTY_LICENSES.txt ./
COPY backend/vendor/cad-converter-worker/src/ ./src/

# Compile the worker as a locked, optimized release binary.
RUN cargo build --release --locked

# Install and test the Python PDF-to-DOCX worker in an isolated stage.
FROM python:3.12-slim-bookworm AS pdf-docx-build
WORKDIR /opt/utilities-pdf-docx-converter

COPY backend/vendor/pdf-docx-converter-worker/requirements.lock ./
RUN python -m pip install \
    --disable-pip-version-check \
    --no-cache-dir \
    --require-hashes \
    --requirement requirements.lock

COPY backend/vendor/pdf-docx-converter-worker/worker.py ./
COPY backend/vendor/pdf-docx-converter-worker/pdf2docx/ ./pdf2docx/

# Restore and publish the ASP.NET Core API in a separate SDK stage.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files before source code so NuGet restore can be cached independently.
COPY backend/src/Utilities.Domain/Utilities.Domain.csproj backend/src/Utilities.Domain/
COPY backend/src/Utilities.Application/Utilities.Application.csproj backend/src/Utilities.Application/
COPY backend/src/Utilities.Infrastructure/Utilities.Infrastructure.csproj backend/src/Utilities.Infrastructure/
COPY backend/src/Utilities.Api/Utilities.Api.csproj backend/src/Utilities.Api/
RUN dotnet restore backend/src/Utilities.Api/Utilities.Api.csproj

# Copy the backend source and publish the API without repeating package restore.
COPY backend/ backend/
RUN dotnet publish backend/src/Utilities.Api/Utilities.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Assemble the final runtime-only image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Pin yt-dlp and verify its downloaded binary against the expected checksum.
ARG YT_DLP_VERSION=2026.08.19
ARG YT_DLP_SHA256=1fa6733c37ea6fb51c99ad8fe785e7b7e5f3246c9b980230329d4fb72ed8d4d6

# Install media dependencies, yt-dlp, and writable job directories, then clear package metadata.
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl ffmpeg libgomp1 \
    && curl --fail --location "https://github.com/yt-dlp/yt-dlp/releases/download/${YT_DLP_VERSION}/yt-dlp" --output /usr/local/bin/yt-dlp \
    && echo "${YT_DLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum --check \
    && chmod 0755 /usr/local/bin/yt-dlp \
    && mkdir -p /tmp/jobs /tmp/convert-jobs \
    && chown -R "${APP_UID}:${APP_UID}" /tmp/jobs /tmp/convert-jobs \
    && rm -rf /var/lib/apt/lists/*

# Install the published API, conversion workers, and worker license documentation.
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=pdf-docx-build /usr/local /usr/local
COPY --from=cad-build /src/backend/vendor/cad-converter-worker/target/release/utilities-cad-converter /usr/local/bin/utilities-cad-converter
COPY --from=cad-build /src/backend/vendor/cad-converter-worker/LICENSE /usr/share/doc/utilities-cad-converter/LICENSE
COPY --from=cad-build /src/backend/vendor/cad-converter-worker/THIRD_PARTY_NOTICES.md /usr/share/doc/utilities-cad-converter/THIRD_PARTY_NOTICES.md
COPY --from=cad-build /src/backend/vendor/cad-converter-worker/THIRD_PARTY_LICENSES.txt /usr/share/doc/utilities-cad-converter/THIRD_PARTY_LICENSES.txt
COPY --from=pdf-docx-build /opt/utilities-pdf-docx-converter/worker.py /opt/utilities-pdf-docx-converter/worker.py
COPY --from=pdf-docx-build /opt/utilities-pdf-docx-converter/pdf2docx /opt/utilities-pdf-docx-converter/pdf2docx
COPY backend/vendor/pdf-docx-converter-worker/LICENSE /usr/share/doc/utilities-pdf-docx-converter/LICENSE
COPY backend/vendor/pdf-docx-converter-worker/LICENSE.PyMuPDF-AGPL-3.0.txt /usr/share/doc/utilities-pdf-docx-converter/LICENSE.PyMuPDF-AGPL-3.0.txt
COPY backend/vendor/pdf-docx-converter-worker/THIRD_PARTY_NOTICES.md /usr/share/doc/utilities-pdf-docx-converter/THIRD_PARTY_NOTICES.md
COPY backend/vendor/pdf-docx-converter-worker/THIRD_PARTY_LICENSES.txt /usr/share/doc/utilities-pdf-docx-converter/THIRD_PARTY_LICENSES.txt

# Configure service ports, converter executables, and temporary job storage.
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Youtube__ExecutablePath=/usr/local/bin/yt-dlp
ENV Youtube__JobsDirectory=/tmp/jobs
ENV FileConversion__FfmpegExecutablePath=/usr/bin/ffmpeg
ENV FileConversion__FfprobeExecutablePath=/usr/bin/ffprobe
ENV FileConversion__MaxUploadFileBytes=268435456
ENV FileConversion__MaxUploadRequestBytes=269484032
ENV FileConversion__CadConverterExecutablePath=/usr/local/bin/utilities-cad-converter
ENV FileConversion__PdfDocxConverterExecutablePath=/usr/local/bin/python3
ENV FileConversion__PdfDocxConverterScriptPath=/opt/utilities-pdf-docx-converter/worker.py
ENV FileConversion__JobsDirectory=/tmp/convert-jobs

# Run the application as the non-root user supplied by the .NET runtime image.
USER $APP_UID

# Document the listening port and launch the API process.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Utilities.Api.dll"]
