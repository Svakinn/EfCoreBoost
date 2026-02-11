# DbUOW.md  
## Database Unit of Work (EfBoost / DbRepo)

`DbUow<TContext>` is the foundational **Unit of Work** abstraction in EfBoost / DbRepo.  
It defines a controlled, structured, and powerful gateway between your .NET application and the database.

Instead of working directly with raw Entity Framework DbContexts, EfBoost introduces a **layered, structured access model**:
- Applications do **not** talk directly to DbContext
- Applications talk to a **DbUOW**
- The UOW decides
  - what parts of the database are available
  - what is readable or writable
  - what must stay protected
- Actual table / entity access happens through **Repository classes** (see: [DbRepo.md](../DbRepo/DbRepo.md))
- Specialized and high‑performance database logic flows through **Routines** (see: [DbUowRoutines.md](./DbUowRoutines.md))

This gives clarity, safety, better architecture boundaries, and powerful features beyond normal EF.

Put simply:

> **This is the controlled entry point between your .NET application and the database.  
> Everything meaningful passes through a UOW.**

---

## Purpose

A UOW exists to:

- Represent a single logical database session  
- Define *what* parts of the database are accessible by Exposing repositories and routine helpers
- Control whether things are:
  - Read-only  
  - Read/Write  
  - Or not exposed at all  
- Provide convenient helpers where EF alone is not ideal  
- Coordinating SaveChanges and transactions
- Enable advanced scenarios through routines  
- Provider-agnostic behavior (SQL Server, PostgreSQL, MySQL)
- Safe retry semantics for cloud databases

UOW is not just plumbing.  
It is the **guardian, rulebook, and control center** for database communication.

---

## Sync vs Async API

The DbUOW exposes both synchronous and asynchronous APIs.

Naming conventions:

- Async methods end with `Async`
- Synchronous methods end with `Synchronized`

Example:

```csharp
await uow.SaveChangesAsync(ct);
uow.SaveChangesSynchronized();
```

Guideline:

- Prefer async APIs in WebAPI, background services, and I/O-bound workflows
- Synchronous APIs exist for legacy, tooling, and batch scenarios

Naming convention:

| Pattern | Meaning |
|--------|--------|
| `Async` methods | Non-blocking async |
| `Syncronized` suffix | Explicit synchronous variant |

---

## Construction Model

Concrete UOWs declare how to securely create their DbContext using a provided secure factory.

Example:

```csharp
public partial class UOWTestDb(IConfiguration cfg, string cfgName) : UowFactory<DbTest>(cfg, cfgName)
{
}
```

### Key Characteristics

- Constructor receives:
  - `IConfiguration`
  - Logical connection name
- The factory takes care of:
  - Provider handling (SQL Server / PostgreSQL / MySQL)
  - Handle connections including Azure and Managed Identity behavior
- Keeps configuration responsibility centralized and safe

Your application simply requests a UOW, and the right database connection is in play.

---

## Provider Awareness

A UOW is transparently aware of which database engine is being used.

Supported engines:

- SQL Server  
- PostgreSQL  
- MySQL  

The UOW ensures higher-level APIs stay consistent even when individual engines differ under the hood.

---

## Saving & Lifecycle

### Standard Commit
Normal persistence:

```csharp
await SaveChangesAsync();
```

### Resetting Commit
For large workloads, bulk operations, or very long running contexts:

```csharp
await SaveChangesAndNewAsync();
```

This allows you to continue working without DbContext tracking buildup.

The UOW follows predictable disposal semantics, ensuring connections are properly closed.

> Note:
> `SaveChangesAndNewAsync()` and `SaveChangesAndNewSynchronized()`
> cannot be used while a transaction is active on the UOW instance.

---

## Transactions

The UOW provides a **transaction envelope** rather than exposing raw transaction handles.

This design:

- Uses EF Core execution strategies (retry-safe, Azure-friendly)
- Ensures consistent commit / rollback behavior
- Prevents accidental nested transactions on the same UOW instance

### Async transaction envelope (recommended)

```csharp
await uow.RunInTransactionAsync(async ct =>
{
    // repository and routine calls
    await uow.SaveChangesAsync(ct);
}, IsolationLevel.ReadCommitted, ct);
```

### Synchronized transaction envelope

```csharp
uow.RunInTransactionSynchronized(() =>
{
    // repository and routine calls
    uow.SaveChangesSynchronized();
}, IsolationLevel.ReadCommitted);
```

Behavior:

