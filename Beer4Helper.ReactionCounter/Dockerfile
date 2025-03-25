FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Beer4Helper.ReactionCounter/Beer4Helper.ReactionCounter.csproj", "Beer4Helper.ReactionCounter/"]
RUN dotnet restore "Beer4Helper.ReactionCounter/Beer4Helper.ReactionCounter.csproj"
COPY . .
WORKDIR "/src/Beer4Helper.ReactionCounter"
RUN dotnet build "Beer4Helper.ReactionCounter.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Beer4Helper.ReactionCounter.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Beer4Helper.ReactionCounter.dll"]
