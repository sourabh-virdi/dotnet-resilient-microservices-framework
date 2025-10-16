# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY *.sln .

# Copy project files
COPY src/ResilientMicroservices.Core/*.csproj src/ResilientMicroservices.Core/
COPY src/ResilientMicroservices.Resilience/*.csproj src/ResilientMicroservices.Resilience/
COPY src/ResilientMicroservices.Tracing/*.csproj src/ResilientMicroservices.Tracing/
COPY src/ResilientMicroservices.Messaging/*.csproj src/ResilientMicroservices.Messaging/
COPY src/ResilientMicroservices.Sagas/*.csproj src/ResilientMicroservices.Sagas/
COPY samples/OrderService/*.csproj samples/OrderService/
COPY samples/PaymentService/*.csproj samples/PaymentService/
COPY samples/InventoryService/*.csproj samples/InventoryService/
COPY samples/ApiGateway/*.csproj samples/ApiGateway/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build the application
ARG SERVICE_NAME=OrderService
WORKDIR /src/samples/${SERVICE_NAME}
RUN dotnet build -c Release --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release --no-build -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser
USER appuser

# Copy published application
COPY --from=publish /app .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/orders/health || exit 1

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "OrderService.dll"] 