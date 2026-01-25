# OData Support  
## High-Capability Query Shaping in EfCore.Boost

OData provides a standardized way for clients to filter, sort, page, and shape data directly at the database level. Instead of designing many specialized REST endpoints or inventing custom query languages, OData delivers a powerful, well-defined query model that integrates naturally with Entity Framework Core.

EfCore.Boost integrates OData in a way that emphasizes:

- performance
- control
- security
- predictable behavior

OData becomes a strong querying capability, while EfBoost ensures it remains safe, bounded, and architecturally disciplined.

---

## Why OData?

Traditional REST APIs often evolve into very complex endpoints when consumers need flexibility. Typical real-world requirements include:

- filtering by multiple fields
- text searching
- paging
- sorting
- conditional filters
- result counting

Without OData this leads to:
- many API variants
- complicated custom filter models
- brittle ad‑hoc logic
- slow application-side filtering

OData solves this with a proven standard:

```
?$filter=
?$orderby=
?$top=
?$skip=
?$count=
?$select=
```

EfBoost ensures these translate into efficient SQL executed by the database engine, not processed in application memory.

---

## Performance Characteristics

EfBoost applies OData queries directly to EF Core queries. This means:

- filtering executes in SQL
- paging is computed by the database
- ordering is handled by indexes
- only the required rows are materialized

This is particularly valuable for:

- administrative grids
- reporting dashboards
- analytic views
- searchable lists
- large datasets

OData, when applied correctly, offers performance comparable to well-tuned custom endpoints, but without needing to build dozens of specialized APIs.

---

## OData Is Best for Querying (Not CRUD)

While OData defines full CRUD semantics, real-world enterprise systems rarely benefit from using OData as the write API. Challenges include:

- unclear business rule enforcement
- computed or read-only field conflicts
- lifecycle validation
- versioning and audit concerns
- consumer misuse risks

EfBoost’s design stance is intentionally clear:

OData is ideal for querying and data shaping.
Normal application APIs remain ideal for writes and workflows.

EfBoost enables rich read capabilities while leaving update logic to structured, intentional service contracts.

---

## Security and Data Boundary Control

Raw OData exposed directly over DbSets is unsafe and potentially harmful. Without careful control, it may lead to:

- cross‑tenant data exposure
- access to unintended rows
- excessively large result sets
- performance degradation

EfBoost avoids this by requiring OData to be applied on controlled query roots, not raw DbSets.

Example secure pattern:

```csharp
var baseQuery = _uow.Users
    .QueryNoTrack()
    .Where(u => u.TenantId == session.TenantId && u.IsActive);
```

Then apply OData on top of the secured base dataset:

```csharp
var result = await _uow.Users.ApplyOdataFilterAsync(options, baseQuery);
```

This model guarantees:

- tenant isolation
- enforced security boundary
- predictable behavior
- index‑friendly performance

---

# OData in EfCore.Boost

EfBoost provides two complementary ways to work with OData.

---

## ApplyOdataFilterAsync

This method gives maximum control. You define what data is visible, and OData can refine it.

```csharp
var baseQuery = _uow.Users
    .QueryNoTrack()
    .Where(u => u.IsActive);

var result = await _uow.Users.ApplyOdataFilterAsync(options, baseQuery);
```

Use this when you want:

- fixed mandatory filters
- multi-tenant isolation
- predefined business constraints
- controlled exposure

OData becomes a shaping tool, not a gateway.

---

## QueryWithODataAsync

When you want convenience and a complete OData experience:

```csharp
var result = await _uow.Users.QueryWithODataAsync(options);
```

This:
- executes an EF query
- applies OData
- packages results and metadata
- supports paging and counts

You need to be more careful here since all the dataset is exposed here without base-query boundaries.

---

## ODataPolicy

`ODataPolicy` is EfBoost’s **query firewall** for OData. It defines which OData query options are allowed and enforces limits on query complexity. The intent is predictable behavior and protection against expensive or unintended queries.

### What it controls

- **MaxTop**  
  Maximum value allowed for `$top`. Use this to prevent very large result sets.

- **ServerPageSize**  
  Maximum number of items returned per server-generated page. When set, this caps page size regardless of client input.

- **AllowFilter**  
  Enables or disables the `$filter` query option.

- **AllowOrderBy / AllowedOrderBy**  
  Enables `$orderby`. If `AllowedOrderBy` is specified, sorting is restricted to the listed column names only.

- **AllowSelect**  
  Enables or disables the `$select` query option.

- **AllowExpand / AllowedExpand / MaxExpansionDepth**  
  Enables `$expand`. You may optionally restrict which navigation properties can be expanded via `AllowedExpand`, and limit nesting with `MaxExpansionDepth`.

- **AllowCount**  
  Enables or disables the `$count` query option.

By default, all options are allowed except `$expand` and `$orderby`. 

---

## About $expand & $orderby

`$expand` allows related entities to be included in the response. While powerful, it can:

- increase payload size dramatically
- expose unintended relationships
- reduce query predictability
- degrade performance

Recommended guidance:

- enable `$expand` only when clearly justified
- avoid expanding large or unbounded collections
- prefer explicit API endpoints for deep or complex object graphs

