# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

USER app

WORKDIR /app

# TLS is terminated at the ingress/reverse proxy; the container speaks HTTP only.
# Pin ASPNETCORE_URLS so Kestrel doesn't try to bind HTTPS without a certificate.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# ---- Restore layer -----------------------------------------------------------
# Copy ONLY the project manifests + Directory.Build.props before the source tree.
# This makes the `dotnet restore` layer cacheable: Docker reuses it across builds
# until any .csproj or Directory.Build.props changes. Without all ProjectReference
# targets present here, NuGet can't resolve the full graph and most of the restore
# work is pushed to a later step, defeating layer caching.
COPY Directory.Build.props ./
COPY ["Services/Auth/Auth.Web/Auth.Web.csproj", "Services/Auth/Auth.Web/"]
COPY ["Services/Plugins/Plugins.AssemblyExtensions/Plugins.AssemblyExtensions.csproj", "Services/Plugins/Plugins.AssemblyExtensions/"]
COPY ["Services/Plugins/Plugins.Http.Extensions/Plugins.Http.Extensions.csproj", "Services/Plugins/Plugins.Http.Extensions/"]
COPY ["Services/Plugins/Plugins.PhoneHelpers/Plugins.PhoneHelpers.csproj", "Services/Plugins/Plugins.PhoneHelpers/"]
COPY ["Services/Plugins/Plugins.StartupRunner/Plugins.StartupRunner.csproj", "Services/Plugins/Plugins.StartupRunner/"]

RUN dotnet restore "Services/Auth/Auth.Web/Auth.Web.csproj"

# ---- Publish layer -----------------------------------------------------------
# `dotnet publish` runs build implicitly — a separate `dotnet build` step would
# duplicate the work and its output is never copied to the final image anyway.
# `--no-restore` skips a redundant restore since the previous layer already did it;
# `UseAppHost=false` skips the native apphost we don't need (we launch via `dotnet`).
COPY . .
WORKDIR "/src/Services/Auth/Auth.Web"
RUN dotnet publish "Auth.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =============== Final ===============
FROM base AS final

WORKDIR /app

# --chown keeps files owned by the non-root `app` user — matches the USER directive
# in `base` and avoids root-owned files in a non-root container (defense in depth).
COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "Auth.Web.dll"]
