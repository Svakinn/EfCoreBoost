# Bulk Inserts  
## High-throughput Data Persistence in EfCore.Boost

Bulk Insert in EfCore.Boost is designed for scenarios where large volumes of data must be persisted efficiently and reliably. Standard EF Core insert pipelines are appropriate for normal transactional workloads, but they are not optimized for sustained high-volume ingestion. Bulk Insert provides a safe, high-performance alternative.

---

## When to Use Bulk Inserts

Bulk insert operations are appropriate when:

### Large Data Imports
- customer data onboarding  
- historical dataset loading  
- ETL imports  

### Replication and Synchronization
- delta synchronization  
- backfill jobs  
- inter-system data replication  

### High-Throughput Operational Workloads
- application logging  
- telemetry ingestion  
- auditing streams  
- performance metrics  

In these workloads, the optimizations in EF Core’s normal unit-of-work pipeline become overhead. Bulk Insert is intended for sustained throughput and operational resilience.

---

## How EfBoost Implements Bulk Inserts

EfBoost does **not** attempt to batch normal EF inserts.  
It directly uses **native bulk ingestion mechanisms** per database provider:

| Provider | Implementation |
|--------|------------------|
SQL Server | SqlBulkCopy (binary streaming insert) |
PostgreSQL | COPY (binary mode) |
MySQL | Optimized multi-row parameterized batching |

This provides:

- minimal round trips  
- significantly reduced CPU cost  
- predictable performance under load  
- transactional safety  
- correct identity handling when required  

Where applicable, bulk operations automatically participate in the current transaction; otherwise, they operate inside their own transaction boundary.

---

## Usage

Bulk insert usage is intentionally simple:

```csharp
await uow.MyTable.BulkInsertAsync(list);
```

No special EF configuration, no metadata handling, and no external packages are required.

---

## Example: Logging Pipeline Using Bulk Insert

The following example demonstrates how bulk insert is used to support a high-throughput structured logging solution. Error logs are buffered in memory and persisted as efficient bulk operations.

This domain uses EfBoost’s model conventions and attributes to ensure cross-database consistency.

---

### Error Log Model

```csharp
[Index(nameof(LastChangedUtc), IsUnique = false, AllDescending = true)]
[Index(nameof(SessionId), IsUnique = false)]
[Index(nameof(Context), nameof(LastChangedUtc), nameof(SessionId), IsUnique = false)]
public class ErrorLog
{
    [DbAutoUid]
    public long Id { get; set; }

    public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;

    public long? SessionId { get; set; }

    public int Context { get; set; }

    [StrMed]
    public string? ErrorMsg { get; set; }

    [Text]
    public string? ErrorDetails { get; set; }

    [ForeignKey(nameof(Context))]
    public LogContext? LogContext { get; set; }

    public int Tenant { get; set; } = 1;
}
```

---

### Context Metadata Model

```csharp
[Index(nameof(Ctx), nameof(Tenant), IsUnique = true)]
public class LogContext
{
    [DbAutoUid]
    public int Id { get; set; }

    public int Ctx { get; set; }

    [StrMed]
    public string Name { get; set; } = string.Empty;

    [Text]
    public string? Description { get; set; }

    public int LogTypeId { get; set; } = 1;

    public int Tenant { get; set; } = 1;
}
```

These attributes are part of EfBoost modeling conventions.  
They define index rules, identity behavior, and structured string semantics across supported providers.

See: [ModelBuilding.md](../../Model/ModelBuilding.md) for details.

---

## Buffered Logging and Bulk Persistence

A simple buffered logging workflow:

```csharp
public class BufferedLogPersister
{
    private readonly UOWLogs _uow;
    private readonly List<ErrorLog> _pending = new();

    public BufferedLogPersister(UOWLogs uow)
    {
        _uow = uow;
    }

    public void AddError(string message, string? details, int tenant)
    {
        _pending.Add(new ErrorLog
        {
            ErrorMsg = message,
            ErrorDetails = details,
            LastChangedUtc = DateTimeOffset.UtcNow,
            Tenant = tenant
        });
    }

    public async Task FlushAsync()
    {
        if (_pending.Count == 0)
            return;
        await _uow.ErrorLogs.BulkInsertAsync(_pending);
        _pending.Clear();
    }
}
```

