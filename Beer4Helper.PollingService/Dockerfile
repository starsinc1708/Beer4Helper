FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Beer4Helper.PollingService/Beer4Helper.PollingService.csproj", "Beer4Helper.PollingService/"]
RUN dotnet restore "Beer4Helper.PollingService/Beer4Helper.PollingService.csproj"
COPY . .
WORKDIR "/src/Beer4Helper.PollingService"
RUN dotnet build "Beer4Helper.PollingService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Beer4Helper.PollingService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN chmod 644 /app/modules-conf.yml
ENTRYPOINT ["dotnet", "Beer4Helper.PollingService.dll"]
