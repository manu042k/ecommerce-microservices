# Building Blocks

Shared contracts, utilities, and extensions used across all microservices in the ecommerce platform.

## Overview

The Building Blocks project provides common functionality to ensure consistency across services:

- **Contracts**: Message contracts for MassTransit (events, commands)
- **Logging**: Serilog configuration extensions
- **Shared DTOs**: Common data transfer objects

## Project Structure

```
building-blocks/
├── BuildingBlocks/
│   ├── Contracts/          # Message contracts
│   │   ├── Fulfillment/
│   │   └── Payments/
│   └── Logging/            # Logging extensions
└── BuildingBlocks.Tests/   # Unit tests
```

## Contracts

Message contracts define the structure of events and commands used for inter-service communication via RabbitMQ/MassTransit.

### Fulfillment Contracts

Located in `Contracts/Fulfillment/ShipmentContracts.cs`:

- `IShipmentCreated`
- `IShipmentScheduled`
- `IShipmentStatusChanged`
- `IShipmentFailed`

### Payment Contracts

Located in `Contracts/Payments/PaymentEvents.cs`:

- `IPaymentSucceeded`
- `IPaymentFailed`
- `IRefundIssued`

## Logging Extensions

The `LoggingExtensions` class provides standardized Serilog configuration:

### Usage

```csharp
// In Program.cs
builder.AddCustomLogging();

// In middleware pipeline
app.UseCustomLogging();
```

### Features

- Configuration from `appsettings.json`
- Console output
- Request logging middleware
- Machine name enrichment
- Log context enrichment

## Adding New Contracts

When adding new message contracts:

1. Create appropriate folder structure in `Contracts/`
2. Define interfaces following MassTransit conventions
3. Use record types or classes with public properties
4. Include version information if needed for evolution
5. Update this README with contract documentation

Example:

```csharp
namespace BuildingBlocks.Contracts.Orders;

public interface IOrderCreated
{
    Guid OrderId { get; }
    string UserId { get; }
    decimal TotalAmount { get; }
    DateTime Timestamp { get; }
}
```

## Testing

The test project includes unit tests for logging extensions and contract validation. Run tests:

```bash
cd building-blocks
dotnet test
```

## Versioning

Contracts should be versioned to support evolution:

- Use explicit version fields: `Version = 1`
- Use topic names: `order-created.v1`
- Maintain backward compatibility when possible

## Best Practices

- Keep contracts simple and focused
- Avoid service-specific dependencies
- Use value types and simple objects
- Document contract changes
- Share contracts through NuGet packages in production (optional)

## Integration

All services reference this project:

```xml
<ItemGroup>
  <ProjectReference Include="../../building-blocks/BuildingBlocks/BuildingBlocks.csproj" />
</ItemGroup>
```

This ensures all services use the same contract definitions and prevents message drift.
