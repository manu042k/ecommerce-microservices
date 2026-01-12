# Payment Service

Handles payment intents, refunds, and gateway callbacks for the ecommerce stack. Built with ASP.NET Core 8, EF Core (PostgreSQL), MassTransit/RabbitMQ, and Keycloak-secured endpoints.

## Features

- Create/confirm payment intents via abstractions over Stripe (`IPaymentProvider`).
- Persist payments, refunds, and webhook payloads for auditing.
- Publish shared events (`IPaymentSucceeded`, `IPaymentFailed`, `IRefundIssued`) through MassTransit for downstream services.
- Finance-only administration endpoints plus general customer policies enforced through Keycloak realm roles.
- Webhook receiver that validates Stripe signatures (when configured) and reconciles local state.

## HTTP API Surface

| Endpoint                                 | Auth Policy                 | Description                                                         |
| ---------------------------------------- | --------------------------- | ------------------------------------------------------------------- |
| `POST /api/payments`                     | `CustomersOrAdmin`          | Create a payment intent and return provider metadata/client secret. |
| `GET /api/payments/{id}`                 | Authenticated               | Retrieve a single payment with refund history.                      |
| `GET /api/payments`                      | `FinanceOrAdmin`            | Filter payments by order, status, or date window.                   |
| `POST /api/payments/{id}/confirm`        | `CustomersOrAdmin`          | Force confirmation for wallets/3DS flows.                           |
| `POST /api/payments/{id}/refund`         | `FinanceOrAdmin`            | Trigger partial/full refunds.                                       |
| `POST /api/payments/webhooks/{provider}` | Anonymous (provider-signed) | Receive gateway callbacks (Stripe).                                 |
| `POST /internal/payments/capture`        | Service-to-service          | Capture/confirm settlements from Fulfillment/Order services.        |

## Configuration

- **PostgreSQL:** `ConnectionStrings:DefaultConnection` (defaults to `payment-db`).
- **Redis:** optional cache/idempotency store via `ConnectionStrings:Redis` + `Redis:InstanceName`.
- **RabbitMQ:** `RabbitMQ:{Host,UserName,Password}` for MassTransit.
- **Keycloak:** `Keycloak:{AuthServerUrl,Realm,Resource}`; policies expect `Admin`, `Finance`, `Customer`, or `User` roles.
- **Stripe:** set `PaymentProviders:Stripe:{ApiKey,WebhookSecret}`. When omitted, the provider runs in simulated mode for local testing.

## Local Development

1. Run Postgres/Redis/RabbitMQ via `docker compose up payment-service payment-db redis rabbitmq keycloak`.
2. Configure Keycloak realm client secret `payment-secret`; assign roles to the admin/test users.
3. Launch the service: `dotnet run --project services/payment-service/PaymentService.csproj`.
4. Use `PaymentService.http` for quick smoke tests (requires a Bearer token from Keycloak).

## Next Steps

- Add real provider webhooks for refund/reconciliation events beyond payment intents.
- Implement scheduled reconciliation jobs and dead-letter handling for failed events.
- Wire Order Service to call the `POST /api/payments` endpoint before transitioning orders into `Processing`.
