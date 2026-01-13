# Identity Service

Service responsible for user authentication, registration, and identity management. Integrates with Keycloak for OAuth2/JWT token management and provides user management endpoints.

## Overview

The Identity Service acts as the authentication and authorization layer for the ecommerce platform. It handles user registration, login, and delegates token management to Keycloak while providing a unified API interface.

## Features

- User registration
- User login and authentication
- JWT token management via Keycloak
- User profile management
- Integration with Keycloak Admin API

## Technology Stack

- **Framework**: .NET 8.0
- **Authentication**: Keycloak (OAuth2/JWT)
- **HTTP Client**: For Keycloak API integration
- **Logging**: Serilog

## API Endpoints

| Method | Route                    | Description         | Auth Policy   |
| ------ | ------------------------ | ------------------- | ------------- |
| `POST` | `/api/identity/register` | Register new user   | Anonymous     |
| `POST` | `/api/identity/login`    | User login          | Anonymous     |
| `GET`  | `/api/identity/profile`  | Get user profile    | Authenticated |
| `PUT`  | `/api/identity/profile`  | Update user profile | Authenticated |

## Authentication Flow

1. User registers via `/api/identity/register`
2. User logs in via `/api/identity/login` (returns JWT token)
3. Client includes JWT token in `Authorization: Bearer <token>` header
4. Service validates token with Keycloak

## Configuration

Key settings in `appsettings.json`:

```json
{
  "Keycloak": {
    "AuthServerUrl": "http://keycloak:8080/",
    "Realm": "ecommerce-realm",
    "Resource": "identity-client",
    "Credentials": {
      "Secret": "your-client-secret"
    },
    "AdminClientId": "admin-cli",
    "AdminClientSecret": "your-admin-client-secret"
  },
  "Swagger": {
    "AuthorizationUrl": "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/auth",
    "TokenUrl": "http://localhost:8080/realms/ecommerce-realm/protocol/openid-connect/token",
    "ClientId": "identity-client",
    "ClientSecret": "your-client-secret"
  }
}
```

## Keycloak Integration

The service integrates with Keycloak using:

- **Admin API**: For user management operations
- **Token Endpoint**: For authentication
- **User Info Endpoint**: For profile retrieval

### Required Keycloak Setup

1. Create a realm: `ecommerce-realm`
2. Create a client: `identity-client` (confidential client)
3. Configure client credentials
4. Set up user roles: `Admin`, `User`, `Customer`

## Local Development

1. Start Keycloak:

```bash
cd deploy
docker-compose up -d keycloak
```

2. Configure Keycloak realm (see `deploy/keycloak/README.md`)

3. Run the service:

```bash
cd services/identity-service
dotnet run
```

4. Access Swagger UI:

- Direct: http://localhost:5001/swagger
- Via Gateway: http://localhost:5050/swagger

## Testing

Run tests:

```bash
cd services/identity-service
dotnet test
```

Test coverage includes:

- Authorization policy tests
- CORS policy tests
- Swagger configuration tests

## Docker Deployment

Build and run:

```bash
docker build -f services/identity-service/Dockerfile -t ecommerce/identity-service .
docker run -p 5001:8080 ecommerce/identity-service
```

Or use Docker Compose (see `deploy/docker-compose.yml`).

## Security Considerations

- Passwords are handled by Keycloak (never stored in this service)
- JWT tokens are validated on each request
- HTTPS should be used in production
- Client secrets should be stored securely (environment variables, secrets manager)

## User Roles

The service supports the following roles (defined in Keycloak):

- **Admin**: Full system access
- **User**: Standard user access
- **Customer**: Customer-specific access

## Logging

Structured logging with Serilog:

- Console output
- File output: `/app/logs/identity-service/`
- Request/response logging
- Authentication events

## Integration with Other Services

All other services depend on the Identity Service for:

- User authentication
- JWT token validation
- User role verification

The API Gateway validates tokens and forwards user claims to downstream services.
