# EfCore.Boost Analyzers + Code Fixers

EfBoost ships with a small Roslyn analyzer + code-fix pack that targets one thing:

**Avoid silly (and expensive) sync/async mistakes in EF Core, repositories, and Unit-of-Work code.**

If you write `async` code, EfBoost nudges you to:
- **await** important async calls (no “fire and forget” DB work)
- avoid calling **sync** repo/UOW methods inside `async` methods
- avoid **blocking** on EF async queries (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`)
- use **async EF terminal operators** (`ToListAsync`, `FirstAsync`, …) instead of sync terminals

And when you slip, the IDE can offer a one-click repair via **code fixers**.

---

## Rule index (purpose)

| ID | Purpose | Typical mistake it prevents |
|---|---|---|
| **EFB0001** | Async UOW calls must be awaited | `uow.SaveChangesAsync();` gets forgotten and never awaited |
| **EFB0002** | Don’t use sync UOW calls inside `async` methods | `SaveChangesSynchronized()` blocks inside `async` |
| **EFB0003** | Don’t execute EF queries synchronously inside `async` methods | `ToList()` inside `async` -> thread blocking |
| **EFB0004** | Don’t block on EF async queries inside `async` methods | `.Result/.Wait()/GetResult()` -> deadlocks, starvation |
| **EFB0005** | Async repository calls must be awaited | `repo.ByKeyAsync(id);` fire-and-forget |
| **EFB0006** | Don’t use sync repository calls inside `async` methods | `ByKeySynchronized()` blocks inside `async` |
| **EFB0007** | (Analyzer only) Avoid sync repo methods in async codepaths | Same intent as EFB0006, but broader sync repo surface |

> Note: Code fix availability depends on the rule. In this set, fixers exist for **EFB0001–EFB0006**.

---

# Rules (with examples)

## EFB0001 — Async UOW calls must be awaited

### What it catches
Calling important async UOW operations without awaiting them.

### Why it matters
A missing `await` can mean:
- writes don’t happen when you think they do
- exceptions get lost
- transaction scopes behave unpredictably

### Error example
```csharp
public async Task SaveAsync()
{
    _uow.SaveChangesAsync(); // EFB0001
}
```

### Fix
```csharp
public async Task SaveAsync()
{
    await _uow.SaveChangesAsync();
}
```

### Code fix
✅ Adds `await` to the invocation.

---

## EFB0002 — Don’t use sync UOW calls inside `async` methods

### What it catches
Calling the sync “Synchronized” UOW methods inside `async` methods.

### Why it matters
Sync UOW calls block threads and can cause:
- thread pool starvation under load
- accidental deadlocks (depending on context)
- inconsistent performance vs proper async

### Error example
```csharp
public async Task UpdateAsync()
{
    _uow.SaveChangesSynchronized(); // EFB0002
}
```

### Fix
```csharp
public async Task UpdateAsync()
{
    await _uow.SaveChangesAsync();
}
```

### Code fix
✅ Renames to the mapped async counterpart and adds `await`.
Example: `CommitTransactionSynchronized()` -> `await CommitTransactionAsync()`.

---

## EFB0003 — Don’t execute EF queries synchronously inside `async` methods

### What it catches
Using sync EF “terminal” operators in an `async` method (e.g. `ToList()`, `First()`, `Single()`, `Count()`, etc.) when async equivalents exist.

### Why it matters
Sync terminals block threads, which defeats the point of async web APIs and can reduce throughput.

### Error example
```csharp
public async Task<List<User>> GetActiveAsync()
{
    return _ctx.Users.Where(u => u.IsActive).ToList(); // EFB0003
}
```

### Fix
```csharp
public async Task<List<User>> GetActiveAsync()
{
    return await _ctx.Users.Where(u => u.IsActive).ToListAsync();
}
```

### Code fix
✅ Converts `<Terminal>()` to `<Terminal>Async()` and wraps with `await`.

---

## EFB0004 — Don’t block on EF async queries inside `async` methods

### What it catches
Blocking on EF async query tasks via:
- `.Result`
- `.Wait()`
- `.GetAwaiter().GetResult()`

### Why it matters
Blocking on async is where deadlocks and starvation like to hide.
Even when it “works”, it’s usually slower and more fragile.

### Error examples
```csharp
var list = query.ToListAsync().Result;                  // EFB0004
query.ToListAsync().Wait();                             // EFB0004
var x = query.ToListAsync().GetAwaiter().GetResult();   // EFB0004
```

### Fix
```csharp
var list = await query.ToListAsync();
```

### Code fix
✅ Replaces the blocking pattern with `await <asyncInvocation>()`.

---

## EFB0005 — Async repository calls must be awaited

### What it catches
Calling repo async methods as bare statements (fire-and-forget).

### Why it matters
Most repo async methods are not “background tasks”.
If you don’t await them, you get:
- lost exceptions
- incomplete DB operations
- confusing ordering bugs

### Error example
```csharp
public async Task LoadAsync()
{
    _uow.Customers.ByKeyAsync(13); // EFB0005
}
```

### Fix
```csharp
public async Task LoadAsync()
{
    await _uow.Customers.ByKeyAsync(13);
}
```

### Code fix
✅ Adds `await` to the repo invocation.

---

## EFB0006 — Don’t use sync repository calls inside `async` methods

### What it catches
Calling `...Synchronized()` repo methods inside `async` methods.

### Why it matters
Same story as EFB0002: blocking in async codepaths reduces throughput and increases risk.

### Error example
```csharp
public async Task<Customer?> GetAsync(long id)
{
    return _uow.Customers.ByKeySynchronized(id); // EFB0006
}
```

### Fix
```csharp
public async Task<Customer?> GetAsync(long id)
{
    return await _uow.Customers.ByKeyAsync(id);
}
```

### Code fix
✅ Renames `*Synchronized` -> `*Async` and adds `await`.

---

## EFB0007 — Avoid sync repository methods in async codepaths (analyzer only)

### What it catches
The analyzer flags sync repo calls used inside `async` methods even when the method name is not strictly `*Synchronized()` (depending on your repo surface).

### Why it matters
It keeps “async all the way down” consistent across the repo API.

### Fix
Use the async equivalent and `await` it.

> No code fix currently included for EFB0007.

---

## Tips

- If you intentionally want to keep a sync codepath, keep it sync end-to-end (don’t mix in `async`).
- If you see an EFB diagnostic inside a method that *should not* be async, consider removing `async` entirely and returning a completed task (or refactor the callchain).
