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
- **OData Support**: Demonstrates how effortlessly powerful data querying can be. The OData standard is quite effective for serving data, allowing clients to perform complex filtering, sorting, and paging without any extra backend development. With **EfCore.Boost**, you get the full power of OData expressions translated directly to efficient SQL, without the overhead of setting up a formal OData service. This "best-of-both-worlds" approach keeps your API lightweight while providing maximum flexibility to consumers.

### Why OData?

- **Dynamic Querying**: Clients can request exactly the data they need (e.g., `?filter=Processed eq false&orderby=LastChangedUtc desc&top=10`).
- **Reduced Backend Work**: No need to write custom filters for every field or combination of fields.
- **Performance**: Expressions are translated to IQueryable and executed on the database level, ensuring only the necessary records are fetched.
- **Flexibility**: Perfect for powering data-heavy frontends and grids that require advanced searching and sorting.

### OData Query Example

In `IpLogic.cs`, the `ListIps` method shows how to apply OData query options directly to a repository query with just one line of code:

```csharp
public async Task<QueryResult<BoostCTX.IpInfoView>> ListIps(ODataQueryOptions<BoostCTX.IpInfoView> options, CancellationToken ct)
{
    // Base query: you can still enforce security or business rules here
    var baseQuery = UoW.IpInfoViews.QueryUnTracked().Where(tt => tt.Id > 0);

    // One-liner to apply client-side filtering, sorting, and paging
    return await UoW.IpInfoViews.FilterODataAsync(baseQuery, options, null, false, ct);
}
```