This achieves:

- decoupling application execution from persistence cost  
- controlled batching  
- transactional correctness  
- high sustained throughput  

---

## Behavior Characteristics

Bulk Insert guarantees the following:

- Uses provider-native optimized ingestion paths  
- Automatically respects identity columns when enabled  
- Reuses existing EF Core transactions when present  
- Creates internal transaction otherwise  
- Does not populate EF Core change tracker  
- Does not require subsequent `SaveChanges()`  

This makes it suitable for service workloads, infrastructure components, and processing pipelines.

---

## Data Quality & Constraint Considerations

Bulk insert assumes that incoming data is structurally valid. If a batch contains invalid rows (for example, rows violating primary key, unique key, or foreign key constraints), the database provider may reject the entire batch.

In production environments this must be considered explicitly. Recommended strategies include the following.

### 1️⃣ Bulk Insert to Staging + Controlled Merge  

Bulk insert the dataset into a **staging table** with minimal or no constraints, then consolidate the data into the target structure via:

- stored procedure  
- merge statement  
- controlled EF processing pipeline  

This allows:

- validation and cleansing  
- enrichment of references  
- duplicate handling  
- rejection and audit of invalid rows  

This is the most robust approach for external ingestion and ETL processes.

---

### 2️⃣ Skip Bulk Insert and Use Normal EF Processing  

If each entity requires domain validation, business logic execution, auditing, or lifecycle hooks, then bulk insert is not appropriate. Standard EF insert operations provide correctness guarantees and participate in the domain model lifecycle.

---

### 3️⃣ Pre-Validate Before Bulk Insert  

When performance matters but the dataset is moderately controlled, validation can be applied in the application layer before bulk insertion:

- check primary keys  
- verify foreign keys  
- normalize and sanitize data  
- remove invalid or duplicate payloads  

This preserves bulk insert performance while reducing risk of failure.

---

## Identity Handling

Many systems require retaining existing identity values during migration or replication. EfBoost supports identity preservation when explicitly requested.

This is typically needed when:

- migrating from another database  
- restoring preserved data segments  
- promoting between environments  
- performing synchronization activities  

### Including Existing Identity Values

Identity preservation is activated with:

```csharp
await uow.MyEntities.BulkInsertAsync(list, includeIdentityValues: true);
```

When enabled, EfBoost:

- inserts supplied identity values as-is  
- handles provider-specific identity insert rules  
- ensures consistency with expected identity ranges  
- maintains referential integrity expectation  

Where necessary, EfBoost also ensures identity sequences remain valid so that subsequent inserts continue from correct identity positions.

---

### When to Use Identity Preservation

Use identity preservation when:

- existing identities must remain stable  
- foreign references are externally defined  
- business processes depend on existing identifiers  

Do not enable identity preservation when identities are newly generated application state. In those scenarios database-managed identity generation remains preferable.

---

## When Not to Use Bulk Insert

Bulk Insert should not be used when:

- entities need to remain tracked after insert  
- workloads are small or infrequent  
- business logic requires per-entity execution  
- cascading domain rules depend on EF change tracking  

Standard EF insert behavior is recommended in those scenarios.

---

## Summary

Bulk Insert in EfCore.Boost is designed to support systems that must handle sustained, high-volume data insertion without sacrificing safety or correctness. It leverages native provider bulk ingestion mechanisms and integrates seamlessly into EfBoost’s repository and unit-of-work architecture.

Use it for:

- data import  
- replication workloads  
- structured logging  
- telemetry and auditing  
- performance-sensitive ingestion pipelines  

It provides performance stability, transactional behavior, and a simple API surface.  
For modeling conventions, identity semantics, and cross-provider considerations.
