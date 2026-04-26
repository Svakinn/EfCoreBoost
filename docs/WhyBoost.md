# Why EfCore.Boost?

This document explains the key challenges commonly encountered when using EF Core in real-world database projects, and how EfCore.Boost addresses them.

EfCore.Boost addresses a set of common challenges in EF Core-based systems, making it particularly effective for database-driven applications.

The table below summarizes these areas. Each point highlights a typical issue in EF Core usage and the corresponding approach taken by EfCore.Boost.

Further sections in this document expand on each topic in more detail.

| #              | Area                        | Typical EF Core Challenge                                      | EfCore.Boost Approach                                                     |
|----------------|-----------------------------|----------------------------------------------------------------|----------------------------------------------------------------------------|
| [1](#link-1)   | [Structure](#link-1)                | DbContext grows to handle too many responsibilities            | Clear Unit of Work boundaries and structured data access                   |
| [2](#link-2)   | [Multi-Provider](#link-2)            | Behavior differs across SQL Server, PostgreSQL, and MySQL      | Provider-aware conventions and consistent mappings                         |
| [3](#link-3)   | [Migrations](#link-3)                | Managing migrations across environments and providers is fragile | Controlled and streamlined migration workflows with script generation      |
| [4](#link-4)   | [Bulk Operations](#link-4)           | High-volume inserts and updates require custom solutions       | Built-in patterns for efficient bulk handling                              |
| [5](#link-5)   | [OData](#link-5)                       | Query exposure can become unsafe or inconsistent               | Controlled and predictable OData integration                               |
| [6](#link-6)   | [Database Features](#link-6)            | Views, routines, and raw SQL are awkward to integrate cleanly  | First-class support for database-native constructs                         |
| [7](#link-7)   | [Transactions](#link-7)                | Cross-provider transaction behavior can vary                   | Consistent transactional patterns across providers                         |
| [8](#link-8)   | [Maintainability](#link-8)             | Patterns drift over time across teams and projects             | Enforced conventions and predictable structure                             |
| [9](#link-9)   | [Model Definition](#link-9) | Fluent configuration becomes complex and fragmented            | Attribute-driven conventions simplify and clarify model definitions        |
| [10](#link-10) | [DbContext Usage](#link-10)             | DbContext is often overexposed and used inconsistently         | Access is controlled through purpose-specific Unit of Work boundaries      |
| [11](#link-11) | [Access Control](#link-11)              | DbSet access is unrestricted and widely exposed                | Multiple Unit of Work layers enable controlled read/write access patterns  |

<a id="link-1"></a>
## 1. Clear Unit of Work boundaries and structured data access
Clear Unit of Work boundaries and structured data access.

A DbContext often represents a large part of the database schema. In many systems it may contain all tables, views, relationships, and provider-specific model configuration needed for that database.

That is not necessarily a problem by itself.

The problem starts when the DbContext is used directly throughout the application. Once every service can reach every DbSet, the database access layer becomes too wide, too permissive, and too easy to misuse.

EfCore.Boost treats the DbContext as the lower-level database model and places one or more Unit of Work layers on top of it.

Each Unit of Work can expose only the repositories needed for a specific purpose. One Unit of Work may expose read-only access to lookup data. Another may expose read/write access to business tables. Another may be designed for import, synchronization, reporting, or administrative routines.

This makes it possible to use a broad DbContext while still keeping application access narrow, intentional, and controlled.

In short:

- DbContext describes the database model
- Unit of Work defines the access boundary
- repositories expose the allowed operations
- application code works through purpose-specific entry points

This keeps the database model complete without turning the entire application into a free-for-all around DbSet access.
<a id="link-2"></a>
## 2. Provider-aware conventions and consistent mappings
Supporting multiple database providers involves more than switching a connection string. Each database engine has different rules and defaults that affect how data is stored and accessed.

Common differences include:

- **Schemas and naming**  
  SQL Server and PostgreSQL support schemas differently, while MySQL treats schemas more like databases.

- **Date and time handling**  
  Types such as `datetime`, `datetimeoffset`, and `timestamptz` behave differently across providers.

- **Strings and lengths**  
  SQL Server distinguishes between fixed and max-length types, PostgreSQL uses `text` more freely, and MySQL has stricter limits.

- **Identifiers (GUIDs)**  
  Each provider uses different types and generation strategies (`uniqueidentifier`, `uuid`, `char/binary`).

- **Numeric precision**  
  Decimal and money handling can vary in precision and defaults.

- **Booleans**  
  Represented as `bit`, `boolean`, or `tinyint` depending on provider.

- **Migrations and SQL generation**  
  SQL syntax, indexes, constraints, and computed fields differ across providers.

- **Routines (functions and procedures)**  
  Each provider has its own syntax, capabilities, and calling conventions for routines. Mapping and invoking them consistently is non-trivial.


EfCore.Boost centralizes these differences into conventions and provider-specific mappings.

Instead of handling these variations repeatedly in model configuration or application code, they are defined once and applied consistently.

This includes routines (stored procedures & functions), where EfCore.Boost provides a structured way to define, map, and call database routines across providers.

In practice:

- the model expresses intent
- EfCore.Boost applies provider-correct behavior
- database differences are handled in one place

This makes cross-database support predictable and significantly reduces the effort required to switch providers.  
For those who want to dive deeper into the details, see the resources below.  
EfCore.Boost handles these differences through conventions, so you typically do not need to deal with them directly.
- [Applying EfBoost Conventions](./ModelBuilding.md#applying-efboost-conventions)
- [Naming & Schema Conventions](./ModelBuilding.md#naming--schema-conventions)
- [Date & Timestamp Behavior](./ModelBuilding.md#date--timestamp-behavior)
- [String Size Considerations](./ModelBuilding.md#string-size-considerations)
- [Primary Keys & Identity Behavior](./ModelBuilding.md#primary-keys--identity-behavior)
- [Concurrency Columns](./ModelBuilding.md#concurrency-columns)
- [Cascade Delete Policy](./ModelBuilding.md#cascade-delete-policy)
- [Shared Read Models: Views & Routines](./ModelBuilding.md#shared-read-models-views--routines)
- [Custom Attributes](./ModelBuilding.md#custom-attributes)
- [Routine Design Guidelines](./DbUowRoutines.md#routine-design-guidelines-for-cross-platform-databases)
<a id="link-3"></a>
## 3. Controlled and streamlined migration workflows with script generation

**Typical EF Core challenge:**  
Managing migrations across environments and providers becomes fragile.

**EfCore.Boost approach:**  
Structured migration workflows provided by the migration project template.

EF Core provides a solid migration system, but real-world usage often becomes complex when dealing with multiple environments, providers, and deployment strategies.

EfCore.Boost addresses this through its **migration project template**, which provides a structured and repeatable way to manage migrations.

The template includes ready-to-use PowerShell scripts and a command-line tool that:

- create and apply migrations
- generate deployment SQL scripts
- manage migrations per provider
- support version-to-version script generation
- apply migrations through a consistent CLI workflow

It also supports **data seeding through CSV files**, using EfCore.Boost’s bulk insert capabilities for efficient data loading.

Using these tools is optional, but they significantly simplify the migration and deployment process in practice.

### Key concepts

- **Per-provider migration sets**  
  Each provider (SQL Server, PostgreSQL, MySQL) maintains its own migration history within the migration project.

- **SQL-first deployment**  
  SQL scripts are the primary deployment mechanism, making changes explicit, reviewable, and safe to apply across environments.

- **Version-based scripts**  
  Scripts can be generated between specific versions, allowing controlled upgrades instead of always applying the full migration chain.

- **Dedicated migration project**  
  Migrations are managed in a separate project, keeping them isolated from application code and easier to reason about.

- **Integrated data seeding**  
  Seed data can be loaded from CSV files as part of the workflow, using bulk insert for performance and consistency.

Even when working with a single database provider, using the migration template makes the workflow more predictable and easier to maintain.

### Further details

For those who want to dive deeper into the migration workflow, see:

- [Migration Project Structure](./Migrations.md#migration-project-structure)
- [PowerShell Workflows](./Migrations.md#powershell-workflows)
- [SQL Script Generation](./Migrations.md#sql-script-generation)
- [Multi-Provider Migrations](./Migrations.md#multi-provider-migrations)
- [Data Seeding from CSV](./Migrations.md#data-seeding)

---

<a id="link-4"></a>
## 4. Built-in patterns for efficient bulk handling
Efficient bulk operations are supported by the underlying database providers, but they vary significantly in capability and integration.

For example:

- SQL Server provides high-performance bulk operations via `SqlBulkCopy`
- PostgreSQL provides efficient bulk loading through `COPY`
- MySQL supports bulk-style operations (such as multi-row inserts and `LOAD DATA`), but with less consistent integration

These capabilities are not exposed as a unified part of standard EF Core workflows.

Using them typically requires provider-specific code, external libraries, or separate data pipelines. As a result, bulk handling often sits outside normal DbContext usage and is treated as a special case.

EfCore.Boost bridges this gap by integrating bulk handling directly into repositories and DbContext transactions.

This means:

- provider-specific bulk capabilities are used where available
- a consistent API is presented across providers
- bulk operations follow the same patterns as standard EF operations
- they participate in the same transactional flow


### Beyond imports

Bulk handling is not limited to data imports or replication scenarios.

With EfCore.Boost, bulk operations become part of the standard toolset and can be used in everyday application logic.

For example:

- efficiently inserting large volumes of log data
- processing batch updates
- handling integration or synchronization workloads

Instead of treating bulk operations as a special case, they become a natural extension of the data access layer.

Using it is as simple as this example:
```csharp
await uow.Customers.BulkInsert(customers);
```

### Further details

For those who want to dive deeper, see:

- [Bulk Operations Overview](./Bulk.md#overview)
- [Provider-Specific Implementations](./Bulk.md#provider-specific)
- [Integration with Repositories](./Bulk.md#repository-integration)
- [Bulk Inserts and Transactions](./Bulk.md#transactions)

---

<a id="link-5"></a>
## 5. Controlled and predictable OData integration

OData provides a powerful and standardized way to query data over HTTP. However, exposing OData directly on top of DbContext or DbSet can easily lead to overexposure of the database model and unpredictable query behavior.

EfCore.Boost integrates OData at the repository level using the standard `ODataQueryOptions` construct.

This means:

- queries are applied through repositories, not directly on DbSet
- access is limited to what the Unit of Work exposes
- query behavior remains consistent with the application’s data access structure

### Controlled exposure

When building an OData service, it is important to control what parts of the model are exposed.

EfCore.Boost provides an **EDM helper** that builds the OData model based on the repositories exposed by a given Unit of Work.

This ensures that:

- only explicitly exposed entities are available through OData
- the OData surface matches the intended access boundaries
- internal or unrelated tables are not accidentally exposed

### Further details

For those who want to dive deeper into OData usage, see:

- [OData Integration](./OData.md#integration)
- [Using ODataQueryOptions](./OData.md#query-options)
- [EDM Helper](./OData.md#edm-helper)
- [Controlling Exposed Entities](./OData.md#exposure-control)

---

<a id="link-6"></a>
## 6. First-class support for database-native constructs
EF Core supports database views, but view mapping is normally configured through fluent setup in `OnModelCreating`. This works, but it separates the view definition from the model class and makes the intent less visible.

EfCore.Boost improves this by allowing views to be declared directly on the model using attributes.

For views, EfCore.Boost supports:

- class-level view declaration
- explicit view key definition using `ViewKey`
- cleaner model definitions with less fluent configuration
- clearer separation between table-backed entities and view-backed read models

This keeps view intent close to the model instead of hiding it in fluent configuration.

---

### Unified routine access

Routine support in EF Core is more limited. In many cases, calling functions or stored procedures requires using raw SQL, ADO.NET commands, or provider-specific code.

EfCore.Boost integrates routine support into the Unit of Work.

This allows routines to be called as part of the same structured data access layer as repositories.

It also means routine calls can participate in the same DbContext and transaction scope as other database operations.

---

### Provider abstraction

Different databases expose routines differently.

One provider may use stored procedures. Another may use functions. Parameter handling, return values, and SQL syntax can also vary.

EfCore.Boost hides these details behind conventions and provider-aware implementations.

The C# calling code can remain consistent even when the underlying database implementation differs between SQL Server, PostgreSQL, and MySQL.

---

### Why this matters

Views and routines are common in real database systems.

They are used for:

- read models
- reporting
- lookups
- imports and synchronization
- validation
- encapsulated database logic

EfCore.Boost makes these features easier to use without pushing them outside the normal application data access structure.

---

### Further details

For those who want to dive deeper, see:

- [Routine Design Guidelines](./DbUowRoutines.md)

---

<a id="link-7"></a>
## 7. Consistent transactional patterns across providers
Transaction handling in EF Core can become verbose when real workloads combine normal repository operations, bulk operations, and routine calls.

EfCore.Boost simplifies this by exposing a transaction envelope on the Unit of Work instead of requiring application code to manually begin, commit, or roll back transactions.

The application defines the block of work.  
The Unit of Work handles the transaction lifecycle.

This provides:

- automatic commit when the block succeeds
- automatic rollback when an exception escapes the block
- consistent behavior for repository operations, bulk inserts, and routines
- protection against accidental nested transactions on the same Unit of Work
- retry-safe execution through EF Core execution strategies, which is especially useful for cloud databases such as Azure SQL

This means bulk operations and routine calls can participate in the same transaction as ordinary EF operations.

Instead of managing transaction objects directly, application code works through a structured transaction boundary.

---

### Why this matters

Real database work is rarely just one operation.

A single logical task may need to:

- insert or update entities
- call a routine
- bulk insert log or import rows
- save normal EF changes
- roll everything back if one part fails

EfCore.Boost keeps this as one coordinated unit of work.

This makes transaction handling easier to read, safer to use, and more consistent across providers.

Example:
```csharp
await uow.RunInTransactionAsync(async ct =>
{
    ...some reopo work perhaps eding with the code below...
    await uow.LogEntries.BulkInsertAsync(items, ct);
    await uow.SaveChangesAsync(ct);
}, ct);
```
*You can also wrap try-catch blocks around the transaction block to handle exceptions and what to do when transaction fails.* 
### Further details

For those who want to dive deeper, see:

- [Transactions](./DbUow.md#transactions)
- [Nested Work and Bulk Operations](./DbUow.md#nested-work-and-bulk-operations)
- [Saving & Lifecycle](./DbUow.md#saving--lifecycle)
- [Routines: Beyond Plain Table Access](./DbUow.md#routines-beyond-plain-table-access)
- [Unit of Work Routines](./DbUowRoutines.md)

---

<a id="link-8"></a>
## 8. Enforced conventions and predictable structure
EF Core is intentionally flexible, but this flexibility often leads to inconsistency in real-world projects.

Over time, different parts of the system may adopt different patterns for:

- naming and schema structure
- string lengths and data types
- nullability and defaults
- model configuration (attributes vs fluent)
- transaction handling and data access

This makes the system harder to understand, maintain, and evolve.

---

### Consistent conventions

EfCore.Boost introduces a set of conventions that are applied consistently across the model.

These include:

- attribute-driven definitions for common data patterns
- consistent handling of strings, identifiers, and numeric types
- provider-aware mappings applied centrally

This ensures that similar concepts are modeled the same way throughout the system.

---

### Predictable structure

EfCore.Boost also enforces structure at the application level:

- Unit of Work defines access boundaries
- repositories define data access patterns
- migrations follow a structured workflow

This reduces variation in how data access is implemented across the codebase.

---

### Why this matters

Without conventions, systems tend to drift over time.

With EfCore.Boost:

- the model remains consistent
- patterns are easy to recognize
- new code follows existing structure naturally

This improves long-term maintainability and reduces the cost of onboarding and refactoring.

---

<a id="link-9"></a>
### 9. Attribute-driven modeling and DDD

EfCore.Boost supports both attribute-driven models and fluent EF Core configuration.

The preferred Boost style is to express database intent directly on the model using attributes. This keeps the model compact, readable, and easy to reason about.

EfCore.Boost introduces a small set of custom attributes to support this approach. These include attributes for string purpose and length, GUID and identity handling, and other common database concerns.  
See [Model Building](./ModelBuilding.md#examples-of-applying-efboost-attributes) for more details.

Projects that follow strict DDD or Clean Architecture principles often use domain entities directly with EF Core, without introducing separate persistence models. In these cases, persistence metadata is typically defined using fluent configuration rather than attributes.

This approach works fully with EfCore.Boost.

The only tradeoff is reduced clarity in the model itself, as configuration is moved away from the entity definitions and into fluent mappings.

### 9b. Database independence and DDD

A common goal in DDD and Clean Architecture is to keep the database replaceable.

EfCore.Boost directly supports this goal.

By centralizing provider-specific behavior into conventions and abstractions, EfCore.Boost ensures that database differences are handled consistently and in one place.

This provides a clear and structured path for switching between supported providers (such as SQL Server, PostgreSQL, and MySQL), without requiring widespread changes to application code or model definitions.

EfCore.Boost does not remove the need to understand your database, but it significantly reduces the effort required to adapt to a different provider.

In practice, database portability becomes a matter of configuration and migration strategy, rather than a redesign of the data layer.

---

<a id="link-10"></a>
## 10. Access is controlled through purpose-specific Unit of Work boundaries
In standard EF Core usage, DbContext is frequently injected directly into services and components.

This gives broad access to all DbSet properties, allowing any part of the application to query or modify any part of the database.

Over time, this leads to:

- unclear ownership of data access
- unintended coupling between unrelated parts of the system
- inconsistent use of transactions and data patterns

---

### Purpose-specific access

EfCore.Boost introduces the options of multiple Unit of Work types on top of a shared DbContext.

Each Unit of Work exposes only the repositories required for a specific purpose.

For example:

- a read-only Unit of Work for lookups
- a read/write Unit of Work for business operations
- a specialized Unit of Work for imports, logs, or administration

This ensures that each part of the application only has access to what it actually needs.

---

### Controlled surface area

Instead of exposing the full database model, each Unit of Work defines a controlled surface area.

This means:

- fewer accidental dependencies
- clearer boundaries between application components
- easier reasoning about data access

---

### Why this matters

The database model can remain complete and expressive, while application access stays focused and intentional.

This reduces coupling, improves maintainability, and helps prevent misuse of the data layer.

The DbContext can represent the full database.  
The application does not need to see all of it.

---
 
<a id="link-11"></a>
## 11. Multiple Unit of Work layers enable controlled read/write access patterns
Instead of giving every consumer full access to the underlying `DbContext`, EfCore.Boost promotes creating purpose-specific Unit of Work variants, for example:

- **Read-only UOW** → exposes query operations only (no `SaveChanges`)
- **Write-enabled UOW** → allows controlled mutations and persistence
- **Restricted UOW** → exposes only selected repositories or operations

This allows:

- Clear separation between **query and command paths**
- Prevention of accidental writes in read scenarios
- Encapsulation of data access rules per use case
- Reduced risk of leaking persistence concerns across layers

In practice, this means consumers do not interact with raw `DbSet`s directly, but through a UOW tailored to their responsibility. The result is more predictable behavior and easier enforcement of data access policies across the application.