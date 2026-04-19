# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

ENV ASPNETCORE_URLS=http://*:8080
EXPOSE 8080
WORKDIR /app

RUN apk add --no-cache \
    tzdata \
    icu-libs

USER app

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY [".editorconfig", "."]
COPY ["*.props", "."]
COPY ["Services/App/App.Web/*.csproj", "Services/App/App.Web/"]
COPY ["Services/App/App.Web/*.targets", "Services/App/App.Web/"]
COPY ["Services/App/App.Application/*.csproj", "Services/App/App.Application/"]
COPY ["Services/App/App.Domain/*.csproj", "Services/App/App.Domain/"]
COPY ["Services/App/App.Shared/*.csproj", "Services/App/App.Shared/"]
COPY ["Services/App/App.Infrastructure/*.csproj", "Services/App/App.Infrastructure/"]
COPY ["Services/App/App.Infrastructure.Data/*.csproj", "Services/App/App.Infrastructure.Data/"]
COPY ["Services/Plugins/Plugins.AssemblyExtensions/*.csproj", "Services/Plugins/Plugins.AssemblyExtensions/"]
COPY ["Services/Plugins/Plugins.Http.Extensions/*.csproj", "Services/Plugins/Plugins.Http.Extensions/"]
COPY ["Services/Plugins/Plugins.Logging.HttpClientLog/*.csproj", "Services/Plugins/Plugins.Logging.HttpClientLog/"]
COPY ["Services/Plugins/Plugins.SharedExtensions/*.csproj", "Services/Plugins/Plugins.SharedExtensions/"]
COPY ["Services/Plugins/Plugins.StartupRunner/*.csproj", "Services/Plugins/Plugins.StartupRunner/"]
COPY ["Services/Plugins/Plugins.JsonMultipart/*.csproj", "Services/Plugins/Plugins.JsonMultipart/"]
COPY ["Services/Plugins/Plugins.JsonMultipart.Abstractions/*.csproj", "Services/Plugins/Plugins.JsonMultipart.Abstractions/"]
COPY ["Services/Plugins/Plugins.PhoneHelpers/*.csproj", "Services/Plugins/Plugins.PhoneHelpers/"]

RUN dotnet restore "Services/App/App.Web/App.Web.csproj"

COPY . .

WORKDIR "/src/Services/App/App.Web"

RUN dotnet publish "App.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "App.Web.dll"]