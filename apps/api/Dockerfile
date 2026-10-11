# Build from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# docker compose does this for you.

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only. This layer is rebuilt only when a project file,
# a package version or the SDK version changes, so editing a .cs file reuses it.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Now the source. Only this layer and the ones after it are rebuilt on a code change.
COPY apps/api/src apps/api/src
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---- run ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Plain HTTP on 8080, as a user that is not root. The image ships with a built-in "app" user.
# No configuration, connection string or key is set here: they all arrive as environment
# variables when the container starts (see docker-compose.yml and .env.example).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
