# Order Service

## Overview

The Order Service is a critical microservice in the ecommerce platform responsible for managing customer orders, order items, and order status lifecycle. It integrates with the Catalog Service for product validation and the Identity Service for authentication.

## Features

### Core Functionality

- **Create Orders**: Customers can create new orders with multiple items
- **Retrieve Orders**: Get individual orders or user's order history
- **Order Status Management**: Track orders through their lifecycle (Pending → Processing → Shipped → Delivered)
- **User Order History**: Retrieve all orders for a specific user with pagination support
- **Admin Functions**: Manage orders, update status, and view orders by status

### Order Status Enum

- `Pending` (0): Order created, awaiting processing
- `Processing` (1): Order is being prepared for shipment
- `Shipped` (2): Order has been shipped
- `Delivered` (3): Order has been delivered to customer
- `Cancelled` (4): Order has been cancelled

## Architecture

### Database Schema

**Order Table**

- `Id` (Guid): Primary key
- `UserId` (string): User who placed the order
- `Status` (OrderStatus): Current order status
- `TotalAmount` (decimal): Total order cost
- `CreatedAt` (DateTime): Order creation timestamp
- `UpdatedAt` (DateTime): Last update timestamp
- `ShippingAddress` (string): Delivery address

**OrderItem Table**

- `Id` (Guid): Primary key
- `OrderId` (Guid): Foreign key to Order
- `ProductId` (Guid): Reference to product in Catalog Service
- `ProductName` (string): Cached product name
- `UnitPrice` (decimal): Price at time of order
- `Quantity` (int): Quantity ordered

### Technology Stack

- **Framework**: .NET 8.0
- **Database**: PostgreSQL with Entity Framework Core
- **Cache**: Redis (for future caching implementation)
- **Message Broker**: RabbitMQ with MassTransit
- **Authentication**: JWT via Keycloak
- **Logging**: Serilog

## API Endpoints

### Authentication Required (User)

#### Create Order

```
POST /api/orders
Authorization: Bearer <JWT_TOKEN>
Content-Type: application/json

{
  "items": [
    {
      "productId": "550e8400-e29b-41d4-a716-446655440000",
      "quantity": 2
    },
    {
      "productId": "550e8400-e29b-41d4-a716-446655440001",
      "quantity": 1
    }
  ],
  "shippingAddress": "123 Main St, City, State 12345"
}

Response: 201 Created
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "userId": "user-123",
  "status": 0,
  "totalAmount": 199.99,
  "createdAt": "2026-01-10T12:00:00Z",
  "updatedAt": null,
  "shippingAddress": "123 Main St, City, State 12345",
  "items": [...]
}
```

#### Get User Orders

```
GET /api/orders/my-orders
Authorization: Bearer <JWT_TOKEN>

Response: 200 OK
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "userId": "user-123",
    "status": 0,
    "totalAmount": 199.99,
    "createdAt": "2026-01-10T12:00:00Z",
    "items": [...]
  }
]
```

#### Get Order by ID

```
GET /api/orders/{orderId}
Authorization: Bearer <JWT_TOKEN>

Response: 200 OK
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "userId": "user-123",
  "status": 0,
  "totalAmount": 199.99,
  "createdAt": "2026-01-10T12:00:00Z",
  "items": [...]
}
```

### Admin Only Endpoints

#### Update Order Status

```
PUT /api/orders/{orderId}/status
Authorization: Bearer <ADMIN_JWT_TOKEN>
Content-Type: application/json

{
  "status": 1
}

Response: 200 OK
```

#### Get Orders by Status

```
GET /api/orders/by-status/{status}
Authorization: Bearer <ADMIN_JWT_TOKEN>

Response: 200 OK
[
  {
    "id": "...",
    "status": 0,
    ...
  }
]
```

#### Delete Order

```
DELETE /api/orders/{orderId}
Authorization: Bearer <ADMIN_JWT_TOKEN>

Response: 204 No Content
```

