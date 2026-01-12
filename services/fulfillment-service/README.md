# Fulfillment & Shipping Service

Service responsible for persisting shipment plans, coordinating label purchases, and surfacing delivery timelines across the platform. It listens to payment events, exposes admin APIs for fulfillment teams, and emits `Shipment*` events for downstream consumers.

## Features

- PostgreSQL + EF Core model for shipments, items, and timeline entries.
- MassTransit consumers for `IPaymentSucceeded` / `IPaymentFailed` to auto-create or cancel shipments.
- OAuth2-protected REST API for creating, scheduling, cancelling, and querying shipments.
- Carrier abstraction with a sandbox provider that returns deterministic tracking numbers.
- Structured Serilog logging via BuildingBlocks plus Redis cache placeholder for future tracking lookups.

## REST Endpoints

| Method | Route                                  | Description                                     | Policy             |
| ------ | -------------------------------------- | ----------------------------------------------- | ------------------ |
| `GET`  | `/api/shipments`                       | Filter by order, status, carrier, or date range | `FulfillmentRead`  |
| `GET`  | `/api/shipments/{shipmentId}`          | Detailed shipment with timeline                 | `FulfillmentRead`  |
| `GET`  | `/api/shipments/orders/{orderId}`      | All shipments for an order                      | `FulfillmentRead`  |
| `POST` | `/api/shipments`                       | Create shipment draft with address + items      | `FulfillmentWrite` |
| `POST` | `/api/shipments/{shipmentId}/schedule` | Purchase label via carrier provider             | `FulfillmentWrite` |
| `POST` | `/api/shipments/{shipmentId}/status`   | Override status/timeline notes                  | `FulfillmentWrite` |
| `POST` | `/api/shipments/{shipmentId}/cancel`   | Cancel/void a shipment                          | `FulfillmentWrite` |

## Messaging

### Consumes

- `IPaymentSucceeded` — creates shipment placeholder when an order is paid.
- `IPaymentFailed` — cancels outstanding shipments for the order.

### Publishes

- `IShipmentCreated`
- `IShipmentScheduled`
- `IShipmentStatusChanged`
- `IShipmentFailed`

Contracts are located under `building-blocks/BuildingBlocks/Contracts/Fulfillment` and are shared via MassTransit.

## Configuration

Key settings (see `appsettings*.json`):

| Setting                               | Description                                           |
| ------------------------------------- | ----------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL database for shipments                     |
| `ConnectionStrings:Redis`             | Redis cache for tracking lookups                      |
| `Keycloak:*`                          | OAuth issuer/audience plus swagger client information |
| `Carrier:DefaultCarrier`              | Name exposed by the registered carrier provider       |
| `RabbitMQ:*`                          | Broker connection for MassTransit                     |

## Local Development

1. Ensure docker-compose is running (includes `fulfillment-db`, `rabbitmq`, `redis`, `keycloak`).
2. Launch the service with `dotnet run --project services/fulfillment-service/FulfillmentService.csproj`.
3. Visit Swagger via the API Gateway (`/doc/fulfillment/swagger.json`) to exercise endpoints with OAuth.

## Next Steps

- Wire carrier webhooks + tracking sync and emit `Delivered` events to Order Service.
- Listen to inventory/order readiness events to auto-populate address + line items.
- Harden against duplicate payment events and add background health probes.
