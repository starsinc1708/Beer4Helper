# Use the ASP.NET base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Ensure the container runs as root for testing purposes
USER root  
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Beer4Helper.PollingService/Beer4Helper.PollingService.csproj", "Beer4Helper.PollingService/"]
RUN dotnet restore "Beer4Helper.PollingService/Beer4Helper.PollingService.csproj"
COPY . . 
WORKDIR "/src/Beer4Helper.PollingService"
RUN dotnet build "Beer4Helper.PollingService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Beer4Helper.PollingService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage (for runtime)
FROM base AS final
WORKDIR /app

# Copy the published app
COPY --from=publish /app/publish .

# Copy the configuration file into the container
COPY ../modules-conf.test.yml /app/modules-conf.yml

# Set correct permissions for the config file (root needed for this)
RUN chmod 644 /app/modules-conf.yml

# Run the application
ENTRYPOINT ["dotnet", "Beer4Helper.PollingService.dll"]