## Service Integration

### Catalog Service Integration

The Order Service calls the Catalog Service to verify product existence before creating orders:

```
GET http://catalog-service:8080/api/products/verify?ids={productId1}&ids={productId2}
```

If verification fails, order creation is rejected with a 400 Bad Request.

### Message Events

The Order Service publishes events to the message broker:

#### IOrderCreated

Published when a new order is successfully created

```
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "userId": "user-123",
  "totalAmount": 199.99,
  "timestamp": "2026-01-10T12:00:00Z"
}
```

#### IOrderStatusChanged

Published when an order status is updated

```
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "previousStatus": 0,
  "newStatus": 1,
  "timestamp": "2026-01-10T12:00:00Z"
}
```

## Configuration

### Environment Variables

```
# Database
ConnectionStrings__DefaultConnection=Host=order-db;Database=orderdb;Username=orderuser;Password=orderpassword

# Redis Cache
ConnectionStrings__Redis=redis:6379
Redis__InstanceName=OrderService_

# RabbitMQ
RabbitMQ__Host=rabbitmq
RabbitMQ__UserName=guest
RabbitMQ__Password=guest

# Keycloak Authentication
Keycloak__AuthServerUrl=http://keycloak:8080/
Keycloak__Realm=ecommerce-realm
Keycloak__Resource=order-service

# Catalog Service
CatalogService__BaseUrl=http://catalog-service:8080
```

## Development

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL 15 (or Docker)
- Redis (or Docker)
- RabbitMQ (or Docker)
- Keycloak (or Docker)

### Local Setup

1. Start infrastructure:

```bash
cd deploy
docker-compose up -d
```

2. Build the service:

```bash
dotnet build
```

3. Run the service:

```bash
cd services/order-service
dotnet run
```

The API will be available at `http://localhost:5003`
Swagger UI: `http://localhost:5003/swagger`

### Running Tests

```bash
dotnet test
```

## Database Migrations

### Initial Setup (Development)

```csharp
// In Program.cs, DbContext.Database.EnsureCreated() is called automatically
```

### Production Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Performance Considerations

### Indexes

The Order Service creates indexes on frequently queried columns:

- `UserId` - for user order history queries
- `CreatedAt` - for time-based sorting
- `Status` - for status filtering

### Caching

Redis caching is configured for future implementation:

- User order history cache
- Product lookup cache
- Order detail cache

## Security

- JWT authentication via Keycloak
- Role-based access control (User, Admin roles)
- User ownership validation for order retrieval
- Admin-only endpoints for status updates and deletion

## Logging

Serilog is configured with:

- Console output
- File output to `/app/logs/order-service/`
- Structured logging with correlation IDs

## Docker Deployment

### Build Image

```bash
docker build -f services/order-service/Dockerfile -t ecommerce/order-service .
```

### Run Container

```bash
docker run -p 5003:8080 \
  -e "ConnectionStrings__DefaultConnection=Host=host.docker.internal;Database=orderdb;..." \
  -e "Keycloak__AuthServerUrl=http://keycloak:8080/" \
  ecommerce/order-service
```

## Future Enhancements

- [ ] Order fulfillment workflow
- [ ] Return/Refund management
- [ ] Invoice generation
- [ ] Email notifications
- [ ] Payment integration
- [ ] Inventory synchronization
- [ ] Order analytics and reporting
- [ ] GraphQL API support

## Troubleshooting

### Database Connection Issues

Check connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=order-db;Database=orderdb;Username=orderuser;Password=orderpassword"
}
```

### Authentication Failures

Ensure:

1. Keycloak is running and accessible
2. JWT token is valid and not expired
3. User has correct roles assigned in Keycloak

### Product Verification Failures

Verify:

1. Catalog Service is running
2. Products exist in Catalog Service
3. Network connectivity between services

## Support

For issues or questions, refer to the main [README.md](../../README.md) or contact the development team.
