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

## How to think about OData in EfCore.Boost

EfCore.Boost supports OData in two layers:

- a **convenience path** for the most common API usage
- a **full control path** when you need explicit control over behavior and output

Both paths use the same underlying mechanisms.

---

## Security, ODataPolicy and Data Boundary Control

Raw OData exposed directly over DbSets is unsafe and potentially harmful. Without careful control, it may lead to:

- cross‑tenant data exposure
- access to unintended rows
- excessively large result sets
- performance degradation

EfBoost avoids this by requiring OData to be applied on controlled query roots, not raw DbSets.

`ODataPolicy` defines **what the client is allowed to ask for**.

It governs:

- whether filtering is allowed
- whether ordering is allowed and on which fields
- whether paging is allowed and its limits
- whether `$count` is allowed
- whether `$expand` or `$select` are allowed

Example:

```csharp
var policy = new ODataPolicy(
    AllowFilter: true,
    AllowOrderBy: true,
    AllowedOrderBy: [nameof(User.Name), nameof(User.Created)],
    AllowExpand: false,
    AllowSelect: false,
    MaxTop: 100
);
```
Secure base query could i.e. be like this:
Example secure pattern:

```csharp
var baseQuery = _uow.Users
    .QueryNoTrack()
    .Where(u => u.TenantId == session.TenantId && u.IsActive);
```

Important distinction:

- **Policy** answers: *“What is the client allowed to request?”*
- **Query plan pipeline** answers: *“How do we execute what we accepted?”*

Disallowed options are ignored and recorded in the plan report.

---

## The convenience path

In most APIs, OData is primarily used for:

- client-provided `$filter`
- paging with `$skip` / `$top`
- sorting with `$orderby`
- returning results with an inline count

For this scenario, EfCore.Boost provides:

- **`FilterODataAsync`**

```csharp
var result = await repo.FilterODataAsync(
    baseQuery,
    options,
    policy: new ODataPolicy(
        MaxTop: 200,
        AllowOrderBy: true,
        AllowCount: true
    ),
    forceCount: true,
    ct
);
```

This method:

- applies OData on top of a secured base query you define
- enforces the provided `ODataPolicy`
- returns a typed `QueryResult<T>`
- ignores shaping (`$select` / projection `$expand`)

---

## Typed vs shaped execution

Once policy is applied, EfCore.Boost lets you decide **how** the accepted OData options are executed.

### Typed execution (entities)

Typed execution keeps results materialized as `T`.

- optional `$expand` via **expand-as-include**
- navigation paths become EF `Include(...)`
- nested expand options are ignored
- result type: `QueryResult<T>`

This is ideal for predictable, strongly typed responses.

---

### Shaped execution (projection)

Shaped execution is required when the request includes:

- `$select`
- projection-style `$expand`

In this mode:

- results no longer materialize as `T`
- result type becomes `QueryResult<object>`
- client controls payload shape (within policy)

This is more flexible but should usually be more restricted.

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

## Step 1: Build the query plan

```csharp
var plan = repo.BuildODataQueryPlan(
    baseQuery,
    options,
    policy: new ODataPolicy(
        AllowFilter: true,
        AllowOrderBy: true,
        AllowExpand: true,
        AllowSelect: true,
        MaxTop: 200
    ),
    forceCount: true
);
```

At this point:

- policy decisions are applied
- paging, ordering, and filtering are validated
- no query has executed yet

---

### Step 2A: Typed execution without expand

```csharp
var result = await repo.MaterializeODataAsync(plan, ct);
```

---

### Step 2B: Typed execution with expand-as-include

```csharp
plan = repo.ApplyODataExpandAsInclude(plan);
var result = await repo.MaterializeODataAsync(plan, ct);
```

Use this when:

- you want navigation data
- you still want strongly typed entities
- you accept limited expand semantics

---

### Step 2C: Shaped execution (select / projection expand)

```csharp
var shapedQuery = repo.ApplyODataSelectExpand(plan);
var result = await repo.MaterializeODataShapedAsync(plan, shapedQuery, ct);
```

Use this when:

- `$select` is present
- projection-style `$expand` is present
- you accept untyped output

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

## EfCore.Boost OData Examples (from BoostTest Smoke Test)
This document adds **concrete, copy-pasteable** OData examples based on the Boost test project’s `DbTest` model (`MyTable`, `MyTableRef`) and the smoke test code path.  
See the this model here: [TestDb.cs](../../../../tests/BoostTest/TestDb/TestDb.cs).

Relevant test model:

- `DbTest.MyTable` (table) has a navigation collection `MyTableRefs`
- `DbTest.MyTableRef` (table) is the child/line entity
- OData is applied on `EfRepo<DbTest.MyTable>` from `UOWTestDb` 

---

### 0. Always start with a secured base query

Even in tests, we start from a base query we own:

```csharp
var baseQuery = uow.MyTables.QueryNoTrack(); // your security boundary normally goes here
```

In a real API, this is where you apply tenant scoping, ACLs, soft-delete filters, etc.

---

### 1. The common path: Filter + paging + count (typed results)

This is the “most common and most useful” path: apply `$filter` and get a typed `QueryResult<MyTable>`.

From the smoke test:

```csharp
var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=LastChangedBy eq 'Stefan'");
var baseQuery = uow.MyTables.QueryNoTrack();
var result = await uow.MyTables.FilterODataAsync(
    baseQuery,
    options,
    policy: null,
    forceCount: true
);
Assert.True(result.InlineCount > 0 && !result.Results.Any(x => x.LastChangedBy != "Stefan"));
``` 

