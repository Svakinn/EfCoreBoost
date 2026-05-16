# Smoke Test (End-to-End Integration Verification)

The smoke test (`BasicSmokeAsync`) is executed for **every supported database provider**:

- SQL Server  
- PostgreSQL  
- MySQL  
- Azure SQL  

Exactly the same test method is run for each provider.

The goal of this test is not to validate isolated micro-features.  
It validates that the **entire EfCore.Boost stack works end-to-end** on a real database:

- model mapping  
- Unit of Work  
- repositories  
- views  
- routines  
- bulk operations  
- transactions  
- OData pipelines  

If this test passes, the provider is considered functionally usable.  
See [UnitTestContainers.cs](./UnitTestContainers.cs#L299-L451) for the actual test code.

---

## Test entry point

```csharp
static async Task BasicSmokeAsync(UOWTestDb uow, UOWTestDb uow2, UOWTestView uowV)
```

At this point:

- the database exists
- migrations are applied
- seed data is present (from `TestDbData.cs`)

Everything that follows verifies runtime behavior rather than setup.

---

## 1. Basic insert and read

```csharp
var myRow = await uow.MyTables.QueryTracked().FirstOrDefaultAsync();
Assert.IsNotNull(myRow);
```

Confirms that:

- the database is reachable
- seeded data exists
- the repository pipeline works

Next we add a related row via navigation property:

```csharp
var refRow = new DbTest.MyTableRef { MyInfo = "ref", LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "Philip" };
myRow.MyTableRefs.Add(refRow);
await uow.SaveChangesAsync();
```

This validates:

- relationship mapping
- change tracking
- normal `SaveChangesAsync` behavior

Then we verify that the row was actually persisted:

```csharp
var found = await uow.MyTableRefs.RowUnTrackedAsync(tt => tt.Id == refRow.Id);
Assert.IsNotNull(found);
```

---

## 2. View lookup (and GUID auto-seeding)

```csharp
var viewItem = await uowV.MyTableRefViews.QueryUnTracked()
    .FirstOrDefaultAsync(tt => tt.RefId == refRow.Id);
Assert.IsNotNull(viewItem);
Assert.IsTrue((viewItem.RowID != Guid.Empty), "RowID should not be empty");
```
```

This validates:

- the view exists in the database  
- EF mapping for the view works  
- the `ViewKey` identity definition is correct  
- joins and projections inside the view behave correctly  

The additional check for `RowID` confirms that **GUID default generation / auto-seeding** works correctly across providers.

---

## 3. Routine call: ID reservation

```csharp
var idList = await uow.GetNextSequenceIds(10);
Assert.HasCount(10, idList, "Did not get 10 rows from sequence function");
```

This validates:

- routine invocation  
- parameter binding  
- resultset materialization  

Although implementations differ (stored procedure vs function), the C# call remains identical.

---

## 4. Bulk insert, delete, and identity/sequence behavior

This section validates several tightly related behaviors:

- bulk insert with explicit identities  
- bulk delete by ID  
- identity / sequence continuity  
- transactional behavior  

### Bulk insert with explicit identities

```csharp
var tt = new DbTest.MyTable { Id = 10, LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "gorm" };
var tt2 = new DbTest.MyTable { Id = 11, LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "gorm2" };
```

We deliberately use fixed identity values so later delete and verification steps are deterministic.

```csharp
await uow.RunInTransactionAsync(async ct =>
{
    await uow.MyTables.BulkInsertAsync([tt, tt2], true, ct); // true = insert with identities
}, ct: CancellationToken.None);
```

Important notes:

- Wrapped in an **outer transaction** using `RunInTransactionAsync`  
- Transaction envelope is **Azure retry-safe**  
- `BulkInsertAsync` already runs each batch inside its own transaction  
- You only wrap it when you want to group multiple operations atomically  

### Verify identity/sequence continuity

```csharp
uow.MyTables.Add(new DbTest.MyTable() { LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "swarm", RowID = Guid.NewGuid() });
await uow.SaveChangesAndNewAsync();
```

After explicitly inserting IDs 10 and 11, the next generated ID should be **12**.

### Bulk delete and verification

```csharp
await uow.MyTables.BulkDeleteByIdsAsync([10]);
var currIds = await uow.MyTables.QueryUnTracked().Select(xx => xx.Id).ToListAsync();

Assert.IsFalse(currIds.Any(xx => xx == 10), "Bulk delete failed, row 10 still exists");
Assert.IsTrue(currIds.Any(xx => xx == 11), "Bulk inserted row not found");
Assert.IsTrue(currIds.Any(aa => aa == 12), "Sequence not resetting after bulk-insert");
```

Both bulk insert and bulk delete execute inside transactions automatically.

---

## 5. Bulk insert without identities

```csharp
await uow.MyTables.BulkDeleteByIdsAsync([11]);
await uow.MyTables.BulkInsertAsync([tt, tt2]);
var row2 = await uow.MyTables.RowByKeyUnTrackedAsync(13);

Assert.IsNotNull(row2, "Bulk-insert without identities fail");
```

Validates:

- database-generated identities  
- bulk insert path without explicit IDs  
- identity/sequence remains consistent  

---

## 6. Scalar routine

```csharp
var fId = await uow.GetMaxIdByChanger("Stefan");

Assert.AreEqual(-2, fId, "Scalar routine did not return valid id");
```

Validates scalar routine execution and return-value mapping.

---

## 7. Transaction rollback

Two rows share the same unique value:

```csharp
var sameUniqueGuId = Guid.NewGuid();
var rb = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
var rb2 = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
```

Inside transaction:

- first insert succeeds  
- second insert violates constraint  

```csharp
try
{
    await uow.RunInTransactionAsync(async ct =>
    {
        uow.MyTables.Add(rb);
        await uow.SaveChangesAsync(ct);
        var insideExists = await uow.MyTables.QueryUnTracked().AnyAsync(t => t.Id == rb.Id, cancellationToken: ct);

        Assert.IsTrue(insideExists, "Row should be visible inside active transaction before rollback");

        uow.MyTables.Add(rb2);
        await uow.SaveChangesAsync(ct);
    }, ct: CancellationToken.None);
}
catch (Exception) { }
```

After transaction:

```csharp
var afterRollbackExists = await uow.MyTables.QueryUnTracked().AnyAsync(t => t.Id == rb.Id);

Assert.IsFalse(afterRollbackExists, "Row should not exist after rollback");
```

Validates full rollback and transactional consistency.

---

## 8. OData filter

```csharp
 var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=LastChangedBy eq 'Stefan'");
 var baseQuery = uow.MyTables.QueryUnTracked();
 var filteredResult = await uow.MyTables.FilterODataAsync(baseQuery, options);

 Assert.IsTrue(filteredResult.InlineCount > 0 && filteredResult.Results.All(x => x.LastChangedBy == "Stefan"), "We expect to find Stefan's, but only Stefan's");

 // Verify that data exist with linQ
 var normRow = await uow.MyTables.QueryUnTracked().Where(mm => mm.Id == -1).Include(xx => xx.MyTableRefs.Where(r => r.MyInfo == "BigData")).ToListAsync();
 
 Assert.IsNotEmpty(normRow);
```

Validates EDM generation, filter parsing, and translation.

---

## 9. OData expand-as-include

```csharp
 var bq = uow.MyTables.QueryUnTracked();
 var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
 var plan = uow.MyTables.BuildODataQueryPlan(bq, options2, new ODataPolicy(AllowExpand: true), true);
 var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);

 Assert.AreEqual(1, plan2.Report.Count(zz => zz == "ExpandInnerFilterIgnored:MyTableRefs"), "We did not find $filter warning within AsInclude query"); 

 var res = await uow.MyTables.MaterializeODataAsync(plan2);
 //we received our MyTableRefs records inline (but unfiltered)

 Assert.IsTrue(res.InlineCount is > 0 && res.Results.FirstOrDefault() != null && res.Results.FirstOrDefault()!.MyTableRefs.Count > 0, 
     "$expand as include failed to produce data for MyTableRefs");
