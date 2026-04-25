# Getting Started with EfCore.Boost

This guide walks you from a fresh template to a working data model and database, and explains the recommended workflow for migrations and SQL deployment.

---

## 1) Create a Project from the Template

Use one of the provided templates:

- **Simple** → Model + Migrate
- **Simple.Solution** → Model + Migrate + Test (+ optional Web)

CLI example:

```bash
dotnet new boostsimple -n YourProjectName
```

> In Rider / Visual Studio, create a **new solution** from the template (not “Add Project”).

---

## 2) Understand the Generated Structure

Typical output:

- **YourApp.Model** → DbContext (`BoostCTX`), entities, UoW
- **YourApp.Migrate** → console tool for schema deployment and data import

See also: fileciteturn2file0

---

## 3) Extend Your Model

Work in the **Model** project:

- Add entities
- Configure relationships and mappings
- Apply conventions (EfCore.Boost)

Update your UoW to expose repositories and routines:

```csharp
public EfLongIdRepo<Customer> Customers => new(Ctx, DbType);
```

Keep the UoW as your **application-facing surface**.

---

## 4) Configure Connection(s)

Set your connection name(s) in `appsettings.json`.

The factory (or direct UoW creation) will use:

```json
{
  "DefaultAppConnName": "YourConnectionName"
}
```

---

## 5) Generate Migrations

From the `/Ps` folder, run the provider script. There are two types of scripts:

### A) Initial / Rebuild
Use these for the first migration or when you want to start fresh. **Note: These wipe existing migrations for the provider.**

```bash
./PgMigrate.ps1   # PostgreSQL
./MsMigrate.ps1   # SQL Server
./MyMigrate.ps1   # MySQL
```

### B) Incremental (AddMigration)
Use these to add a new migration on top of existing ones. These preserve history and allow merging manual SQL files (e.g., `SQL/MsSql-1.sql`).

```bash
./PgAddMigration.ps1
./MsAddMigration.ps1
./MyAddMigration.ps1
```

This updates migrations based on your current model.

---

## 6) Apply Migrations

Use the migration utility:

```bash
YourApp.Migrate check
YourApp.Migrate create
YourApp.Migrate update
```

This is the **recommended approach**.

### Why use the migration project?

- Clean separation from runtime application
- Central place for schema operations
- Works consistently across environments
- Integrates with generated SQL scripts

---

## 7) SQL Deployment Scripts (Important)

In addition to runtime migration, the template generates SQL scripts:

```text
/SQL/DbDeploy_MsSQL.sql
/SQL/DbDeploy_PgSQL.pgsql
/SQL/DbDeploy_MySQL.mysql
```

These are critical because:

- Views and stored procedures often require **manual SQL**
- Enterprise environments prefer **script-based deployment**
- Allows DBAs to review changes before execution

👉 In real-world setups, **SQL scripts are often the primary deployment method**, not runtime EF migrations.

---

## 8) Seed Data Strategy

You have two options:

### A) Model seeding (`HasData`)
- Good for small static data
- Included in migrations
- Can clutter model if overused

### B) External / import seeding (recommended for real data)
- Use migration tool:

```bash
YourApp.Migrate import
```

- Better for larger datasets
- Keeps model clean
- Works well with SQL deployment

---

## 9) Use the Model in Your Application

Reference only the **Model project**:

- Add project reference to `YourApp.Model`
- Configure connection string

Then either:

### Direct usage (tools)

```csharp
using var uow = new YourAppUow(configuration, connectionName);
```

### Application usage (DI + factory)

```csharp
builder.Services.AddSingleton<IYourAppUowFactory, YourAppUowFactory>();
```

(See UoW documentation for patterns)

---

## 10) Typical Workflow Summary

1. Update model (entities + UoW)
2. Generate migrations
3. Review SQL output
4. Apply via migration tool or SQL scripts
5. Import seed data (if needed)

---

## Where to go next

- UoW patterns → `docs/uow/uow-factory-pattern.md`
- Model building → `src/EfCoreBoost/Model/ModelBuilding.md`
- Migration details → `YourApp.Migrate/README.md`

---

## Final Notes

- Keep **Model project clean and focused**
- Use **migration project for all schema operations**
- Treat **SQL scripts as first-class artifacts**

This workflow scales well from local development to enterprise deployment.
