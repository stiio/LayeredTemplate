# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

USER app

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# tzdata + icu-libs are needed when InvariantGlobalization is disabled (see Directory.Build.props).
RUN apk add --no-cache tzdata icu-libs

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Cacheable restore layer: copy all .csproj + Directory.Build.props + Version.targets first so
# `dotnet restore` is only re-run when project files / package versions actually change.
COPY [".editorconfig", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Services/App/App.Web/App.Web.csproj", "Services/App/App.Web/"]
COPY ["Services/App/App.Web/Version.targets", "Services/App/App.Web/"]
COPY ["Services/Plugins/Plugins.AssemblyExtensions/Plugins.AssemblyExtensions.csproj", "Services/Plugins/Plugins.AssemblyExtensions/"]
COPY ["Services/Plugins/Plugins.Http.Extensions/Plugins.Http.Extensions.csproj", "Services/Plugins/Plugins.Http.Extensions/"]
COPY ["Services/Plugins/Plugins.JsonMultipart/Plugins.JsonMultipart.csproj", "Services/Plugins/Plugins.JsonMultipart/"]
COPY ["Services/Plugins/Plugins.JsonMultipart.Abstractions/Plugins.JsonMultipart.Abstractions.csproj", "Services/Plugins/Plugins.JsonMultipart.Abstractions/"]
COPY ["Services/Plugins/Plugins.StartupRunner/Plugins.StartupRunner.csproj", "Services/Plugins/Plugins.StartupRunner/"]

RUN dotnet restore "Services/App/App.Web/App.Web.csproj"

COPY . .

WORKDIR "/src/Services/App/App.Web"
RUN dotnet publish "App.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "App.Web.dll"]
