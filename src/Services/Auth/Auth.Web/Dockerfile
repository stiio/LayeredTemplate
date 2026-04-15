# =============== Base ===============
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

USER app

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# =============== Build ===============
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY ["Services/Auth/Auth.Web/Auth.Web.csproj", "Services/Auth/Auth.Web/"]

RUN dotnet restore "Services/Auth/Auth.Web/Auth.Web.csproj"

COPY . .

WORKDIR "/src/Services/Auth/Auth.Web"

RUN dotnet build "./Auth.Web.csproj" -c Release -o /app/build

# =============== Publish ===============
FROM build AS publish

RUN dotnet publish "Auth.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =============== Final ===============
FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Auth.Web.dll"]
