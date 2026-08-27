# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY EcommerceTxPr.sln ./
COPY EcommerceApi.V2/EcommerceApi.V2.csproj EcommerceApi.V2/
COPY EcommerceTxPr.Application/EcommerceTxPr.Application.csproj EcommerceTxPr.Application/
COPY EcommerceTxPr.DbMigrator/EcommerceTxPr.DbMigrator.csproj EcommerceTxPr.DbMigrator/
COPY EcommerceTxPr.Domain/EcommerceTxPr.Domain.csproj EcommerceTxPr.Domain/
COPY EcommerceTxPr.Infrastructure/EcommerceTxPr.Infrastructure.csproj EcommerceTxPr.Infrastructure/
COPY EcommerceTxPr.IntegrationTests/EcommerceTxPr.IntegrationTests.csproj EcommerceTxPr.IntegrationTests/
COPY EcommerceTxPr.UnitTests/EcommerceTxPr.UnitTests.csproj EcommerceTxPr.UnitTests/
RUN dotnet restore EcommerceTxPr.sln

COPY . .

FROM restore AS build
RUN dotnet build EcommerceTxPr.sln \
    --configuration Release \
    --no-restore \
    -warnaserror

FROM build AS api-publish
RUN dotnet publish EcommerceApi.V2/EcommerceApi.V2.csproj \
    --configuration Release \
    --no-build \
    --output /app/api \
    /p:UseAppHost=false

FROM build AS migrations-publish
RUN dotnet publish EcommerceTxPr.DbMigrator/EcommerceTxPr.DbMigrator.csproj \
    --configuration Release \
    --no-build \
    --output /app/migrations \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api-publish /app/api .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "EcommerceApi.V2.dll"]

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS migrations-runtime
WORKDIR /app
COPY --from=migrations-publish /app/migrations .
USER app
ENTRYPOINT ["dotnet", "EcommerceTxPr.DbMigrator.dll"]
