# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base

ENV ASPNETCORE_URLS=http://*:3080
EXPOSE 3080
WORKDIR /app

RUN apk add --no-cache \
    tzdata \
    icu-libs

USER app

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /src

COPY [".editorconfig", "."]
COPY ["*.props", "."]
COPY ["Services/App/App.Web/*.csproj", "Services/App/App.Web/"]
COPY ["Services/App/App.Application/*.csproj", "Services/App/App.Application/"]
COPY ["Services/App/App.Domain/*.csproj", "Services/App/App.Domain/"]
COPY ["Services/App/App.Infrastructure/*.csproj", "Services/App/App.Infrastructure/"]
COPY ["Services/App/App.Infrastructure.Data/*.csproj", "Services/App/App.Infrastructure.Data/"]
COPY ["Shared/Shared/*.csproj", "Shared/Shared/"]
COPY ["Shared/Shared.Messaging/*.csproj", "Shared/Shared.Messaging/"]

RUN dotnet restore "Services/App/App.Web/App.Web.csproj"

COPY . .

WORKDIR "/src/Services/App/App.Web"

RUN dotnet build "App.Web.csproj" -c Release -o /app/build

# =============== Publish ===============
FROM build AS publish

RUN dotnet publish "App.Web.csproj" -c Release -o /app/publish

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "App.Web.dll"]