# Why EfCore.Boost?

This document explains the key challenges commonly encountered when using EF Core in real-world database projects, and how EfCore.Boost addresses them.

EfCore.Boost addresses a set of common challenges in EF Core-based systems, making it particularly effective for database-driven applications.

The table below summarizes these areas. Each point highlights a typical issue in EF Core usage and the corresponding approach taken by EfCore.Boost.

Further sections in this document expand on each topic in more detail.

| #              | Area                    | Typical EF Core Challenge                                      | EfCore.Boost Approach                                                     |
|----------------|-------------------------|----------------------------------------------------------------|----------------------------------------------------------------------------|
| 1              | Data Access Boundaries  | Direct DbContext usage can expose too much of the database surface area across an application. | [EfCore.Boost allows applications to expose only the repositories, entities, routines, and operations intended for each part of the system.](#link-1)                   |
| 2              | Multi-Provider          | Behavior differs across SQL Server, PostgreSQL, and MySQL      | [Provider-aware conventions and consistent mappings](#link-2)                         |
| 3              | Migrations              | Managing migrations across environments and providers is fragile | [Controlled and streamlined migration workflows with script generation](#link-3)      |
| 4              | Bulk Operations         | High-volume inserts and updates require custom solutions       | [Built-in patterns for efficient bulk handling](#link-4)                              |
| 5              | OData                   | Query exposure can become unsafe or inconsistent               | [Controlled and predictable OData integration](#link-5)                               |
| 6              | Database Features       | Views, routines, and raw SQL are awkward to integrate cleanly  | [First-class support for database-native constructs](#link-6)                         |
| 7              | Transactions            | Complex transactions require manual coordination across EF operations, bulk work, routines, retries, and provider differences. | [Structured Unit of Work transactions with coordinated execution, rollback safety, and retry-aware handling.](#link-7)                         |
| 8              | Maintainability         | Different parts of the system use different data access patterns | [Enforced conventions and predictable structure](#link-8)                             |
| 9              | Model Definition        | Fluent configuration becomes complex and fragmented            | [Attributes and equivalent fluent API simplify model definitions](#link-9)        |

<a id="link-1"></a>
## 1. Clear Unit of Work boundaries and structured data access

**Typical EF Core challenge:**  
Direct DbContext usage can expose too much of the database surface area across an application.  

`DbContext` commonly contains many DbSet models representing different parts of the underlying database structure.  
However, application modules and services often require more controlled and intentional access to data and operations than a shared DbContext naturally provides.

**EfCore.Boost approach:**  


EfCore.Boost introduces structured Unit of Work and Repository boundaries on top of EF Core, allowing each part of the application to expose only the repositories, entities, routines, and operations intended for its responsibilities.  
Different Unit of Work implementations can expose different capabilities while still sharing the same underlying DbContext and entity model.

### Purpose-specific access

Each Unit of Work can expose only the capabilities required for a specific purpose.

For example:

- Readonly UOW → exposes query operations only
- Write-enabled UOW → allows controlled mutations
- Reporting UOW → exposes readonly views and reporting queries
- Integration UOW → supports imports, exports, or synchronization workflows
- Administrative UOW → exposes elevated operational functionality

This helps ensure that each part of the application only accesses what it actually needs.

### Structured and predictable access patterns

EfCore.Boost helps standardize how repositories, transactions, routines, bulk operations, and queries are organized throughout the system.  
This creates clearer boundaries between application modules and reduces inconsistent data access patterns across services and components.

Readonly repositories and query paths also make tracked and untracked query behavior more explicit.  
Readonly access enforces untracked entity usage, helping reduce accidental state tracking, unintended updates, and unnecessary change tracking overhead in reporting and lookup scenarios.

### Reduced surface area and safer operations

Instead of exposing the full database model everywhere, Unit of Work implementations define controlled access surfaces tailored to operational responsibilities.

This helps reduce:

- unintended coupling between unrelated modules
- accidental writes in readonly scenarios
- inconsistent transaction handling
- broad or unnecessary database exposure
- side effects caused by unrestricted data access

The database model can remain complete and expressive while application access stays focused and intentional.

### Reporting and lookup workflows

Readonly Unit of Work implementations may expose database views instead of direct table entities where appropriate.  
Combined with OData support, this allows applications to build flexible lookup and reporting endpoints directly on top of EF Core, reducing the common temptation to introduce separate read-oriented ORM layers such as Dapper purely for reporting and query workloads.
See more details in the [Unit of Work](./DbUow.md) documentation.
<a id="link-2"></a>
## 2. Provider-aware conventions and consistent mappings

**Typical EF Core challenge:**  
Behavior differs across SQL Server, PostgreSQL, and MySQL.

**EfCore.Boost approach:**  
Provider-aware conventions and consistent mappings.

Supporting multiple database providers involves more than switching a connection string. Each database engine has different rules and defaults that affect how data is stored and accessed.

Common differences include:

- **Schemas and naming**  
  SQL Server and PostgreSQL support schemas differently, while MySQL treats schemas more like databases.

- **Date and time handling**  
  Types such as `datetime`, `datetimeoffset`, and `timestamptz` behave differently across providers. EfCore.Boost recommends storing point-in-time values as UTC using `DateTime` for cross-provider consistency. SQL Server's `datetimeoffset` preserves offsets, but PostgreSQL and MySQL do not; therefore, normalizing persisted values to UTC is preferred.

- **Strings and lengths**  
  SQL Server distinguishes between fixed and max-length types, PostgreSQL uses `text` more freely, and MySQL has stricter limits. **EfCore.Boost encourages Case-Insensitive (CI) designs across all providers to ensure consistent behavior in lookups and comparisons.**

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
- [Applying EfCore.Boost Conventions](./ModelBuilding.md#applying-efcore.boost-conventions)
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

- [Migration Project Structure](./EfMigrationsCMD.md#the-snapshot-problem)
- [PowerShell Workflows](./EfMigrationsCMD.md#why-powershell-helps)
- [SQL Script Generation](./EfMigrationsCMD.md#13-generate-sql-deployment-scripts)
- [Multi-Provider Migrations](./EfMigrationsCMD.md#strategy-used-in-the-templates)

---

<a id="link-4"></a>
## 4. Built-in patterns for efficient bulk handling

**Typical EF Core challenge:**  
High-volume inserts and updates require custom solutions.

**EfCore.Boost approach:**  
Built-in patterns for efficient bulk handling.

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

- [Bulk Operations Overview](./BulkInserts.md)
- [Provider-Specific Implementations](./BulkInserts.md#How-EfCore-Boost-Implements-Bulk-Inserts)

---

<a id="link-5"></a>
## 5. Controlled and predictable OData integration

**Typical EF Core challenge:**  
Query exposure can become unsafe or inconsistent.

**EfCore.Boost approach:**  
Controlled and predictable OData integration.

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
- [Using ODataQueryOptions](./OData.md#ODataQueryOptions-in-Controllers)
- [EDM Helper](./OData.md#How-to-think-about-OData-in-EfCore-Boost)
- [Controlling Exposed Entities](./OData.md#security-odatapolicy-and-data-boundary-control)

---

<a id="link-6"></a>
## 6. First-class support for database-native constructs

**Typical EF Core challenge:**  
Views, routines, and raw SQL are awkward to integrate cleanly.

**EfCore.Boost approach:**  
First-class support for database-native constructs.

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

**Typical EF Core challenge:**  
Complex transactions require manual coordination across EF operations, bulk work, routines, retries, and provider differences.

**EfCore.Boost approach:**  
Structured Unit of Work transactions with coordinated execution, rollback safety, and retry-aware handling.

Transaction handling in EF Core can become verbose once real workloads combine:
- normal repository operations
- bulk operations
- routine calls
- multiple SaveChanges calls
- retry handling for transient cloud failures

While EF Core provides a common transaction API, transactional behavior still differs between providers such as SQL Server, PostgreSQL, and MySQL.

EfCore.Boost simplifies this by exposing structured transaction execution directly on the Unit of Work.

The application defines the block of work.
The Unit of Work manages the transaction lifecycle.

For example, an EfCore transaction block might look like this:
```csharp
var strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    try
    {
        // normal EF db-context operations (Queries & Updates)
        await db.SaveChangesAsync(ct);
        // low level routine calls via ADO-command, hand-bound to the db-transaction
        // low level provider-speciffic, bulk-inserts, hand-bound to the db-transaction
        await tx.CommitAsync(ct);
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
});
```

But with EfCore.Boost like this:

```csharp
await uow.RunInTransactionAsync(async ct =>
{
    // normal uow operations (Queries & Updates)
    await uow.SaveChangesAsync(ct);
    await uow.RunMyRoutineAsync(prop1, prop2); // povider invariant routine call i.e. calculating and saving prop1 & prop2 via Store Procedure, to the database
    await uow.LogEntries.BulkInsertAsync(logList, ct); // simple bulk insert for the LogEntries entity to db, via current transaction
}, ct);
```

This provides:

- automatic commit when the block succeeds
- automatic rollback when an exception escapes the block
- coordinated transactions across repositories, routines, and bulk operations
- protection against accidental nested transactions on the same Unit of Work
- retry-aware execution through EF Core execution strategies (Azure)
- cleaner transactional boundaries in application code

This allows bulk operations and routine calls to participate in the same transaction as ordinary EF operations.

Instead of manually orchestrating transaction objects, application code works through a structured transactional boundary.

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

This makes transactional code:
- easier to read
- safer to maintain
- less repetitive
- more resilient in cloud environments
- more consistent across supported providers

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

**Typical EF Core challenge:**  
Different parts of the system use different data access patterns.

**EfCore.Boost approach:**  
Enforced conventions and predictable structure.

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
- the new code follows the existing structure naturally

This improves long-term maintainability and reduces the cost of onboarding and refactoring.

---

<a id="link-9"></a>
### 9. Attribute-driven modeling and Fluent Support

**Typical EF Core challenge:**  
Fluent configuration becomes complex and fragmented.

**EfCore.Boost approach:**  
Attributes and an equivalent fluent API simplify and clarify model definitions.

EfCore.Boost provides a dual approach to model configuration: high-level intent attributes and a fully-featured fluent API.

#### Attribute Style
The preferred Boost style is to express database intent directly on the model using attributes. This keeps the model compact, readable, and easy to reason about as the metadata stays right next to the properties.

#### Fluent Style
For projects following strict DDD or Clean Architecture, or when working with inherited models where you cannot (or should not) modify the entity classes, EfCore.Boost provides a complete Fluent API.

Each Boost attribute has a corresponding `HasXxx()` extension method (e.g., `HasDbAutoUid()`, `HasStrShort()`, `HasMoney()`). This allows you to apply the same powerful Boost conventions without polluting your domain entities with persistence-specific attributes.

The behavior is identical regardless of the approach chosen. See [Model Building](./ModelBuilding.md#attributes-vs-fluent-api) for more details and side-by-side examples.

### 9b. Database independence and DDD

A common goal in DDD and Clean Architecture is to keep the domain model decoupled from infrastructure concerns.

EfCore.Boost directly supports this goal by providing the fluent alternative. This ensures that even if you choose to keep your entities "clean" of attributes, you still benefit from Boost's provider-aware conventions and consistent mappings.

By centralizing provider-specific behavior into these shared helpers, EfCore.Boost ensures that database differences are handled consistently. This provides a clear path for switching between supported providers (such as SQL Server, PostgreSQL, and MySQL) without requiring widespread changes to the core model logic.

---

<a id="link-10"></a>

