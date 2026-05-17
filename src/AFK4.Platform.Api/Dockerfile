# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/AFK4.BuildingBlocks/AFK4.BuildingBlocks.csproj", "src/AFK4.BuildingBlocks/"]
COPY ["src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj", "src/AFK4.Shared.Contracts/"]
COPY ["src/AFK4.Platform.Api/AFK4.Platform.Api.csproj", "src/AFK4.Platform.Api/"]

RUN dotnet restore "src/AFK4.Platform.Api/AFK4.Platform.Api.csproj"

COPY . .

RUN dotnet publish "src/AFK4.Platform.Api/AFK4.Platform.Api.csproj" \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl wget \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "AFK4.Platform.Api.dll"]