- Only one active transaction is allowed per UOW instance
- If an exception escapes the work delegate, the transaction is rolled back
- On success, the transaction is committed automatically

---

## Nested Work and Bulk Operations

Repositories and bulk operations automatically adapt to the transaction context:

- If a transaction is already active on the DbContext, operations participate in it
- Otherwise, operations may create an internal transaction as needed

This allows patterns such as:

```csharp
await uow.RunInTransactionAsync(async ct =>
{
    await uow.LogEntries.BulkInsertAsync(items, ct: ct);
    await uow.SaveChangesAsync(ct);
}, ct: ct);
```

---

## Repository Access

Repositories are the primary way to work with tables and views.

A UOW exposes repository instances such as:

```csharp
public IAsyncRepo<LoginLog> LoginLogs => new EfRepo<LoginLog>(Ctx!, DbType);
```

Repositories bring significant power:

- Clean query API  
- Optional strongly-typed constraints  
- Dictionary-style access patterns  
- Built-in conventions  

And importantly:

- **OData query shaping support**  
- **Bulk insert support**  

A separate document dives deeper into repo powers:

📄 [DbRepo.md](./DbRepo/DbRepo.md) **– Repository Capabilities, OData & Bulk Operations**

The philosophy is:

> Application code talks to repositories.  
> UOW decides which repositories exist and how they behave.  
> DbContext stays behind the curtain.

---

## Routines: Beyond Plain Table Access

UOW also provides structured access to routines (procedures + functions).

Why routines?

- Highly optimized lookups  
- Hierarchical data evaluation  
- Encapsulated logic  
- Better performance than huge LINQ expressions  
- Efficient sequence usage  
- Engine-native execution paths  

UOW offers scalar, tabular, and non-query routine helpers and keeps them cross-platform safe.

See details here:

📄 [DbUowRoutines.md](./DbUowRoutines.md) **– Routine Execution & Design Guidelines**

---

## Configuration

UOWs bind to named database configuration entries:

```json
"DBConnections": {
  "Logs": {
    "ConnectionString": "...",
    "UseAzure": false,
    "UseManagedIdentity": false,
    "AzureTenantId": "",
    "AzureClientId": "",
    "AzureClientSecret": ""
  }
}
```

Centralized. Secure. Consistent.   
See: [Configs.md](../CFG/Configs.md)

---

## Example UOW

### Definition

```csharp
public partial class UOWLogs(IConfiguration cfg) : UowFactory<DbLogs>(cfg, "Logs") 
{
    public IAsyncRepo<LoginLog> LoginLogs => new EfRepo<LoginLog>(Ctx!, DbType);
    public IAsyncRepo<SessionLog> SessionLogs => new EfRepo<SessionLog>(Ctx!, DbType);

    // Example tabular routine exposure
    public IQueryable<MyViewData> GetMyViewData(long sessionId) =>
        RunRoutineQuery<MyViewData>("my", "GetMyViewData", [ new("@SessionId", sessionId) ]);
}
```

### Usage

```csharp
using var uow = new UOWLogs(configuration);

// Repository usage
var recent = await uow.LoginLogs.Query()
    .OrderByDescending(x => x.CreatedUtc)
    .Take(20)
    .ToListAsync();

// Tabular routine
var viewData = await uow.GetMyViewData(sessionId)
    .AsNoTracking()
    .ToListAsync();
```

The UOW defines the playground.  
Code plays safely inside it.

## Thread Safety

A UOW instance is **not concurrency-safe**. This is a direct consequence of EF Core’s `DbContext`,
which does not support concurrent operations.

Do **not** use the same UOW instance from multiple parallel operations
(e.g. `Task.WhenAll`, background tasks, or overlapping async calls).

If you need parallelism, create a separate UOW instance per operation.
Using multiple UOW instances at the same time is quite safe.

---

## About Model Building & Migrations

EfBoost also includes helpers related to model building and migrations, supporting:

- Multi-provider alignment  
- Structure consistency  
- Improved developer workflow  

However, that belongs to a dedicated topic:📄 [ModelBuilding.md](../Model/ModelBuilding.md)

---

## 🎯 Summary

`DbUow` is:

- The controlled **gateway** to the database  
- The authority defining what data can be accessed  
- The foundation for Repository access  
- The bridge to extremely powerful routine execution  
- Transaction capable  
- Sync + Async capable  
- Provider aware  
- Ready for real-world workloads  

EfBoost replaces “naked DbContext access” with a structured, layered, safer approach.  
And this class is the heart of that design.
