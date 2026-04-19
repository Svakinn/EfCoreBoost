# BoostX.Api

This project is a demonstration of how to implement and use the **Unit of Work (UOW)** pattern within a .NET application using **EfCore.Boost**.

## Purpose

The main goal of this project is to showcase:
- How to inject a custom Unit of Work Factory (`IUowBoostXFactory`) into the Dependency Injection (DI) container.
- How to use the factory to create UOW instances in a convenient, lazy-loading manner within business logic services.
- Best practices for keeping your controllers thin and your business logic decoupled from database implementation details.

> **Note:** The examples provided (e.g., IP address tracking and background processing) are not necessarily practical for a production environment. They are strictly for demonstrating the preferred usage of the Unit of Work within a .NET application.

## Dependency Injection

The `IUowBoostXFactory` is registered as a singleton in `Program.cs`. This allows the application to create UOW instances whenever needed, ensuring that each unit of work is appropriately scoped to its task (especially important in background workers or multi-threaded scenarios).

### Program.cs Registration

```csharp
// Inject our UoW-Factory and business logic into the DI container
builder.Services.AddSingleton<IUowBoostXFactory, UowBoostXFactory>();
builder.Services.AddScoped<IpLogic>();
builder.Services.AddHostedService<IpBackgroundWorker>();
```

## Lazy Loading Usage

In the `IpLogic` service, we demonstrate a "Lazy Init" pattern. The UOW is not created until it is actually needed, and it is reused within the scope of the service instance.

### IpLogic.cs Implementation

```csharp
public sealed class IpLogic(IUowBoostXFactory uowBoostXFactory)
{
    private readonly IUowBoostXFactory _uowBoostXFactory = uowBoostXFactory;
    private BoostXUow? _uowBoostX;

    // Lazy init of the UoW
    private BoostXUow UoW => _uowBoostX ??= _uowBoostXFactory.Create();

    public async Task<IpDto> GetRandomIp()
    {
        var ip = DemoIpCatalog.GetRandomIp();
        
        // Use the lazy-loaded UoW
        var ipId = await UoW.GetIpId(ip);
        // ...
    }
}
```

## Features

- **IpInfoController**: Provides endpoints for retrieving and ensuring IP records.
- **IpBackgroundWorker**: A background service that demonstrates using the `IUowBoostXFactory` to process records on a timer (every 5 minutes).
- **OData Support**: Demonstrates how easily OData filtered queries can be integrated with the UOW repositories.
