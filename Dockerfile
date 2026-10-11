FROM node:22-bookworm-slim AS client-build
WORKDIR /src/IndustrialCatalog/ClientApp
COPY IndustrialCatalog/ClientApp/package.json ./
RUN npm install --no-audit --no-fund
COPY IndustrialCatalog/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY IndustrialCatalog/ ./
COPY --from=client-build /src/IndustrialCatalog/wwwroot/ ./wwwroot/
RUN dotnet publish IndustrialCatalog.csproj -c Release -o /app -p:BuildClientApp=false
RUN dotnet /app/IndustrialCatalog.dll --membership-self-test

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:8080 ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "IndustrialCatalog.dll"]
