# Routine Design Guidelines for Cross-Platform Databases  
*(SQL Server · PostgreSQL · MySQL · EfBoost / DbRepo)*

This document defines **portable design rules** for routines (Stored Procedures/Functions) used with **EfBoost / DbRepo**.

The goals are:

- One mental model for all providers (SQL Server, PostgreSQL, MySQL)
- No provider-specific contortions in DbRepo
- Clear mapping to EfBoost `RunRoutine*` helpers

Output parameters exist per engine, but do **not** behave uniformly.  
EfCore.Boost cross-platform contract avoids them completely.  
To be clear you can use OUT / INOUT / OUTPUT parameters in code intentended for a specific provider.  
However if you do **your .net code is no longer portable across database engines**.

---

## 1. Routine Categories and EfBoost Helpers

DbRepo / EfBoost exposes the following routine helpers in UoW:

---

### 1.1 NonQuery routines

For side-effect-only routines:

```csharp 
async Task<int> RunRoutineNoneQueryAsync(string schema, string routineName, List<DbParmInfo>? parameters = null);  
```  

**Contract**

- Routine performs side effects (delete, cleanup, recalc, etc.).
- Return value is `rowsAffected` or provider-specific `ExecuteNonQuery` count.
- Routine must **not** use OUT/INOUT parameters.
- Routine may optionally SELECT a trivial scalar or message, but DbRepo ignores it for `NoneQuery` helpers.

---

### 1.2 Scalar routines

For a single scalar value:

```csharp 
async Task<long?>   RunRoutineLongAsync   (string schema, string routineName, List<DbParmInfo>? parameters = null)  
async Task<int?>    RunRoutineIntAsync    (string schema, string routineName, List<DbParmInfo>? parameters = null)  
async Task<string?> RunRoutineStringAsync (string schema, string routineName, List<DbParmInfo>? parameters = null)
```  

**Contract**

- Routine returns exactly one scalar value.
- Data is returned via **standard query semantics**:
  - First column of first row from SELECT
  - Or scalar-returning function (PostgreSQL)
- No OUT / INOUT / OUTPUT parameters.

The underlying `DbContext` executes the routine as a command and reads the first column of the first row into the requested CLR type.

**Example usage (your method inside the UOW):**
```csharp 
public async Task<long?> GetMaxIdByChanger(string changer)
{
    var paList = new List<DbParmInfo> { new("@Changer", changer) };
    return await RunRoutineLongAsync("my", "GetMaxIdByChanger", paList);
}
```
or
```csharp 
public async Task<long?> GetMaxIdByChanger(string changer)
{
    return await RunRoutineLongAsync("my", "GetMaxIdByChanger", [new("@Changer", changer)]);
}
```
even
```csharp 
public async Task<long?> GetMaxIdByChanger(string changer) => await RunRoutineLongAsync("my", "GetMaxIdByChanger", [new("@Changer", changer)]);
}
```

**Example routines from our TestDb:**  
Stored procedure for MySQL and SQL Server.  
Function for PostgreSQL.  
All are mapped uniformly by the `RunRoutineLongAsync` method.
```SQL 
DELIMITER $$
-- MySQL procedure test scalar result
CREATE PROCEDURE my_GetMaxIdByChanger(IN Changer VARCHAR(50))
BEGIN
    SELECT MAX(Id) FROM my_MyTable WHERE LastChangedBy = Changer;
END$$
```
```SQL
-- Postgres function test scalar result
CREATE OR REPLACE FUNCTION my."GetMaxIdByChanger"(Changer VARCHAR(50)) RETURNS BIGINT AS $$
BEGIN
    RETURN (SELECT MAX("Id") FROM my."MyTable" WHERE "LastChangedBy" = Changer );
END;
$$ LANGUAGE plpgsql;
```
```SQL 
-- SQL Server procedure test scalar result
Create PROCEDURE [my].[GetMaxIdByChanger](@Changer nvarchar(50)) AS
BEGIN
	SET NOCOUNT ON;
    SELECT max(Id) from my.MyTable where LastChangedBy = @Changer;
END
go
```
---

### 1.3 Tabular routines (simple scalar lists)

