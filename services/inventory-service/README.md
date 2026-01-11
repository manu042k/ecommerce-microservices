# Inventory Service (Planning Draft)

## 1. Mission & Responsibilities

- Maintain canonical product stock levels across warehouses/locations.
- Serve real-time availability checks to Order Service and other consumers.
- Reserve and release inventory in response to order placement, cancellation, and fulfillment events.
- Publish low-stock, reservation, and adjustment events for downstream services (Fulfillment, Analytics).
- Support administrative adjustments and scheduled reconciliations with external ERPs (future).

## 2. Domain Boundaries

- **Owns:** Inventory counts, reservations, stock adjustments, safety thresholds.
- **Depends On:**
  - Catalog Service for product metadata validation.
  - Order Service events (`OrderCreated`, `OrderCancelled`, `OrderDelivered`).
  - Fulfillment Service for shipment confirmations that reduce reserved → deducted.
- **Produces:** Events such as `InventoryReserved`, `InventoryReleased`, `StockLevelChanged`, `LowStockAlert`.

## 3. API Surface (Draft)

### Internal/Service Endpoints

1. `POST /internal/inventory/reservations` — Reserve stock for an order.
2. `POST /internal/inventory/reservations/{reservationId}/release` — Release reservation due to cancellation/timeout.
3. `POST /internal/inventory/reservations/{reservationId}/commit` — Deduct stock when fulfillment confirms shipment.
4. `GET /internal/inventory/availability?productId=...` — Batched availability check for Order Service.

### Admin Endpoints

1. `GET /api/inventory` — List inventory levels with filters (warehouse, product, status).
2. `POST /api/inventory/adjustments` — Manual adjustment (reason codes, audit trail).
3. `POST /api/inventory/reconciliation` — Trigger background job to reconcile with ERP.

## 4. Messaging Contracts

- **Commands/Events Consumed:**
  - `IOrderCreated` — attempt reservation.
  - `IOrderCancelled` / `IOrderFailed` — release reservation.
  - `IFulfillmentConfirmed` — commit deduction.
- **Events Published:**
  - `IInventoryReserved` `{ reservationId, orderId, items }`
  - `IInventoryReservationFailed` `{ orderId, reason }`
  - `IInventoryCommitted` `{ orderId, items }`
  - `ILowStockAlert` `{ productId, remainingQuantity, threshold }`
- Shared DTOs to live in `building-blocks/Contracts/Inventory/`.

## 5. Data Model Sketch (PostgreSQL + EF Core)

1. `InventoryItems`
   - `Id (Guid)`
   - `ProductId (Guid)`
   - `LocationId (Guid)` (optional, default warehouse for MVP)
   - `QuantityOnHand`
   - `QuantityReserved`
   - `ReorderPoint`
   - `SafetyStock`
   - `UpdatedAt`
2. `InventoryReservations`
   - `Id (Guid)`
   - `OrderId (Guid)`
   - `Status (Pending, Confirmed, Released, Failed)`
   - `ExpiresAt`
   - `CreatedAt`
3. `InventoryReservationItems`
   - `ReservationId`
   - `ProductId`
   - `Quantity`
4. `InventoryAdjustments`
   - `Id`
   - `ProductId`
   - `Delta`
   - `Reason`
   - `CreatedBy`
   - `CreatedAt`

## 6. Reservation Strategy

- Default policy: **soft reservation** on `OrderCreated`; automatically release after configurable TTL if payment/confirmation not received.
- Expose configuration for strict vs eventual consistency per channel (future multi-warehouse support).
- Use distributed locks or database row-level locking to avoid overselling; include idempotency keys based on `orderId`.

## 7. Integration Flow

1. Order Service submits reservation via REST/MassTransit immediately after validating payment intent.
2. Inventory Service reserves quantities and responds with success/failure.
3. On cancellation/payment failure, Order Service emits event to release inventory.
4. Fulfillment Service emits `ShipmentCreated`/`ShipmentCancelled`; Inventory commits or releases accordingly.

## 8. Operational Requirements

- Config: DB connection, RabbitMQ, Keycloak, cache (Redis) for fast availability lookups.
- Health Checks verifying DB + message broker.
- Observability: Serilog, metrics (reservations/sec, low stock counts, reservation failure rate).
- Scheduled job (Hangfire/Quartz) to sweep expired reservations.
- Dockerfile + docker-compose entry (port 5005).

## 9. Next Actions

1. Scaffold `services/inventory-service` .NET project and reference BuildingBlocks.
2. Define contracts/events in `building-blocks/Contracts/Inventory/`.
3. Implement reservation workflow (controller + EF Core + MassTransit consumers).
4. Wire Order Service to call Inventory before finalizing order acceptance.
5. Add tests for reservation conflicts and low-stock alerts.
