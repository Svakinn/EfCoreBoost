# DbRepo  
## Repository Layer for EfCore.Boost

`DbRepo` is the **repository layer** of EfCore.Boost.  
It sits on top of EF Core and gives every entity type (and view) a **purposeful, well-structured access surface**, instead of leaving consumers to directly poke `DbSet` everywhere.

Repositories provide:

- predictable **query patterns**
- clear **tracked vs non-tracked** behavior
- **read-only repos** for views ✔ **IReadRepo<T>**
- **read/write repos** for tables ✔ **IRepo<T>**
- **key lookup** helpers
- **OData** shaping
- **bulk operations**
- **raw SQL helpers**
- portability and provider awareness

Repositories are always accessed through a **Unit of Work**, which decides which repos exist, whether they are read-only, and who gets access.

---

## Repo & ReadRepo

EfBoost has two main repository families:

### ✔ ReadRepo  
Used for:
- views
- read-only entity sets
- places where mutation is not allowed

Provides:
- non-tracked querying
- key lookups
- streaming
- counting & existence
- OData shaping
- safe scalar raw SQL

---

### ✔ Repo (full repository)
Used for:
- tables
- entities that support modifications

Provides everything from ReadRepo, plus:
- tracked access
- add / update / delete operations
- bulk delete by primary key
- bulk insert

---

## Async / Sync Philosophy

EfBoost strongly encourages **async-first** usage because modern .NET workloads benefit enormously from non-blocking database I/O.

However:
- sometimes synchronous execution is required
- sometimes legacy code or job contexts still depend on it

So for nearly every async API, EfBoost also provides a **synchronous twin**.

To keep things honest and explicit:
- async methods end with `Async`
- sync methods clearly identify themselves via a distinct naming suffix (**Synchronized**)
- there are no ambiguous “same name but blocking secretly” traps

From here forward, we only document async variants to avoid repetition.  
Know that sync versions exist in parallel where appropriate.

---

## Types of functionality in DbRepo

Repositories provide several categories of functions:

1️⃣ **Querying**
- tracked / non-tracked
- streaming
- first / exists / count
- flexible filtering

2️⃣ **Key Lookup**
- by EF primary key
- specialized helpers for common `long Id` models

3️⃣ **OData**
- safe shaping
- optional policies
- optional count & metadata packaging

4️⃣ **Bulk Operations**
- insert large sets quickly
- delete by primary keys efficiently

5️⃣ **Change Tracking Helpers**
- attach and mark modified
- controlled graph updating


The sections below group functions according to these categories.

---

# Repository API Groups

---

## Querying

Repositories do not fetch data immediately.  
They **expose queries**, so you can build intent first and decide when and how to execute.  
Of course this is the Ef´s IQueriable pattern, the repositiories expose.


```csharp
var qry = uow.Users.QueryUnTracked()
    .Where(u => u.IsActive && u.Role == "Admin");

var admins = await qry.ToListAsync(ct); //ct being the cancellation token
```

No SQL hits the database until execution (`ToListAsync`, `FirstAsync`, `CountAsync`, etc.).

This design lets you:

- build layered filters
- reuse query fragments
- compose conditions dynamically
- keep performance predictable

---

### 🔷 Tracked Query

Use when you intend to modify entities and save changes:

```csharp
var admin = await uow.Users
    .QueryTracked()
    .Where(u => u.Id == 123)
    .FirstAsync(ct);

admin.DisplayName = "Updated";
await uow.SaveChangesAsync(ct);
```

Tracked queries cost more but enable persistence.

---

### 🔷 Non-Tracked Query

Use for APIs, reports, display data, analytics, background jobs — almost every read scenario:

```csharp
var users = await uow.Users
    .QueryUnTracked()
    .Where(u => u.IsActive)
    .OrderBy(u => u.LastName)
    .ToListAsync(ct);
```

Benefits:
- lower memory
- no EF change tracking overhead
- safer (nobody accidentally edits something that wasn’t meant to change)

This is your **default** read strategy.

---

### 🔷 RowTracked & RowUnTracked  
*(why these exist, and yes this matches EF Core mental model)*

EfBoost provides:

```csharp
var user = await uow.Users.RowUnTrackedAsync(u => u.Email == email, ct);
```

and

```csharp
var user = await uow.Users.RowTrackedAsync(u => u.Email == email, ct);
```

This intentionally mirrors the **two dominant EF Core usage patterns**:

| EF Pattern | EfBoost Equivalent |
|-----------|--------------------|
`QueryTracked()` + `RowTrackedAsync()` | `RowUnTrackedAsync(...)` |
`QueryUnTracked()` + `RowByKeyUnTrackedAsync()` | `RowByKeyTrackedAsync(...)` |

So yes:
✔ These exist to support the familiar EF Core mental model,  
but expressed with **clearer intent and safer defaults**.
Instead of First, Single, FirstOrdefault  we supply Row- methods that behave like FirstOrDefault - no exeptions thrown.  

If you still insist on using the First- or Single' pattern throwing errors, you can still use the IQueryable result for that though.  
I.e.  
```csharp 
var user = await uow.Users.QueryUntracked().Where(tt => Id == 20).SingleAsync(ct);   //Thrhows error when 2 rows with same Id
```

Why this is useful:

- Keeps code explicit about tracking vs non-tracking
- Matches how developers already reason about EF queries
- Avoids the “did someone forget AsNoTracking?” problem
- Removes API noise (`QueryUnTracked()...FirstOrDefaultAsync()`) in favor of meaningful verbs

You still can absolutely build queries and call normal EF `.FirstOrDefaultAsync()` yourself.  
These helpers just encode common intent more elegantly.