Convenience helpers for “one-column” result sets:

```csharp 
async Task<List<long>>   RunRoutineLongListAsync   (string schema, string routineName, List<DbParmInfo>? parameters = null)  
async Task<List<int>>    RunRoutineIntListAsync    (string schema, string routineName, List<DbParmInfo>? parameters = null)  
async Task<List<string>> RunRoutineStringListAsync (string schema, string routineName, List<DbParmInfo>? parameters = null)
```  

**Contract**

- Routine returns 0–N rows, one column.
- Data is returned via SELECT.
- No OUT / INOUT parameters.

**Example usage:**
```csharp 
public async Task<List<long>> GetNextSequenceIds(int count)
{
    return await this.RunRoutineLongListAsync("my", "ReserveMyIds", [new("@IdCount", count)]);
}
```

---

### 1.4 Fully tabular routines

For arbitrary row shapes mapped to EF Core models:

```csharp 
IQueryable<T> RunRoutineQuery<T>(string schema, string routineName, List<DbParmInfo>? parameters = null)  
    where T : class;
```  

**Contract**

- Routine returns 0–N rows.
- Columns must map to T.
- Returned via SELECT.
- No OUT / INOUT parameters.

**Example usage:**

```csharp 
public async Task<List<CurrentMenuItemsV>> GetCurrentMenuItemsForSession(long myId)
{
    return await RunRoutineQuery<CurrentMenuItemsV>("my", "GetCurrentMenuItemsForSession", [ new("@SessionId", sessionId) ]).ToListAsync();
}
```  

---

## 2. OUT / INOUT / OUTPUT Parameters

Output parameters exist, but are **not portable**:

| Engine                   | OUT semantics as real parameters? | Notes                                              |
|--------------------------|------------------------------------|----------------------------------------------------|
| SQL Server (procedures)  | ✅ Yes                             | @p OUTPUT                                         |
| MySQL (procedures)       | ✅ Yes                             | OUT / INOUT act as bound parameters               |
| PostgreSQL (functions)   | ❌ No                              | OUT becomes result columns, called via SELECT     |
| PostgreSQL (procedures)  | ❌ No pure OUT                     | Only IN / INOUT, different client semantics       |

### DbRepo Rule

OUT / INOUT / OUTPUT parameters:

- Not part of cross-platform contract  
- Must not be used for primary results  
- Allowed only in provider-specific code outside DbRepo portability guarantees

---

## 3. Naming Conventions and Schema Handling

EfBoost / DbRepo uses a logical routine identity:

Schema + Routine Name:

```csharp 
schema: "my"  
routine: "GetMaxIdByChanger"
```  
**UOW usage:**

```csharp 
public async Task<long?> GetMaxIdByChanger(string changer) {
    return await RunRoutineLongAsync("my", "GetMaxIdByChanger", [ new("@Changer", changer) ]);
}
```  

---

### SQL Server

- Schema is real SQL schema.
- Routine name is unchanged.

```SQL 
CREATE PROCEDURE [my].[GetMaxIdByChanger](@Changer nvarchar(50)) AS ... 
```  

---

### PostgreSQL

- Schema is real schema.
- Implemented as a FUNCTION for scalar logic.
- Function name is wrapped in double quotes.

```SQL
CREATE OR REPLACE FUNCTION my."GetMaxIdByChanger"(Changer text) RETURNS bigint LANGUAGE plPgSQL AS $$ ...
```  

Note: When referencing object names in PostgreSQL, use double quotes to preserve casing since migrations build Case-Sensitive names for Postgres.

---

### MySQL

MySQL uses schema prefix → name composition rule:

```SQL 
CREATE PROCEDURE my_GetMaxIdByChanger(IN Changer VARCHAR(50)) ... 
```  

DbRepo resolves this automatically.

---

## 4. Official EfBoost Doctrine

1 Tabular → SELECT → RunRoutineQuery or List helpers  
2 Scalar → scalar SELECT / function return → RunRoutineScalar helpers  
3 NonQuery → side effects → RunRoutineNoneQuery  
4 OUT / INOUT → Not allowed in portable routines

Clean, predictable, portable.
