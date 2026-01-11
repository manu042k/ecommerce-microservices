# Cross-Cutting Requirements for New Services

Applies to Payment, Inventory, and Fulfillment/Shipping services unless noted otherwise.

## 1. Architecture & Contracts

- **Building Blocks**: Extend `building-blocks/Contracts` with new folders (`Payments`, `Inventory`, `Fulfillment`). All services reference shared message contracts through `BuildingBlocks.csproj` to prevent drift.
- **MassTransit**: Register new consumers/publishers in each service and ensure `BuildingBlocks` exposes helper extensions for consistent configuration (retries, concurrency, outbox).
- **Event Versioning**: Adopt explicit version fields or topic names (e.g., `payment-succeeded.v1`) to enable evolution without breaking consumers.

## 2. API Gateway & Routing

- Update `api-gateway` configuration to route `/api/payments`, `/api/inventory`, and `/api/fulfillment` paths to their respective downstream services with JWT validation scopes.
- Ensure Swagger UIs for new services are exposed via gateway-friendly OAuth client (re-use `swagger-ui` Keycloak client).

## 3. Security & Identity

- **Keycloak Realm**: Add confidential clients for `payment-service`, `inventory-service`, and `fulfillment-service` with service accounts for inter-service calls.
- Define role requirements:
  - Payment: `Admin`, `Customer` scopes.
  - Inventory: `Admin`, `Ops` (new role).
  - Fulfillment: `Admin`, `FulfillmentAgent` (new role) plus `Customer` read-only endpoints.
- Configure scope mappings in API Gateway policies to enforce least privilege.

## 4. Observability & Logging

- Standardize Serilog sinks (console + file under `/app/logs/<service>`). Include correlation IDs (`X-Correlation-ID`) propagated from API Gateway.
- Metrics: expose Prometheus-compatible endpoint `/metrics` with counters (requests, events), histograms (latency), and gauges (queue depth, stock levels).
- Tracing: ensure OpenTelemetry exporters (OTLP endpoint configurable) attach service.name tags.

## 5. Resilience & State Management

- Use PostgreSQL for primary persistence with EF Core migrations per service; maintain `Migrations` folders and update `deploy/docker-compose.yml` with new DB containers if isolation per service is desired.
- Employ Redis for caching/idempotency tokens; namespaced keys (`payment:*`, `inventory:*`, `fulfillment:*`).
- Apply retry + circuit breaker policies (Polly) for external dependencies (Stripe, Shippo, etc.).

## 6. Infrastructure & Deployment

- Extend `deploy/docker-compose.yml` to include three new service containers, databases, and any provider-specific sidecars (e.g., ngrok for webhook testing optional).
- Provide `.env` entries documenting required secrets and sample values.
- Update CI/CD workflows (GitHub Actions/Azure DevOps) to build, test, and publish docker images for each service.

## 7. Testing Strategy

- **Unit Tests**: cover domain logic (payment status transitions, reservation rules, shipment workflows).
- **Contract Tests**: ensure event schemas remain backward compatible (e.g., using `MassTransit.Testing` + snapshots).
- **Integration Tests**: spin up minimal docker compose subset (service + DB + RabbitMQ) for key flows.
- **End-to-End Smoke**: scenario tests orchestrating Order → Payment → Inventory → Fulfillment once all services exist.

## 8. Documentation & Developer Experience

- Each service README must include setup instructions, environment variables, and Swagger endpoint references.
- Update root `README.md` with architecture diagram featuring new services and their interactions.
- Provide API collection updates (HTTP files or Postman) to cover Payment, Inventory, and Fulfillment endpoints.
