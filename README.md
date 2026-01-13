# Ecommerce Microservices Platform

A distributed ecommerce platform built with .NET 8 microservices architecture. The platform consists of multiple independent services that work together to provide a complete ecommerce solution.

## Architecture Overview

This project follows a microservices architecture pattern with the following components:

- **API Gateway**: Single entry point for all client requests using YARP (Yet Another Reverse Proxy)
- **Identity Service**: User authentication and authorization via Keycloak integration
- **Catalog Service**: Product and category management
- **Order Service**: Order creation and lifecycle management
- **Inventory Service**: Stock level management and reservations
- **Payment Service**: Payment processing with Stripe integration
- **Fulfillment Service**: Shipment management and carrier coordination

## Technology Stack

- **Framework**: .NET 8.0
- **Database**: PostgreSQL 15 (one per service)
- **Cache**: Redis
- **Message Broker**: RabbitMQ with MassTransit
- **Authentication**: Keycloak (OAuth2/JWT)
- **API Gateway**: YARP (Yet Another Reverse Proxy)
- **Logging**: Serilog
- **Containerization**: Docker

## Project Structure

```
ecommerce-microservices/
├── api-gateway/          # API Gateway service
├── building-blocks/      # Shared contracts and utilities
├── services/             # Microservices
│   ├── catalog-service/
│   ├── identity-service/
│   ├── inventory-service/
│   ├── order-service/
│   ├── payment-service/
│   └── fulfillment-service/
├── deploy/               # Docker Compose and deployment configs
└── docs/                 # Documentation
```

## Services

### API Gateway

Central entry point that routes requests to appropriate microservices and aggregates Swagger documentation.

### Identity Service

Handles user registration, login, and authentication. Integrates with Keycloak for OAuth2/JWT token management.

### Catalog Service

Manages product catalog including products, categories, and product metadata. Provides product verification endpoints for other services.

### Order Service

Manages customer orders, order items, and order status lifecycle. Integrates with Catalog Service for product validation.

### Inventory Service

Maintains stock levels, handles reservations, and manages inventory adjustments. Supports real-time availability checks.

### Payment Service

Processes payments through Stripe integration, handles refunds, and manages payment webhooks. Publishes payment events for downstream services.

### Fulfillment Service

Manages shipments, coordinates with carrier providers, and tracks delivery timelines. Listens to payment events to create shipments automatically.

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Docker and Docker Compose
- PostgreSQL 15 (or use Docker)
- Redis (or use Docker)
- RabbitMQ (or use Docker)
- Keycloak (or use Docker)

### Local Development

1. Clone the repository:

```bash
git clone <repository-url>
cd ecommerce-microservices
```

2. Start infrastructure services:

```bash
cd deploy
docker-compose up -d
```

This will start:

- Keycloak (port 8080)
- PostgreSQL databases for each service
- Redis (port 6379)
- RabbitMQ (ports 5672, 15672)
- All microservices

3. Access services:

- API Gateway: http://localhost:5050
- Keycloak Admin: http://localhost:8080 (admin/admin)
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Individual services are available on ports 5001-5006

4. Access Swagger UI:
   Navigate to http://localhost:5050/swagger to access the aggregated Swagger UI for all services.

### Running Individual Services

To run a service individually:

```bash
cd services/<service-name>
dotnet run
```

## Configuration

Each service has its own `appsettings.json` and `appsettings.Development.json` files. Key configuration areas:

- **Connection Strings**: Database and Redis connections
- **Keycloak**: Authentication server URL, realm, and client configuration
- **RabbitMQ**: Message broker connection settings
- **Service URLs**: Inter-service communication endpoints

## Building Blocks

The `building-blocks` project contains shared contracts and utilities:

- **Contracts**: Message contracts for MassTransit (events, commands)
- **Logging**: Serilog configuration extensions

All services reference this project to ensure consistent messaging and logging.

## API Gateway

The API Gateway uses YARP to route requests to downstream services:

- `/api/identity/*` → Identity Service
- `/api/catalog/*` → Catalog Service
- `/api/orders/*` → Order Service
- `/api/inventory/*` → Inventory Service
- `/api/payments/*` → Payment Service
- `/api/shipments/*` → Fulfillment Service

Swagger documentation is aggregated at `/swagger` endpoint.

## Message Flow

Services communicate via RabbitMQ using MassTransit:

1. **Order Created** → Inventory Service reserves stock
2. **Payment Succeeded** → Fulfillment Service creates shipment
3. **Payment Failed** → Fulfillment Service cancels shipment
4. **Shipment Created** → Inventory Service commits reservation

## Testing

Each service includes a test project with unit and integration tests:

```bash
dotnet test
```

## Docker Deployment

All services include Dockerfiles. Build and run using Docker Compose:

```bash
cd deploy
docker-compose up --build
```
