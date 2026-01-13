# Catalog Service

Service responsible for managing the product catalog including products, categories, and product metadata. Provides product verification endpoints for other services and publishes product update events.

## Overview

The Catalog Service maintains the central product catalog for the ecommerce platform. It handles product and category management, provides search and filtering capabilities, and integrates with other services for product validation.

## Features

- Product CRUD operations
- Category management
- Product search and filtering
- Product verification for other services
- Redis caching for performance
- Product update event publishing via MassTransit

## Technology Stack

- **Framework**: .NET 8.0
- **Database**: PostgreSQL with Entity Framework Core
- **Cache**: Redis
- **Message Broker**: RabbitMQ with MassTransit
- **Authentication**: JWT via Keycloak
- **Logging**: Serilog

## API Endpoints

### Products

| Method   | Route                  | Description                                 | Auth Policy        |
| -------- | ---------------------- | ------------------------------------------- | ------------------ |
| `GET`    | `/api/products`        | List products with search/filter/pagination | `UserOrAdmin`      |
| `GET`    | `/api/products/{id}`   | Get product by ID                           | `UserOrAdmin`      |
| `POST`   | `/api/products`        | Create new product                          | `AdminOnly`        |
| `PUT`    | `/api/products/{id}`   | Update product                              | `AdminOnly`        |
| `DELETE` | `/api/products/{id}`   | Delete product                              | `AdminOnly`        |
| `GET`    | `/api/products/verify` | Verify product IDs (for other services)     | Service-to-service |

### Categories

| Method   | Route                  | Description         | Auth Policy   |
| -------- | ---------------------- | ------------------- | ------------- |
| `GET`    | `/api/categories`      | List all categories | `UserOrAdmin` |
| `GET`    | `/api/categories/{id}` | Get category by ID  | `UserOrAdmin` |
| `POST`   | `/api/categories`      | Create new category | `AdminOnly`   |
| `PUT`    | `/api/categories/{id}` | Update category     | `AdminOnly`   |
| `DELETE` | `/api/categories/{id}` | Delete category     | `AdminOnly`   |

## Query Parameters

### Product List Endpoint

- `searchTerm`: Search in product name and description
- `categoryId`: Filter by category
- `minPrice`: Minimum price filter
- `maxPrice`: Maximum price filter
- `page`: Page number (default: 1)
- `pageSize`: Items per page (default: 10)

## Data Model

### Product

- `Id` (Guid): Primary key
- `Name` (string): Product name
- `Description` (string): Product description
- `Price` (decimal): Product price
- `ImageUrl` (string): Product image URL
- `CategoryId` (Guid): Foreign key to Category
- `Category` (Category): Navigation property

### Category

- `Id` (Guid): Primary key
- `Name` (string): Category name
- `Description` (string): Category description

## Caching Strategy

Products are cached in Redis to improve performance:

- Individual products: Cached for 10 minutes
- Product lists (no filters): Cached for 5 minutes
- Cache invalidation on product create/update/delete

## Messaging

### Publishes

- `ProductUpdated`: Published when a product is updated

## Configuration

Key settings in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=catalog-db;Database=catalogdb;...",
    "Redis": "redis:6379"
  },
  "Redis": {
    "InstanceName": "CatalogService_"
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "UserName": "guest",
    "Password": "guest"
  },
  "Keycloak": {
    "AuthServerUrl": "http://keycloak:8080/",
    "Realm": "ecommerce-realm",
    "Resource": "catalog-service"
  }
}
```

## Local Development

1. Ensure infrastructure is running:

```bash
cd deploy
docker-compose up -d catalog-db redis rabbitmq keycloak
```

2. Run the service:

```bash
cd services/catalog-service
dotnet run
```

3. Access Swagger UI:

- Direct: http://localhost:5002/swagger
- Via Gateway: http://localhost:5050/swagger

## Database Initialization

The service automatically creates the database and seeds initial data on startup (development mode). In production, use EF Core migrations:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Testing

Run tests:

```bash
cd services/catalog-service
dotnet test
```

Test coverage includes:

- Controller tests
- Database initialization tests
- CORS policy tests
- Swagger configuration tests

## Docker Deployment

Build and run:

```bash
docker build -f services/catalog-service/Dockerfile -t ecommerce/catalog-service .
docker run -p 5002:8080 ecommerce/catalog-service
```

Or use Docker Compose (see `deploy/docker-compose.yml`).

## Integration with Other Services

### Order Service

The Order Service calls `/api/products/verify` to validate product IDs before creating orders.

### Inventory Service

The Inventory Service may reference products from the catalog for inventory management.

## Performance Considerations

- Redis caching reduces database load
- Pagination prevents large result sets
- Indexes on frequently queried columns (Name, CategoryId, Price)
- Connection pooling for database connections

## Security

- JWT authentication via Keycloak
- Role-based access control:
  - `Admin`: Full access
  - `User`/`Customer`: Read-only access
- Service-to-service verification endpoint

## Logging

Structured logging with Serilog:

- Console output
- File output: `/app/logs/catalog-service/`
- Request/response logging
- Correlation ID tracking
