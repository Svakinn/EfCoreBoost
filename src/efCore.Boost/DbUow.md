# DbUOW.md

## Purpose

`DbUOW` (Database Unit of Work) is a base class designed to manage interaction with an `Entity Framework Core` database context, including lifecycle management, raw SQL execution, and common utility functions. It is intended to be extended by specialized Unit of Work implementations that encapsulate business logic and data access patterns in a structured and consistent way.

It promotes clean architecture by:
- Centralizing creation and disposal of the `DbContext`.
- Offering a unified place for executing raw ADO.NET commands and stored procedures.
- Supporting database-agnostic operations (SQL Server, PostgreSQL, MySQL, etc.).
- Enabling metadata discovery for OData and API contracts.
- Supporting EF Core-compatible async operations only (no sync overloads).
- Supports both direct db-connections via connection string and Azure speciffic ones
---

## Key Features

### 🧱 `DbContext` Management
- `CreateDbContext()` is a virtual method that must be implemented by derived classes to initialize the `Ctx` property.
- Automatically detects and caches the database provider (`DatabaseType`).
- Implements the `IDisposable` pattern to ensure proper resource cleanup.

### 🧠 EF Core Metadata
- `Metadata<T>()` and `GetModel<T>()` extract EDM models for OData support.
- `SetCommandTimeout()` lets you override EF's default command timeout.

### 🛠️ Raw SQL Utilities
- `ExecSqlRawAsync(...)` executes SQL commands directly via EF Core’s `Database.ExecuteSqlRawAsync`.
- Scalar readers (e.g. `GetLongScalarAsync`, `GetStringScalarAsync`) simplify getting single values.
- List readers (e.g. `GetLongListAsync`, `GetStringListAsync`) support multi-row reads.

### 🌀 Stored Procedure Support
- `ExecStoredProcToLongListAsync(...)` and `ExecStoredProcToStringListAsync(...)` allow calling stored procedures with parameters using ADO.NET directly.
- Parameters are passed via a simple DTO: `DbParmInfo { Name, ObjValue }`.

### 🔄 Save Semantics
- `SaveChangesAsync()` is the standard async commit.
- `SaveChangesAndNewAsync()` allows for large data operations where the `DbContext` is disposed and recreated to reset internal state and avoid memory growth or tracking bloat.

### 🔐 SQL Exception Flattening
- `SqlExceptionMessages(...)` extracts nested SQL Server error details for logging and diagnostics.

---

## Design Philosophy

This class:
- **Encourages extension**: meant to be subclassed with specific repositories or domain logic.
- **Avoids sync methods**: all I/O is `async`, promoting scalability and preventing thread pool starvation.
- **Plays well with EF Core**: adheres to EF conventions while providing raw access where EF falls short.
- **Avoids overreach**: leaves repository definitions (`EfRepo`, etc.) to be composed separately.

---

## Intended Usage

A typical usage looks like this:
- Extending the base class
- Overriding the CreteDbContext to get the db-connection
- Adding repositories to the UOW, we want to access (for tables/views defined in the dbcontext)

```csharp
public class MyAppUow : DbUOW
{
    public MyAppUow(IConfiguration cfg, string name) : base(cfg, name) { }

    protected override void CreateDbContext()
    {
        this.Ctx = SecureContextFactory.CreateDbContext<DbLogs>(this.Cfg, DbCfgName);
    }

    public IAsyncRepo<Customer> Customers => new EfRepo<Customer>(Ctx!, DbType);
}
```

Passed in is the app global configuration and the config name for the connection

```csharp
  _uow = new UOWTxt(config, "MyDatabase");
```
Where the ""MyDatabase"" stands for the connectin definition name in the config.

```json
"DBConnections": {
  "MyDatabase": {
    "ConnectionString": "Server=localhost;Database=Svak2;Integrated Security=True",
    "UseAzure": false,
    "UseManagedIdentity": "false",
    "AzureTenantId": "",
    "AzureClientId": "",
    "AzureClientSecret": ""
  }
}
```

More details about db-configurations are found in the Configs.md file.