---

### 🔷 Materializing Queries — why have helpers instead of always calling `ToListAsync()`?

Yes, sometimes you will use EF directly:

```csharp
var list = await uow.Users.QueryUnTracked()
    .Where(u => u.IsActive)
    .ToListAsync(ct);
```

Totally fine.

But repo helpers are valuable when you want:

✔ clear naming  
✔ consistent handling  
✔ optional built-in limits / filters  
✔ more readable intent  

```csharp
var users = await uow.Users.QueryUnTrackedAsync(u => u.IsActive, ct);
```

Same result, clearer meaning:  
> “Give me a read-only, list of matching users.”

Both are allowed. Use what keeps your code base consistent and expressive.

---

### 🔷 Standard `long Id` Helpers

Many systems standardize on:

```
Id BIGINT (long)
Auto-increment
Primary key
On almost every table
```

EfBoost embraces that convention.

So in addition to generic `RowByKeyUnTrackedAsync(...)`, you get ergonomic helpers:

```csharp
var user = await uow.Users.RowByIdUnTrackedAsync(12345,ct);
```

No guessing.
No array of objects.
No awkward composite key dance.

If your world uses auto-generated long primary keys, these are extremely pleasant.

---

## OData Support

OData is powerful — and dangerous when applied blindly to a `DbSet`.

In typical WebAPI setups, an OData-capable controller provides an `ODataQueryOptions` instance that describes the requested filtering, ordering, and paging.  
EfBoost deliberately does **not** expose raw `DbSet` access for OData. Instead, it offers **controlled entry points** that let you decide how much freedom the client gets.

EfCore.Boost currently supports two OData usage paths:

- `<T>.FilterODataAsync`
A convenience method for the most common use case: applying client filters, paging, and optional count on top of a query boundary you define.
- `<T>.Plan-first OData pipeline` For more advanced scenarios, use BuildODataQueryPlan and explicitly choose how the request is executed (typed results, expand-as-include, or shaped responses).

Both paths enforce the same safety principles: OData is always applied on top of a query you own, and client freedom is governed by `ODataPolicy` options.

---

### 🔷 FilterODataAsync

Apply OData behavior **onto a query you control**.

```csharp
var baseQuery = uow.Users.QueryUnTracked()
    .Where(u => u.IsActive);

var result = await uow.Users.FilterODataAsync(
    baseQuery,
    options,
    policy: new ODataPolicy(
        MaxTop: 200,
        AllowOrderBy: true,
        AllowCount: true
    ),
    forceCount: true,
    ct
)
```

Typical uses:

- apply filtering only
- apply filtering with paging
- apply filtering with ordering
- retain full control over **which data is even eligible**

This pattern ensures that the *base query defines the boundary*, and OData can only refine what you have explicitly allowed.

---

#### 🔷 Advanced OData Pipeline

For scenarios that require more control, EfCore.Boost exposes a plan-first OData pipeline.

Instead of immediately executing a query, you first build an OData query plan using BuildODataQueryPlan.
From that plan, you explicitly decide how the request is handled:

-  materialize typed entities
- low limited $expand via include-style loading
- produce shaped results when $select or projection $expand is used

This approach separates what is allowed (via ODataPolicy) from how the query is executed, making advanced OData usage explicit, predictable, and safe.

See more details about OData handling  in  
[`Odata.md`](./OData.md).

---

## Bulk Insert  
*(high-throughput throughput when you need to push high-volume data)*

Bulk insert exists because there comes a moment in every growing system where normal EF insert loops start crying softly in the corner.

EfBoost Bulk Insert:

```csharp
await uow.LogEntries.BulkInsertAsync(listOfEvents);
```

This is not a “just batching adds” helper.  
It uses **true provider-native high-performance channels**:

| Provider | What EfBoost Uses |
|----------|-------------------|
SQL Server | SqlBulkCopy |
PostgreSQL | COPY (binary) |
MySQL | Optimized multi-row parameterized batching |

Why this matters in real systems:

- importing millions of rows
- telemetry ingestion
- log batching
- synchronization jobs
- ETL pipelines
- event processing streams

### Key Advantages

✔ does not populate EF change tracker  
✔ fewer round-trips  
✔ significantly lower CPU  
✔ dramatically better throughput  
✔ respects identity handling when required  
✔ participates in existing transactions when present  

And yes, in practice:

🏎 On SQL Server & PostgreSQL this is **much faster** than even well-optimized non-tracked EF inserts.

Full details, behavior guarantees, tuning guidance and caveats:

📄 [`BulkInserts.md`](./BulkInserts.md) 

---

# 🧾 Summary

DbRepo turns raw `DbSet` access into a **disciplined and capable data access layer**.

It gives every entity (and view):

- a **clear access surface**
- intentional **tracked vs non-tracked** usage
- reliable **key lookup**
- controlled and safe **OData integration**
- real-world **bulk operations**
- expressive helpers like `RowUnTrackedAsync`
- scale-aware streaming and existence checks
- provider-aware behavior across SQL Server, PostgreSQL, and MySQL

Repositories live inside a **Unit of Work**, meaning:

- access boundaries are explicit  
- read-only vs read/write semantics are enforced  
- structure replaces “direct DbContext chaos”  

You still write EF-like queries.  
You still get the power of LINQ.  
But now you get:

- better clarity  
- fewer mistakes  
- more performance headroom  
- a design that scales from small systems to serious enterprise workloads  

EfBoost keeps DbContext behind a thoughtful API,  
so your application code feels safer, cleaner, and more purposeful.
