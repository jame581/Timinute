# syntax=docker/dockerfile:1.7

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# csproj-only first → restore layer caches well
COPY Timinute.sln ./
COPY Timinute/Server/Timinute.Server.csproj  Timinute/Server/
COPY Timinute/Client/Timinute.Client.csproj  Timinute/Client/
COPY Timinute/Shared/Timinute.Shared.csproj  Timinute/Shared/
RUN dotnet restore Timinute/Server/Timinute.Server.csproj -a $TARGETARCH

COPY . .
RUN dotnet publish Timinute/Server/Timinute.Server.csproj \
        -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN mkdir -p /keys && chown app:app /keys
USER app
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DatabaseMigrationOnStartup=true \
    DataProtection__KeyPath=/keys/data-protection \
    IdentityServer__KeyManagement__KeyPath=/keys \
    ForwardedHeaders__AllowAnyProxy=true
EXPOSE 8080
# VOLUME is metadata; the actual /keys directory was created and chowned
# earlier (as root) before the USER switch.
VOLUME ["/keys"]
ENTRYPOINT ["dotnet", "Timinute.Server.dll"]
