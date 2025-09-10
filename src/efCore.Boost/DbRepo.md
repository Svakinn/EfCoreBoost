# DbRepo: Async-First Repository Layer for EF Core

## Purpose
The `DbRepo` component is designed to provide a consistent, extensible, and high-performance abstraction over Entity Framework Core's `DbContext` model. Its goal is to:

- Promote clean separation of data access concerns
- Maximize performance through asynchronous-only APIs
- Minimize boilerplate and allow high-level operations like OData and bulk persistence
- Be compatible across multiple database types (SQL Server, PostgreSQL, MySQL)

---

## Key Design Principles

### 1. **Async-Only API Design**

We have deliberately removed all synchronous operations from the public API. This is based on:

- Best practices in modern .NET server applications (especially in Azure or containerized workloads)
- Avoiding deadlocks and thread starvation under ASP.NET Core
- Improving scalability by leveraging the `I/O`-non-blocking nature of `async/await`

All query and command methods return `Task<T>` or `IAsyncEnumerable<T>`, and follow proper async naming conventions (`ByIdAsync`, `StreamNoTrackAsync`, etc.).

### 2. **OData Query Support**

By implementing `IODataQueryable<T>`, the repository layer can expose typed queries to be consumed by ASP.NET OData endpoints. Methods like `ApplyOdataFilterAsync` process standard `ODataQueryOptions` and return paginated and filtered results.

This allows full integration with clients that support OData (e.g., Angular, Excel, Power BI) without leaking database internals into your controller layer.

### 3. **Tracked vs. Non-Tracked Queries**

EF Core's `ChangeTracker` incurs cost and memory overhead. We separate access patterns:

- `Query()` returns tracked entities for write scenarios
- `QueryNoTrack()` returns read-only entities (as does `StreamNoTrackAsync`, `ByIdNoTrackAsync`, etc.)

This design prevents accidental misuse (e.g., updating a non-tracked entity) while allowing precise control.

### 4. **Raw SQL and Bulk Operations**

Real-world systems often require operations beyond EF's default capabilities. We support:

- `GetBoolScalarAsync`, `GetLongScalarAsync`, `GetDecimalScalarAsync` for raw value queries
- `BulkDeleteAsync` to delete by IDs using `DELETE FROM ... WHERE Id IN (...)`
- `BulkInsertAsync` using `SqlBulkCopy` for SQL Server only

These operations require knowledge of table and schema names. Rather than hardcoding them, we extract them from EF metadata and quote them based on database provider.

### 5. **Database Type Detection**

To support safe quoting and provider-specific logic, we use a `DatabaseType` enum (e.g., `SqlServer`, `PostgreSql`, etc.) injected from the UOW layer.

This enables the repository to correctly format table names, handle parameterized raw SQL, and choose optimized insert/delete logic.

---

## Common Use Cases

- **Get a customer**: `var cust = await _uow.Customers.ByIdAsync(id);`
- **Get a paged, filtered result**: `await _uow.Customers.ApplyOdataFilterAsync(options, _uow.Customers.QueryNoTrack());`
- **Insert many rows**: `await _uow.Orders.BulkInsertAsync(orderList);`
- **Delete by IDs**: `await _uow.Orders.BulkDeleteAsync(ids);`
- **Low-level query**: `await _uow.Reports.GetDecimalScalarAsync("SELECT SUM(...) ...");`

---

## Future Considerations

- Optional support for PostgreSQL `COPY` and MySQL `LOAD DATA INFILE` for `BulkInsertAsync`
- Partial updates using `MarkModified`
- Filtering `.Query()` at repo level using conventions or security filters
- Auto-registration of repositories through DI

---

## Summary
`DbRepo` is a pragmatic, powerful abstraction over EF Core that supports modern usage patterns like async/await, OData, raw SQL access, and high-throughput data operations across multiple database engines. By avoiding sync APIs and leveraging metadata, it provides safety, clarity, and performance without sacrificing flexibility.

