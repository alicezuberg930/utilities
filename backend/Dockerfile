FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/src/Utilities.Domain/Utilities.Domain.csproj backend/src/Utilities.Domain/
COPY backend/src/Utilities.Application/Utilities.Application.csproj backend/src/Utilities.Application/
COPY backend/src/Utilities.Infrastructure/Utilities.Infrastructure.csproj backend/src/Utilities.Infrastructure/
COPY backend/src/Utilities.Api/Utilities.Api.csproj backend/src/Utilities.Api/
RUN dotnet restore backend/src/Utilities.Api/Utilities.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/src/Utilities.Api/Utilities.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG YT_DLP_VERSION=2026.08.19
ARG YT_DLP_SHA256=1fa6733c37ea6fb51c99ad8fe785e7b7e5f3246c9b980230329d4fb72ed8d4d6

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl ffmpeg python3 \
    && curl --fail --location "https://github.com/yt-dlp/yt-dlp/releases/download/${YT_DLP_VERSION}/yt-dlp" --output /usr/local/bin/yt-dlp \
    && echo "${YT_DLP_SHA256}  /usr/local/bin/yt-dlp" | sha256sum --check \
    && chmod 0755 /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV Youtube__ExecutablePath=/usr/local/bin/yt-dlp
ENV Youtube__JobsDirectory=/tmp/jobs

EXPOSE 8080
ENTRYPOINT ["dotnet", "Utilities.Api.dll"]
