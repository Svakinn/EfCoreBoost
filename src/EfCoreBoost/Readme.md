# EfCore.Boost

A disciplined **Unit of Work + Repository + Routine + Model Building**
layer for Entity Framework Core that makes database access
**structured**, **portable**, and **predictable** across **SQL Server**,
**PostgreSQL**, and **MySQL**.

EfCore.Boost does **not** replace Entity Framework Core.\
EF Core remains your ORM.

EfCore.Boost strengthens how EF Core is used in real-world, long-lived
systems.

------------------------------------------------------------------------

## What EfCore.Boost Is

EfCore.Boost is an architectural extension layer on top of EF Core.

It introduces a consistent and disciplined data-access model that helps
teams:

-   enforce clear boundaries around database access\
-   keep behavior predictable across database providers\
-   treat views and routines as first-class citizens\
-   support high-performance bulk ingestion\
-   expose safe and powerful OData queries\
-   maintain long-term portability

It is designed for systems that must grow without turning data-access
into chaos.

------------------------------------------------------------------------

## What EfCore.Boost Is Not

-   Not a replacement ORM\
-   Not a code generator\
-   Not a micro-ORM\
-   Not a "magic performance switch"

EfCore.Boost is about **architecture, discipline, and consistency**.

------------------------------------------------------------------------

## Architectural Model (at a glance)

Application\
→ Unit of Work (UOW)\
→ Repositories & Routines\
→ EF Core\
→ Database

Key ideas:

-   Applications talk to UOW, not DbContext\
-   Repositories expose tables and views\
-   Routines expose procedures and functions\
-   Provider differences are absorbed by conventions

This creates a stable and testable data-access boundary.

------------------------------------------------------------------------

## Why EfCore.Boost Exists

As systems grow, direct DbContext usage tends to spread everywhere:

-   inconsistent patterns\
-   duplicated logic\
-   provider-specific hacks\
-   fragile migrations\
-   unclear transaction boundaries

EfCore.Boost centralizes these concerns into a single, coherent layer.

You model **intent** in C#.\
EfCore.Boost applies **provider-correct behavior**.

------------------------------------------------------------------------

## Model Building & Cross-Provider Conventions

EfCore.Boost establishes one uniform model-building contract across
providers.

Instead of hand-tuning mapping rules per database, you define intent
once and let EfCore.Boost apply provider-specific behavior.

``` csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyEfBoostConventions(this, "log");
}
```

From that point:

-   naming and quoting are normalized\
-   identity/sequence behavior is handled\
-   string and datetime quirks are corrected\
-   routines and views follow a consistent pattern

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
