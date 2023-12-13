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
COPY ["Core/Web/*.csproj", "Core/Web/"]
COPY ["Core/Application/*.csproj", "Core/Application/"]
COPY ["Core/Domain/*.csproj", "Core/Domain/"]
COPY ["Core/Infrastructure/*.csproj", "Core/Infrastructure/"]
COPY ["Core/Infrastructure.Data/*.csproj", "Core/Infrastructure.Data/"]
COPY ["Common/Shared/*.csproj", "Common/Shared/"]
COPY ["Common/Messaging/*.csproj", "Common/Messaging/"]

RUN dotnet restore "Core/Web/Web.csproj"

COPY . .

WORKDIR "/src/Core/Web"

RUN dotnet build "Web.csproj" -c Release -o /app/build

# =============== Publish ===============
FROM build AS publish

RUN dotnet publish "Web.csproj" -c Release -o /app/publish

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Web.dll"]