`$orderby` can be expensive on large result sets, especially when applied to non-indexed columns.  
For very large or frequently accessed datasets, it is recommended to restrict `$orderby` to indexed columns only.  
Use **`AllowedOrderBy`** in `ODataPolicy` to explicitly whitelist safe sortable columns.

---

## Usage example

When a query uses `$expand`, you must opt in by passing a policy with `AllowExpand = true`.

```csharp
var res = await uow.MyTables.QueryWithODataAsync(options, new ODataPolicy(AllowExpand: true));
```

To restrict which navigation properties may be expanded:

```csharp
var policy = new ODataPolicy(
    AllowExpand: true,
    AllowedExpand: [nameof(DbTest.MyTable.MyTableRefs)],
    MaxExpansionDepth: 2
);

var res = await uow.MyTables.QueryWithODataAsync(options, policy);
```
Of course you dont need to specify navigation property names in `AllowedExpand` if there is only 
one navigaton available or you want to allow it for all properties.
```csharp
var res = await uow.MyTables.QueryWithODataAsync(options, new ODataPolicy(AllowExpand: true));
```
---

## Validation behavior

All incoming OData queries are validated against the provided `ODataPolicy` before execution.

Validation fails when:
- A disallowed query option is used (e.g. `$expand` when `AllowExpand = false`).
- `$orderby` references a column not listed in `AllowedOrderBy`.
- `$top` exceeds `MaxTop`.
- `$expand` exceeds `MaxExpansionDepth` or references a navigation not listed in `AllowedExpand`.

When validation fails:
- In unit tests, an exception is thrown and the test fails.
- In WebAPI usage, this should typically be translated to a **400 Bad Request** response.

This early rejection is intentional. Invalid or unsafe queries are blocked before EF Core generates SQL.

---

# Using Views with OData
## Shaping data where it belongs

EF is naturally centered around tables and relations. This is excellent for persistence, but not always ideal for:

- presentation shaping
- optimized read models
- cross-table aggregation
- business projections
- cultural transformations

Database views often represent the best surface for OData exposure.

EfBoost treats views as first‑class read repositories, meaning:

- they support OData
- they participate in paging and filtering
- they behave like intentional query sources

Views allow the database to:

- pre-join related tables
- compute fields
- centralize read‑specific logic
- stabilize query shape
- tune indexes effectively

This frequently results in cleaner APIs and better performance than exposing raw tables.

---

# Parameterized Routines as an Alternative

In some cases, a parameterized database routine can be a better choice than OData. This applies when:

- query logic is well defined and not ad hoc
- strict security constraints must always apply
- the result depends on contextual parameters
- execution must be highly deterministic
- procedural logic is more suitable than query logic

Example routines:

- GetCurrentMenuItemsForSession(@SessionId, @CultureId)
- GetCustomerDashboard(@CustomerId)

Example usage:
```csharp
var result = await _uow.RunRoutineQuery<CurrentMenuItemsV>("cms","GetCurrentMenuItemsForSession",
        new[]  {new DbParam("@SessionId", sessionId), new DbParam("@cultureId", cultId})
        .AsNoTracking().ToListAsync();
```

In these cases:

- the database ensures correct scoping
- logic runs at optimal execution location
- results are predictable
- consumers receive a defined structure

This does not replace OData; it is an alternative pattern for scenarios where flexibility is not the primary goal.  
See: [Routines Documentation](../../UOW/DbUowRoutines.md) for more details.

---

## Shared Read Models

EfBoost allows the same EF read model (view model) to be used for:

- database views
- routine result sets

This means:
- you define one read structure
- OData can work on the view
- routines can return the same shape
- application remains consistent

Routines should typically be treated like views internally, but not exposed as repositories when their usage is purpose‑specific.

---

# Practical Considerations, DTOs & $expand

## DTOs

EfBoost recommends using entities as the OData model and projecting to DTOs afterward if needed.

Benefits:

- reliable SQL translation
- reduced complexity
- stable behavior
- fewer surprises

DTOs are ideal for output formatting, not for query semantics.

---

# Common Pitfalls

Applying OData directly to DbSet  
Always route through a controlled base query.

Unbounded paging  
Always enforce Top limits.

Using OData for writes  
Prefer normal APIs for CRUD workflows.

Ignoring indexing  
Query capability always deserves indexing strategy planning.

---

# Optional Appendix  
## ODataQueryOptions<T> in Controllers

ASP.NET Core parses query clauses into ODataQueryOptions<T>:

```csharp
public async Task Get(ODataQueryOptions<User> options)
```

This represents $filter, $orderby, $top, etc.

EfBoost methods accept this object to apply filtering correctly. You may also optionally validate it using ODataValidationSettings to restrict:

- allowed operators
- allowed properties
- max page size

This provides another layer of safety when needed.

---

# Summary

EfBoost integrates OData as a controlled, high‑performance, secure query capability, built for:

- large datasets
- flexible read scenarios
- administrative and reporting needs
- client‑driven shaping with server‑side execution

Designed correctly, OData + EfBoost provides:

- performance comparable to custom APIs
- safer defaults
- better structure
- reduced endpoint complexity

OData for querying.  
Application services for writing.  
Views and routines for advanced shaping.  
EfBoost ensures they all work in harmony.
