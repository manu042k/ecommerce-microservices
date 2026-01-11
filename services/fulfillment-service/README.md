# Fulfillment & Shipping Service (Planning Draft)

## 1. Mission & Responsibilities

- Orchestrate post-payment order lifecycle: pick, pack, ship, and delivery confirmation.
- Integrate with carriers (Shippo/EasyPost first) to purchase labels, track packages, and surface status updates.
- Coordinate with Inventory Service to convert reserved stock into deducted stock once shipments are confirmed.
- Provide customers and admins with shipment visibility (status timeline, carrier, tracking number).
- Emit lifecycle events (`ShipmentCreated`, `ShipmentDispatched`, `ShipmentDelivered`, `ShipmentException`) for Order Service and notifications.

## 2. Domain Boundaries

- **Owns:** Shipment records, package details, carrier selections, tracking metadata, delivery exceptions.
- **Depends On:**
  - Order Service for paid/ready orders and order details (items, addresses).
  - Inventory Service for reservation confirmations and deduction triggers.
  - Payment Service indirectly (only handle orders with successful payments).
  - External carrier APIs for label generation and tracking webhooks.
- **Produces:** Events to Order Service (status transitions), Inventory (commit/release), Notification/Analytics services (future).

## 3. Core Workflow

1. Order transitions to `Processing` after payment + inventory reservation.
2. Fulfillment Service receives `OrderReadyForFulfillment` event → creates shipment(s) and requests pick/pack.
3. Upon packing, service calls carrier API to purchase label and marks as `ReadyToShip`.
4. When package leaves warehouse, status moves to `Dispatched` and event is published.
5. Carrier webhooks update statuses (`InTransit`, `OutForDelivery`, `Delivered`, `Exception`).
6. Delivered status notifies Order Service to mark order `Delivered` and Inventory Service to finalize deduction if not already done.

## 4. API Surface (Draft)

### Internal Endpoints

1. `POST /internal/fulfillment/orders/{orderId}/shipments` — Create shipment(s) for an order (triggered via event consumer fallback).
2. `POST /internal/fulfillment/shipments/{shipmentId}/status` — Update statuses when processing background events.
3. `POST /internal/fulfillment/carriers/webhooks/{provider}` — Receive carrier callbacks (signature validation + dedupe).

### Admin Endpoints

1. `GET /api/fulfillment/shipments` — Filter by status, carrier, warehouse.
2. `GET /api/fulfillment/shipments/{shipmentId}` — Full shipment timeline and audit trail.
3. `POST /api/fulfillment/retry/{shipmentId}` — Re-run label purchase or status sync in case of errors.
4. `POST /api/fulfillment/manual-update` — Manually override status with reason codes.

### Customer-Facing

1. `GET /api/fulfillment/my-shipments` — Paginated history for authenticated users (via API Gateway).

## 5. Messaging Contracts

- **Consumes:**
  - `IOrderReadyForFulfillment` (Order Service) — contains orderId, userId, address, line items.
  - `IInventoryCommitted` / `IInventoryReserved` for synchronization checks.
  - `IPaymentSucceeded` if direct chaining is required for readiness.
- **Publishes:**
  - `IShipmentCreated`
  - `IShipmentDispatched`
  - `IShipmentDelivered`
  - `IShipmentException`
  - `IShipmentCancelled`
- Contract DTOs under `building-blocks/Contracts/Fulfillment/` with MassTransit registration.

## 6. Data Model Sketch (PostgreSQL + EF Core)

1. `Shipments`
   - `Id (Guid)`
   - `OrderId (Guid)`
   - `UserId (Guid/string)`
   - `Status (enum: PendingPick, Packed, ReadyToShip, Dispatched, InTransit, Delivered, Exception, Cancelled)`
   - `Carrier (string)`
   - `TrackingNumber (string)`
   - `LabelUrl`
   - `EstimatedDelivery`, `ActualDelivery`
   - `CreatedAt`, `UpdatedAt`
2. `ShipmentPackages`
   - `ShipmentId`
   - `Weight`, `Dimensions`, `Items (jsonb)`
3. `ShipmentEvents`
   - `Id`
   - `ShipmentId`
   - `Status`
   - `Description`
   - `Source (system/carrier/admin)`
   - `CreatedAt`
4. `CarrierAccounts`
   - `Provider`
   - `ApiKey`, `AccountNumber`
   - `WebhookSecret`

## 7. Carrier Integration Layer

- Define `ICarrierProvider` interface (methods: `CreateShipment`, `PurchaseLabel`, `Track`, `ParseWebhook`).
- Initial provider: Shippo or EasyPost (REST APIs with webhooks).
- Retry/backoff with Polly, mask credentials in logs.
- Support multi-carrier routing later via provider registry.

## 8. Operational Requirements

- Config: DB, RabbitMQ, Redis (cache tracking statuses), carrier credentials, Keycloak.
- Health checks for DB + RabbitMQ + carrier API heartbeat.
- Background jobs to poll carrier tracking as fallback when webhooks fail.
- Observability: metrics (shipments/day, exception rate, delivery times), structured logs with correlation IDs (`orderId`, `shipmentId`).
- Dockerfile + compose entry exposing port 5006.

## 9. Next Actions

1. Scaffold .NET API project under `services/fulfillment-service` referencing BuildingBlocks.
2. Define fulfillment contracts/events and update MassTransit setup.
3. Implement event consumers for `IOrderReadyForFulfillment` and orchestrate shipment creation.
4. Integrate with chosen carrier sandbox (Shippo/EasyPost) with secret placeholders.
5. Extend Order Service to emit readiness events and react to shipment updates.
