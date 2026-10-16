FROM node:22-bookworm-slim AS client-build
WORKDIR /src/IndustrialCatalog/ClientApp
COPY IndustrialCatalog/ClientApp/package.json IndustrialCatalog/ClientApp/package-lock.json ./
RUN npm ci --include=dev --no-audit --no-fund
COPY IndustrialCatalog/ClientApp/ ./
RUN npm run build
RUN npm run test:booklet

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY IndustrialCatalog/ ./
COPY --from=client-build /src/IndustrialCatalog/wwwroot/ ./wwwroot/
RUN dotnet publish IndustrialCatalog.csproj -c Release -o /app -p:BuildClientApp=false
RUN dotnet /app/IndustrialCatalog.dll --quote-self-test
RUN dotnet /app/IndustrialCatalog.dll --product-description-self-test
RUN dotnet /app/IndustrialCatalog.dll --operations-lists-self-test
RUN dotnet /app/IndustrialCatalog.dll --audit-self-test
RUN dotnet /app/IndustrialCatalog.dll --membership-self-test
RUN dotnet /app/IndustrialCatalog.dll --live-support-self-test
RUN dotnet /app/IndustrialCatalog.dll --product-video-self-test
RUN dotnet /app/IndustrialCatalog.dll --media-optimization-self-test

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:8080 ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "IndustrialCatalog.dll"]