#### Adding security limits via `ODataPolicy`

`FilterODataAsync` also accepts `ODataPolicy` directly, so you can enforce guardrails without switching to the plan-first pipeline:

```csharp
var policy = new ODataPolicy(
    AllowFilter: true,
    AllowOrderBy: true,
    AllowCount: true,
    MaxTop: 200
    // optionally: AllowedOrderBy: ["LastChangedBy","LastChanged"]
);

var result = await uow.MyTables.FilterODataAsync(
    baseQuery,
    options,
    policy,
    forceCount: true
);
```

If the client asks for something not allowed by policy, Boost ignores that option and records the reason (report).

---

### 2. Full control: Build a plan first

When you want to decide **what you are going to do** with the OData request, start here:

```csharp
var plan = uow.MyTables.BuildODataQueryPlan(
    baseQuery,
    options,
    policy: new ODataPolicy(
        AllowFilter: true,
        AllowOrderBy: true,
        AllowCount: true,
        MaxTop: 200
    ),
    forceCount: true
);
```

At this point:

- policy has been applied
- skip/top/order/filter/count decisions are made
- nothing has executed yet
- you can choose **typed** vs **shaped** execution next

---

### 3. Typed expand: `$expand` as `Include(...)` (no inner filters)

This is the “I want parent with lines” scenario, **but still typed**.

In EfCore.Boost, typed expand is implemented as **expand-as-include**:

- `$expand=MyTableRefs` becomes `.Include(x => x.MyTableRefs)`
- nested expand options are ignored (`$filter` inside `$expand`, `$orderby` inside `$expand`, etc.)
- ignored nested options are recorded to `plan.Report`

From the smoke test:

```csharp
var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')"
);
var baseQuery = uow.MyTables.QueryNoTrack();
var plan = uow.MyTables.BuildODataQueryPlan(
    baseQuery,
    options2,
    new ODataPolicy(AllowExpand: true),
    forceCount: true
);
var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);
// Nested expand filters are not supported in include-mode, so they are reported:
Assert.True(
    plan2.Report.Where(x => x == "ExpandInnerFilterIgnored:MyTableRefs").Count() == 1
);
var res = await uow.MyTables.MaterializeODataAsync(plan2);
// We got MyTableRefs loaded (but unfiltered, because include-mode ignores inner expand filters)
Assert.True(res.Results.First().MyTableRefs.Count > 0);
``` 

#### When to use this

Use expand-as-include when you need:

- strongly typed entities
- navigation loading
- predictable SQL (no nested OData surprises)

---

### 4. Shaped responses: `$select` (projection output)

When the request uses `$select`, your query no longer materializes as `T`.
You must switch to the shaped path.

From the smoke test:

```csharp
var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow,"$filter=Id eq -1&$select=Id");
var baseQuery = uow.MyTables.QueryNoTrack();
var plan3 = uow.MyTables.BuildODataQueryPlan(
    baseQuery,
    opts,
    new ODataPolicy(AllowSelect: true),
    forceCount: true
);

// ApplyTo produces wrapper/projection element type
var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);

// Materialize into QueryResult<object>
var res3 = await uow.MyTables.MaterializeODataShapedAsync(plan3, shapedQuery3);

var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);

Assert.True(json.Contains("\"Id\""));
Assert.True(!json.Contains("LastChangedBy"));
Assert.True(!json.Contains("MyTableRefs"));
``` 

#### What you get back

- `QueryResult<object>`
- each result item is a shaped/projection object
- you typically serialize it directly (as in the test)

---

### 5. Shaped expand + select: projection `$expand`

If you accept projection expand (not include-mode), you still use the shaped pipeline:

1) build plan with `AllowExpand` and/or `AllowSelect`  
2) call `ApplyODataSelectExpand(plan)`  
3) materialize via `MaterializeODataShapedAsync`

The smoke test includes a shaped expand example (note: plan should be built from the same `opts4` passed in):

```csharp
var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
var baseQuery = uow.MyTables.QueryNoTrack();
var plan4 = uow.MyTables.BuildODataQueryPlan(
    baseQuery,
    opts4,
    new ODataPolicy(AllowExpand: true),
    forceCount: true
);
var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
var res4 = await uow.MyTables.MaterializeODataShapedAsync(plan4, shapedQuery4);
Assert.True(res4.Results != null && res4.Results.Count > 0);
``` 

Shaped expand supports projection, but your output is untyped (`object`) and should usually be protected with stricter policy limits.

---

### 6. Decision cheat sheet

- **Want typed entities + filter/paging/count?**  
  Use `FilterODataAsync(baseQuery, options, policy, forceCount)`.

- **Want typed entities + navigation loaded?**  
  Use plan-first + `ApplyODataExpandAsInclude(plan)` + `MaterializeODataAsync(plan)`.

- **Want `$select` / projection `$expand`?**  
  Use plan-first + `ApplyODataSelectExpand(plan)` + `MaterializeODataShapedAsync(...)`.


---

# Summary

EfBoost integrates OData as a controlled, high‑performance, secure query capability, built for:

- large datasets
- flexible read scenarios
- administrative and reporting needs
- client‑driven shaping with server‑side execution

Designed correctly, OData + EfBoost provides:

- performance superior to custom APIs
- safer defaults
- better structure
- reduced endpoint complexity

OData for querying.  
Application services for writing.  
Views and routines for advanced shaping.  
EfBoost ensures they all work in harmony.
