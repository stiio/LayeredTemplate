#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Base
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine3.16 AS base

ENV ASPNETCORE_URLS=http://*:3090;https://*:3091

WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Web.Mock/Web.Mock.csproj", "Web.Mock/"]
COPY ["Web.Api/Web.Api.csproj", "Web.Api/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Application.Contracts/Application.Contracts.csproj", "Application.Contracts/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "Web.Mock/Web.Mock.csproj"
COPY . .
WORKDIR "/src/Web.Mock"
RUN dotnet build "Web.Mock.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Web.Mock.csproj" -c Release -o /app/publish /p:UseAppHost=false

#Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Web.Mock.dll"]