# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY StockPulse.sln ./
COPY StockPulse.Domain/StockPulse.Domain.csproj StockPulse.Domain/
COPY StockPulse.DAL/StockPulse.DAL.csproj StockPulse.DAL/
COPY StockPulse.BLL/StockPulse.BLL.csproj StockPulse.BLL/
COPY StockPulse.API/StockPulse.API.csproj StockPulse.API/
COPY StockPulse.Tests/StockPulse.Tests.csproj StockPulse.Tests/

RUN dotnet restore StockPulse.sln

# Copy everything else and build
COPY . .
RUN dotnet publish StockPulse.API/StockPulse.API.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create mount point for persistent disk
RUN mkdir -p /data

COPY --from=build /app/publish .

# In .NET 8, the default HTTP port is 8080.
# If Railway or Render sets the PORT env var, Program.cs dynamically binds to it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "StockPulse.API.dll"]
