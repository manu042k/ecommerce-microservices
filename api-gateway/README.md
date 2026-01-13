# API Gateway

The API Gateway serves as the single entry point for all client requests to the ecommerce microservices platform. It uses YARP (Yet Another Reverse Proxy) to route requests to downstream services and aggregates Swagger documentation.

## Overview

The API Gateway provides:

- Request routing to appropriate microservices
- Centralized authentication and authorization
- Aggregated Swagger UI for all services
- CORS policy management
- Request/response logging

## Technology Stack

- **Framework**: .NET 8.0
- **Reverse Proxy**: YARP (Yet Another Reverse Proxy)
- **Authentication**: JWT via Keycloak
- **Documentation**: Swagger/OpenAPI

## Configuration

The gateway configuration is defined in `appsettings.json`:

### Reverse Proxy Routes

Routes are configured in the `ReverseProxy` section:

- `/api/identity/*` → Identity Service (port 5001)
- `/api/catalog/*` → Catalog Service (port 5002)
- `/api/orders/*` → Order Service (port 5003)
- `/api/inventory/*` → Inventory Service (port 5005)
- `/api/payments/*` → Payment Service (port 5004)
- `/api/shipments/*` → Fulfillment Service (port 5006)

### Swagger Aggregation

Swagger endpoints are exposed at:

- `/doc/identity/swagger.json` → Identity Service
- `/doc/catalog/swagger.json` → Catalog Service
- `/doc/order/swagger.json` → Order Service
- `/doc/inventory/swagger.json` → Inventory Service
- `/doc/payment/swagger.json` → Payment Service
- `/doc/fulfillment/swagger.json` → Fulfillment Service

### Keycloak Configuration

```json
{
  "Keycloak": {
    "AuthServerUrl": "http://keycloak:8080/",
    "Realm": "ecommerce-realm",
    "Resource": "api-gateway",
    "Credentials": {
      "Secret": "your-client-secret"
    }
  }
}
```

## Features

### Authentication

The gateway validates JWT tokens from Keycloak and forwards them to downstream services. Authentication is configured using JWT Bearer authentication scheme.

### CORS Policy

A permissive CORS policy is configured for development. In production, this should be restricted to specific origins.

### Swagger UI

The gateway aggregates Swagger documentation from all services into a single UI accessible at `/swagger`. OAuth2 authentication is configured for testing protected endpoints.

## Local Development

1. Ensure Keycloak and all services are running:

```bash
cd deploy
docker-compose up -d
```

2. Run the gateway:

```bash
cd api-gateway/ApiGateway
dotnet run
```

3. Access the gateway:

- Gateway: http://localhost:5050
- Swagger UI: http://localhost:5050/swagger

## Docker Deployment

The gateway includes a Dockerfile. Build and run:

```bash
docker build -f api-gateway/ApiGateway/Dockerfile -t ecommerce/api-gateway .
docker run -p 5050:8080 ecommerce/api-gateway
```

Or use Docker Compose (see `deploy/docker-compose.yml`).

## Testing

The gateway includes test projects for:

- Authentication configuration
- CORS policy
- Reverse proxy configuration
- Swagger configuration

Run tests:

```bash
cd api-gateway
dotnet test
```

## Request Flow

1. Client sends request to API Gateway
2. Gateway validates JWT token (if required)
3. Gateway routes request to appropriate service based on path
4. Service processes request and returns response
5. Gateway forwards response to client

## Logging

The gateway uses Serilog for structured logging. Logs include:

- Request/response details
- Authentication events
- Routing decisions
- Error information

Logs are written to console and file (in Docker: `/app/logs/api-gateway/`).
