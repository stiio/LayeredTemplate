#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Base
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.16 AS base

ENV ASPNETCORE_URLS=http://*:3080

WORKDIR /app
EXPOSE 3080

# Build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /src

COPY ["*.targets", "."]
COPY ["Web/*.csproj", "Web/"]
COPY ["Application/*.csproj", "Application/"]
COPY ["Application.Contracts/*.csproj", "Application.Contracts/"]
COPY ["Domain/*.csproj", "Domain/"]
COPY ["Shared/*.csproj", "Shared/"]
COPY ["Infrastructure/*.csproj", "Infrastructure/"]
COPY ["Infrastructure.Data/*.csproj", "Infrastructure.Data/"]
COPY ["Web.Api/*.csproj", "Web.Api/"]

RUN dotnet restore "Web/Web.csproj"

COPY . .

WORKDIR "/src/Web"

RUN dotnet build "Web.csproj" -c Release -o /app/build

# Publish
FROM build AS publish

RUN dotnet publish "Web.csproj" -c Release -o /app/publish

# Final
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Web.dll"]