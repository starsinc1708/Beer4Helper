﻿FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Beer4Helper.BeerEventManager/Beer4Helper.BeerEventManager.csproj", "Beer4Helper.BeerEventManager/"]
RUN dotnet restore "Beer4Helper.BeerEventManager/Beer4Helper.BeerEventManager.csproj"
COPY . .
WORKDIR "/src/Beer4Helper.BeerEventManager"
RUN dotnet build "Beer4Helper.BeerEventManager.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Beer4Helper.BeerEventManager.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

COPY ../bot-settings.test.yml /app/bot-settings.yml

RUN chmod 644 /app/bot-settings.yml

ENTRYPOINT ["dotnet", "Beer4Helper.BeerEventManager.dll"]
