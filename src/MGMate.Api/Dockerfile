FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
WORKDIR /src
ARG APP_VERSION=0.0.0-local
# Shared build settings (target framework, analyzers) and central package versions. MSBuild
# finds them by walking up from each project folder, so they sit one level above the projects.
COPY ["Directory.Build.props", "Directory.Packages.props", ".editorconfig", "./"]
COPY ["src/MGMate.Core/MGMate.Core.csproj", "MGMate.Core/"]
COPY ["src/MGMate.Data/MGMate.Data.csproj", "MGMate.Data/"]
COPY ["src/MGMate.Api/MGMate.Api.csproj", "MGMate.Api/"]
RUN dotnet restore "MGMate.Api/MGMate.Api.csproj"
COPY src/MGMate.Core/ MGMate.Core/
COPY src/MGMate.Data/ MGMate.Data/
COPY src/MGMate.Api/ MGMate.Api/
WORKDIR /src/MGMate.Api
# ${APP_VERSION#v} strips a leading "v" (e.g. a tag name), which MinVer rejects
RUN dotnet publish -c Release -o /app /p:MinVerVersionOverride=${APP_VERSION#v}

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c AS runtime
WORKDIR /app
# OS_PATCH_DATE busts the layer cache so apt-get upgrade actually re-runs instead of
# reusing a stale cached layer from before an OS package fix was published.
ARG OS_PATCH_DATE=0
RUN echo "os patch date: ${OS_PATCH_DATE}" \
	&& apt-get update \
	&& apt-get upgrade -y \
	&& apt-get install -y --no-install-recommends libgssapi-krb5-2 curl tzdata \
	&& rm -rf /var/lib/apt/lists/*
# The entrypoint drops root to APP_UID. The dotnet base image sets APP_UID=1654 and creates the
# "app" user with that uid; say so here and check it while building, so a base image that renames
# the user or drops the variable fails the build instead of crash-looping the container.
ENV APP_UID=1654
RUN test "$(id -u app)" = "$APP_UID"
COPY --from=build /app .
COPY --chmod=755 src/MGMate.Api/docker-entrypoint.sh /docker-entrypoint.sh
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=15s --retries=5 \
	CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["/docker-entrypoint.sh"]