```

Validates expand handling and include translation.

---

## 10. OData shaped ($select)

```csharp
  var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$select=Id");
  var plan3 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowSelect: true), true);
  var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);
  var res3 = await uow.MyTables.MaterializeODataShapedAsync(plan3, shapedQuery3);

  Assert.IsNotEmpty(res3.Results, "Filtered and selected query failed");

  var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);

  Assert.Contains("\"Id\"", json, $"$select=Id expected 'Id' in shaped JSON.\nJSON: {json}");
  Assert.DoesNotContain("LastChangedBy", json, $"$select=Id should not include 'LastChangedBy'.\nJSON: {json}");
  Assert.DoesNotContain("MyTableRefs", json, $"$select=Id should not include navigation 'MyTableRefs'.\nJSON: {json}");

  //Inner filter test for shaped expansion
  var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
  var plan4 = uow.MyTables.BuildODataQueryPlan(bq, opts4, new ODataPolicy(AllowExpand: true), true);
  var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
  var res4 = await uow.MyTables.MaterializeODataShapedAsync(plan4, shapedQuery4);

  Assert.IsNotEmpty(res4.Results, "Expected at least one result from $filter=Id eq -1 with expanded MyTableRefs, but none were returned.");
```

Validates projection, shaping, and reduced payload.

---

## Summary

This single smoke test verifies that:

- model mapping works  
- providers behave consistently  
- Boost conventions are applied correctly  
- core database features function end-to-end  

It is intentionally large because it validates **integration**, not isolated units.
