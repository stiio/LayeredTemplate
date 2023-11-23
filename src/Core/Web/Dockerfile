# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base

ENV ASPNETCORE_URLS=http://*:3080

WORKDIR /app
EXPOSE 3080

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /src

COPY ["*.props", "."]
COPY ["Core/Web/*.csproj", "Web/"]
COPY ["Core/Web.Api/*.csproj", "Web.Api/"]
COPY ["Core/Application/*.csproj", "Application/"]
COPY ["Core/Application.Contracts/*.csproj", "Application.Contracts/"]
COPY ["Core/Domain/*.csproj", "Domain/"]
COPY ["Core/Infrastructure/*.csproj", "Infrastructure/"]
COPY ["Core/Infrastructure.Data/*.csproj", "Infrastructure.Data/"]
COPY ["Common/Shared/*.csproj", "Shared/"]
COPY ["Common/Messaging/*.csproj", "Messaging/"]

RUN dotnet restore "Web/Web.csproj"

COPY . .

WORKDIR "/src/Web"

RUN dotnet build "Web.csproj" -c Release -o /app/build

# =============== Publish ===============
FROM build AS publish

RUN dotnet publish "Web.csproj" -c Release -o /app/publish

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Web.dll"]