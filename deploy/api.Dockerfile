# Build context is the repository root:
#   podman build -f deploy/api.Dockerfile -t employees-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore as its own layer so it is cached when only source changes.
COPY WebAPI/WebAPI/WebAPI.csproj WebAPI/WebAPI/
COPY schema.sqlite.sql seed.sqlite.sql ./
RUN dotnet restore WebAPI/WebAPI/WebAPI.csproj

COPY WebAPI/ WebAPI/
RUN dotnet publish WebAPI/WebAPI/WebAPI.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Workstation GC: server GC reserves per-core heaps that do not fit a
# 512 MB / 0.1 CPU free-tier instance.
ENV DOTNET_gcServer=0 \
    Database__Provider=Sqlite \
    ConnectionStrings__DefaultConnection="Data Source=/data/employees.db;Default Timeout=5" \
    Storage__PhotosPath=/data/Photos \
    PORT=8080

# Database and photos share one directory so that on an ephemeral filesystem
# they reset together and never disagree.
RUN mkdir -p /data/Photos && chown -R $APP_UID:$APP_UID /data
USER $APP_UID

EXPOSE 8080

# The port is read in Program.cs, not baked into ASPNETCORE_URLS here: $PORT
# would not be interpolated at container runtime with an exec-form entrypoint.
ENTRYPOINT ["dotnet", "WebAPI.dll"]
