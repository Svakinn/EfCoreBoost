
# EfCore.Boost 

A disciplined **Unit of Work + Repository + Routine + Model Building** layer for Entity Framework Core that makes database access **structured**, **portable**, and **powerful** across **SQL Server**, **PostgreSQL**, and **MySQL**.

EfCore.Boost isn’t here to replace EF Core. It is designed for systems that require any of the following:

- structured database access boundaries  
- predictable behavior across SQL Server, PostgreSQL, and MySQL  
- safe and powerful OData  
- high-performance bulk ingestion  
- first-class handling of views and routines  
- consistent transactional semantics  
- architecture that remains maintainable as systems grow  

EF Core remains the ORM.  
EfBoost strengthens how EF is used in real applications.

------------------------------------------------------------------------

## What EfCore.Boost Is

EfCore.Boost is an architectural extension layer on top of EF Core.

It simplifies model building and cross-platform conventions, and introduces a disciplined Unit-of-Work model for accessing tables, views, and stored procedures or functions.

EfCore.Boost is designed for systems that must grow without turning data access into chaos.

It is about **architecture, discipline, and consistency**.

------------------------------------------------------------------------

## Additional Capabilities

In addition to its core architectural model, EfCore.Boost provides several focused tools commonly needed in production systems:

- **Strong and natural OData support**  
  Expressive, safe query composition that integrates cleanly with EF Core while remaining provider-correct.

- **Simple, Azure-safe transaction model**  
  Explicit transactional boundaries designed to behave correctly under retries and transient failures.

- **Simple and powerful bulk inserts**  
  High-throughput ingestion helpers that operate within the same Unit-of-Work and transaction model.

------------------------------------------------------------------------

## Architectural Model (at a glance)

Application
→ Unit of Work (UOW)
→ Repositories & Routines
→ EF Core
→ Database

Key ideas:

-   Applications talk to UOW, not DbContext
-   Repositories expose tables and views
-   Routines expose procedures and functions
-   Provider differences are absorbed by conventions

This creates a stable and testable data-access boundary.

------------------------------------------------------------------------

## Why EfCore.Boost Exists

As systems grow, direct DbContext usage tends to spread everywhere:

-   Inconsistent patterns
-   Duplicated logic
-   Provider-specific hacks
-   Fragile migrations
-   Unclear transaction boundaries

EfCore.Boost centralizes these concerns into a single, coherent layer.

You model **intent** in C#.
EfCore.Boost applies **provider-correct behavior**.  
The idea is to simplify everyday creation and handling of database models.

------------------------------------------------------------------------

## Model Building & Cross-Provider Conventions

EfCore.Boost establishes one uniform model-building contract across
providers.

Instead of hand-tuning mapping rules per database, you define intent
once and let EfCore.Boost apply provider-specific behavior.

``` csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyEfBoostConventions(this, "log");
}
```

From that point:

-   Naming and quoting are normalized
-   Identity/sequence behavior is handled
-   String and datetime quirks are corrected
-   Routines and views follow a consistent pattern

The model remains portable.

------------------------------------------------------------------------

## Installation

``` bash
dotnet add package EfCore.Boost
```

------------------------------------------------------------------------

## Documentation & Source

Full documentation, guides, and examples:

https://github.com/Svakinn/EfCoreBoost/

------------------------------------------------------------------------

## License

MIT

------------------------------------------------------------------------

Actively developed.
