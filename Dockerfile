# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY StockPulse.sln ./
COPY StockPulse.Domain/StockPulse.Domain.csproj StockPulse.Domain/
COPY StockPulse.DAL/StockPulse.DAL.csproj StockPulse.DAL/
COPY StockPulse.BLL/StockPulse.BLL.csproj StockPulse.BLL/
COPY StockPulse.API/StockPulse.API.csproj StockPulse.API/

RUN dotnet restore StockPulse.sln

# Copy everything else and build
COPY . .
RUN dotnet publish StockPulse.API/StockPulse.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create mount point for Render persistent disk
RUN mkdir -p /data

COPY --from=build /app/publish .

# Render routes traffic to port 10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "StockPulse.API.dll"]
