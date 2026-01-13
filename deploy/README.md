# Deployment Configuration

This directory contains Docker Compose configuration and deployment-related files for the ecommerce microservices platform.

## Contents

- `docker-compose.yml`: Main Docker Compose file for all services
- `keycloak/`: Keycloak realm configuration and setup documentation

## Quick Start

### Start All Services

```bash
docker-compose up -d
```

This will start:

- Keycloak (port 8080)
- All microservices (ports 5001-5006)
- API Gateway (port 5050)
- PostgreSQL databases for each service
- Redis (port 6379)
- RabbitMQ (ports 5672, 15672)

### Stop All Services

```bash
docker-compose down
```

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f api-gateway
```

## Service Ports

| Service             | Port      | Description                    |
| ------------------- | --------- | ------------------------------ |
| Keycloak            | 8080      | Identity and access management |
| Identity Service    | 5001      | User authentication            |
| Catalog Service     | 5002      | Product catalog                |
| Order Service       | 5003      | Order management               |
| Payment Service     | 5004      | Payment processing             |
| Inventory Service   | 5005      | Inventory management           |
| Fulfillment Service | 5006      | Shipment management            |
| API Gateway         | 5050      | Central API entry point        |
| RabbitMQ Management | 15672     | Message broker UI              |
| PostgreSQL          | 5432-5436 | Database instances             |

## Database Configuration

Each service has its own PostgreSQL database:

- `catalog-db`: Catalog Service database
- `order-db`: Order Service database
- `inventory-db`: Inventory Service database
- `payment-db`: Payment Service database
- `fulfillment-db`: Fulfillment Service database

Database credentials are defined in `docker-compose.yml` environment variables.

## Environment Variables

Key environment variables can be set in `docker-compose.yml` or via `.env` file:

- `KEYCLOAK_CLIENT_SECRET`: Keycloak client secret
- `STRIPE_API_KEY`: Stripe API key for payment service
- `STRIPE_WEBHOOK_SECRET`: Stripe webhook secret

## Keycloak Setup

See `keycloak/README.md` for detailed Keycloak configuration instructions.

The Keycloak realm is automatically imported from `keycloak/realm-export.json` on startup.

## Volumes

Docker volumes are created for:

- Database data persistence
- Service logs (mounted to `./logs/<service-name>/`)

## Building Services

To rebuild services after code changes:

```bash
docker-compose up --build
```

To rebuild a specific service:

```bash
docker-compose build api-gateway
docker-compose up -d api-gateway
```

## Health Checks

Services include health check endpoints. Monitor service health:

```bash
# Check service status
docker-compose ps

# Check specific service health
curl http://localhost:5050/health
```

## Production Considerations

For production deployment:

1. **Secrets Management**: Use Docker secrets or external secrets manager
2. **Networking**: Configure proper network isolation
3. **Resource Limits**: Set CPU and memory limits for containers
4. **Persistent Storage**: Use named volumes or external storage
5. **Monitoring**: Integrate with monitoring solutions (Prometheus, Grafana)
6. **Logging**: Configure centralized logging (ELK, Loki)
7. **SSL/TLS**: Enable HTTPS with proper certificates
8. **Backup**: Set up database backup strategies

## Troubleshooting

### Services Not Starting

1. Check logs: `docker-compose logs <service-name>`
2. Verify ports are not in use
3. Check database connectivity
4. Verify Keycloak is running

### Database Connection Issues

1. Ensure database containers are running: `docker-compose ps`
2. Check connection strings in service configuration
3. Verify database credentials match docker-compose.yml

### Keycloak Issues

1. Check Keycloak logs: `docker-compose logs keycloak`
2. Verify realm import was successful
3. Check client configuration in Keycloak admin console

### Message Broker Issues

1. Check RabbitMQ management UI: http://localhost:15672
2. Verify queues and exchanges are created
3. Check service logs for connection errors

## Development vs Production

The current `docker-compose.yml` is configured for development:

- Uses development environment variables
- Exposes all ports
- Includes Swagger UI
- Uses development database settings
