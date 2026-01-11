# Payment Service (Planning Draft)

## 1. Mission & Responsibilities

- Accept payment intents triggered by the Order Service and return deterministic status updates.
- Abstract external payment providers (initial target: Stripe) while keeping the core domain provider-agnostic.
- Persist payment attempts, captures, refunds, and provider webhooks for auditability.
- Publish domain events (`PaymentSucceeded`, `PaymentFailed`, `RefundIssued`) to notify Order, Inventory, and Fulfillment services.
- Enforce security (Keycloak + service-to-service auth) and provide observability/alerting hooks similar to other services.

## 2. Domain Boundaries

- **Owns:** Payment intents, transaction lifecycle, provider configuration, reconciliation jobs.
- **Depends On:**
  - Order Service: consumes `IOrderCreated` events and synchronous REST calls to initiate payments.
  - Identity Service / Keycloak: authenticates admin/customer endpoints.
  - External Payment Provider (Stripe sandbox initially).
- **Produces:** Events for successful/failed payments, refund outcomes, reconciliation notifications.

## 3. API Surface (Draft)

### Customer/Auth Endpoints

1. `POST /api/payments` — Create payment intent (body: `orderId`, `amount`, `currency`, `paymentMethodId`). Returns client secret or redirect URL depending on provider.
2. `GET /api/payments/{paymentId}` — Retrieve payment status for owned orders.
3. `POST /api/payments/{paymentId}/confirm` — Optional manual confirmation route if wallet/redirect flows require it.

### Admin Endpoints

1. `GET /api/payments` — Filter by status/date/order for ops dashboards.
2. `POST /api/payments/{paymentId}/refund` — Trigger full/partial refund.
3. `POST /api/payments/reconcile` — Force reconciliation pass with provider (also scheduled job).

### Webhooks / Internal

1. `POST /api/payments/webhooks/{provider}` — Endpoint to receive provider callbacks (Stripe signature validation, etc.).
2. `POST /internal/payments/capture` — Internal MassTransit consumer to capture upon shipment (optional future use).

## 4. Contracts & Messaging

- **Commands**
  - `CreatePaymentCommand` (Order Service → Payment Service) via HTTP.
  - `RefundPaymentCommand` (Order/Fulfillment → Payment Service) via MassTransit queue.
- **Events**
  - `IPaymentSucceeded` `{ paymentId, orderId, amount, currency, providerReference, timestamp }`
  - `IPaymentFailed` `{ paymentId, orderId, errorCode, reason }`
  - `IRefundIssued` `{ paymentId, refundId, amount, timestamp }`
- Add shared DTOs under `building-blocks/Contracts/Payments/` and register consumers/producers in `BuildingBlocks.csproj`.

## 5. Data Model Sketch

Tables (PostgreSQL + EF Core):

1. `Payments`
   - `Id (Guid)`
   - `OrderId (Guid)`
   - `Status (enum: Pending, RequiresAction, Succeeded, Failed, Refunded)`
   - `Amount (decimal)` / `Currency (string, ISO-4217)`
   - `Provider (string)`
   - `ProviderPaymentId (string)`
   - `ProviderClientSecret (string, nullable)`
   - `FailureCode/Message` fields
   - `CreatedAt`, `UpdatedAt`
2. `PaymentMethods` (future, for saving tokens)
3. `Refunds`
   - `Id (Guid)`
   - `PaymentId`
   - `Amount`
   - `Status`
   - `ProviderRefundId`
   - `CreatedAt`, `CompletedAt`
4. `WebhookEvents`
   - Raw payload + headers for replay/debugging.

## 6. Provider Integration Layer

- Create `IPaymentProvider` interface with methods `CreateIntent`, `Confirm`, `Capture`, `Refund`, `ParseWebhook`.
- First implementation `StripePaymentProvider` using Stripe .NET SDK.
- Support dependency injection via Polly-wrapped HTTP clients for resilience.
- External credentials via configuration: `PaymentProviders:Stripe:{ApiKey,WebhookSecret}`.

## 7. Operational Requirements

- **Configuration**: `appsettings.json` entries for DB, RabbitMQ, Redis (cache for idempotency tokens), Keycloak, external provider keys.
- **Logging**: Serilog sinks identical to other services + masking for PAN/token details.
- **Health Checks**: `/health/ready` verifying DB, RabbitMQ, Stripe connectivity.
- **Retries**: Exponential backoff for provider calls; outbox pattern for event publishing.
- **Docker**: Add `services/payment-service/Dockerfile` plus compose entry exposing port `5004` (local) → `8080` (in container).

## 8. Next Actions

1. Scaffold .NET API project (`dotnet new webapi`) under `services/payment-service` and reference `BuildingBlocks`.
2. Define contracts in `building-blocks/Contracts/Payments/` and update MassTransit registration.
3. Implement Stripe provider integration (sandbox keys via `.env`).
4. Wire Order Service to call Payment Service before moving orders to `Processing`.
5. Document API in Swagger and update API Gateway routes.
