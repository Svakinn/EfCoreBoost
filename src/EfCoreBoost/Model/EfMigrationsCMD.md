# EF Migrations Command Guide  
## Multi-provider migration guidance for EfCore-based systems

This document describes practical strategies for driving Entity Framework Core migrations when a project targets **multiple database flavors at once** (SQL Server, PostgreSQL, MySQL).  

Its purpose is to help you *cope with the problems EF migrations naturally create in such environments*, and show examples of how PowerShell automation can make that process repeatable instead of frustrating.

Important spirit of this document:

- This is **not enforced by EfCore.Boost**
- You are free to design your own migration process
- What follows is **a proven and comfortable approach**
- Scripts shown here are examples, not rules
- The BoostTest project demonstrates how developers *can* organize things effectively

---

## 1. Core EF Migration Commands

Everything ultimately runs on top of `dotnet ef`. These are the commands we automate rather than rewrite.

### 1.1. Add a migration

```
dotnet ef migrations add InitDbCore --context DbCoreContext --output-dir "Migrations/SqlServer"
```

### 1.2. Update / initialize a database

```
dotnet ef database update --context DbCoreContext
```

### 1.3. Generate SQL deployment scripts

```
dotnet ef migrations script --context DbCoreContext --output sql/InitDbCore.sql
```

EF remains in control. Our job is to **run these commands sanely across providers**.

---

## 2. Why PowerShell Helps

Managing migrations manually becomes painful when you have:

- three different database types
- multiple DbContexts
- provider-specific folders
- differing snapshot states

So instead of relying on discipline and memory, automation scripts:

- always call `dotnet ef` with correct settings
- ensure migrations land in correct folders
- temporarily adjust snapshot visibility when needed
- optionally generate final deployable SQL packages

---

## 3. The Snapshot Problem

EF Core keeps **one active model snapshot per DbContext**.  
That snapshot represents EF’s current understanding of the database model.

With a single database provider, this works well.  
With multiple providers, things become more complicated:

- provider-specific differences appear in the snapshot
- providers do not always evolve identically
- EF still expects a single authoritative snapshot
- migration diffs can become inaccurate or misleading

### The practical reality

For a given `DbContext`, EF Core can only work correctly with **one active snapshot at a time** when generating a new migration.

Because of this, a common strategy in multi-database solutions is to keep a **separate migrations project for each provider**.  
That approach is perfectly valid and is often the cleanest option for formal deployment pipelines.

### The EfCore.Boost approach

In the `EfCore.Boost` test project, and in the related project templates, we use a different setup:

- a **single migration project**
- support for **multiple providers**
- optionally support for **multiple DbContexts**

To make this work, we use a simple scripting approach:

- one provider snapshot is kept active
- the other provider snapshots are temporarily disabled
- the migration is generated
- the disabled snapshots are restored afterward

In practice, this is handled by a PowerShell script that renames snapshot files so EF only sees the snapshot for the provider currently being migrated.

This means the workflow is still deterministic and repeatable, while avoiding the overhead of maintaining one migration project per database flavor.

### About production migration histories

For formal production releases, you should still treat each provider’s migration history carefully.

That usually means:

- preserving the correct snapshot state for each provider
- keeping provider-specific migration history intact
- using that history when generating proper **incremental** migrations for production deployment

EfCore.Boost does not enforce one specific production process here.  
Instead, it provides a practical pattern that you can adapt to your own release and deployment workflow.

---

## 4. Strategy Used in the Templates

The templates and test projects follow this workflow:

1. Choose the provider you want to generate a migration for  
2. Keep that provider’s snapshot active  
3. Temporarily disable the snapshots for the other providers  
4. Run `dotnet ef migrations add ...` normally  
5. Restore the disabled snapshots afterward  

In our setup, “disable” simply means renaming the non-active `*ModelSnapshot.cs` files through PowerShell so EF Core ignores them during migration generation.

### Key takeaway

In many enterprise-grade multi-database solutions, the standard approach is to keep one migration project per provider.

EfCore.Boost shows an alternative approach that is lighter for development, testing, and templates:

- one migration project
- multiple providers
- one active snapshot at a time
- simple script-driven switching between providers

You are not changing how EF Core works.  
You are creating the conditions EF Core needs in order to generate correct migrations in a multi-provider setup.

---

## 5. Manual SQL is Still Valuable

EF is great for:

- tables
- relationships
- basic schema evolution

It is not intendent for:

- complex views
- performance-tuned SQL
- sequences
- stored routines

So a healthy hybrid model is:

- use EF migrations for the structural schema
- complement with handwritten SQL
- package them together for deployment

The examples below show how to merge EF-generated SQL and your own SQL into one deployable script.

---

## 6. PostgreSQL Example — Real Scripts

The following example comes from the Boost test environment.  
It shows:

- per-provider migration handling
- snapshot hiding
- EF migration execution
- merging multiple SQL sources into one deployment artifact

These scripts are not theoretical. They actually run.

---

### 6.1 Helper Script (shared logic)

This script exists separately so multiple migration workflows can reuse common helpers.

It currently:

- selects which app connection is “active” (switching between DB flavors)
- handles snapshot enabling / disabling per provider


```powershell
function Set-MigrationProvider {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectDir,
        [Parameter(Mandatory = $true)][ValidateSet("MsSQL","PgSQL","MySQL")][string]$Provider
    )
    if (-not (Test-Path $ProjectDir)) {
        throw "Migration project folder not found at: $ProjectDir"
    }
    $projFolder = $ProjectDir
    Write-Host "Migration project: $projFolder"
    Write-Host "Activating migrations for provider: $Provider"
    Write-Host ""
    $providers = @("MsSQL","PgSQL","MySQL")
    foreach ($p in $providers) {
        $migDir = Join-Path $projFolder "Migrations\$p"
        if (-not (Test-Path $migDir)) {
            Write-Host "Skipping missing folder: $migDir"
            continue
        }
        $enable = ($p -eq $Provider)
        if ($enable) {
            Write-Host "Preparing CLEAN migration folder for provider '$p' at $migDir"
            Get-ChildItem -Path $migDir -Recurse -File |
                Where-Object { $_.Extension -in '.cs', '.xcs' } |
                Remove-Item -Force
        }
        else {
            Write-Host "Disabling migrations under $migDir (provider '$p')"
            Get-ChildItem -Path $migDir -Recurse -File |
                Where-Object { $_.Extension -eq '.cs' } |
                ForEach-Object {
                    Rename-Item $_.FullName ($_.FullName + ".xcs") -Force
                }
        }
    }
    Write-Host ""
    Write-Host "Done. Active migration provider: $Provider"
}
```

---

### 6.2 pgMigrate.ps1 — PostgreSQL Migration + Merge Pipeline

This script:

- ensures other provider snapshots do not interfere
- runs EF migrations
- collects resulting SQL
- merges them with manual SQL scripts where required


```powershell
param(
    [string]$ProjectDir,
    [string]$ConnName = "TestPg",
    [string]$SqlOutFileName = "Migrations\DbDeploy_PgSQL.pgsql"
)
# If no ProjectDir passed, infer from script location:
# script lives in /PS under the project folder
if (-not $ProjectDir) {
    $ProjectDir = Split-Path $PSScriptRoot -Parent
}
$ErrorActionPreference = "Stop"
Write-Host "=== EF PgSQL: Creating initial migrations for Postgres ==="
# Build paths relative to project dir
$Project = Join-Path $ProjectDir "TestDb.Migrate.csproj"
Write-Host "PSScriptRoot: $PSScriptRoot"
Write-Host "ProjectDir:   $ProjectDir"
Write-Host "Project name: $Project"
Write-Host "ConnName:     $ConnName"
Write-Host ""
# Import helpers relative to this script
$helperPath = Join-Path $PSScriptRoot "helpers.ps1"
. $helperPath
# Make sure we have PgSQL as current provider
Set-MigrationProvider -ProjectDir $ProjectDir -Provider "PgSQL"
$migrations = @(
    @{ Name = "InitDbTest"; Context = "DbTest"; OutDir = "Migrations/PgSQL/MgDbTest"; OutFile = "Migrations/PgSQL/InitDbTest.pgsql" }
)
$filesInOrder = @(
    "Migrations/PgSQL/InitDbTest.pgsql",
    "SQL/PgSQL.pgsql"
)
foreach ($m in $migrations) {
    Write-Host "-> Creating migration $($m.Name) ($($m.Context))"
    dotnet ef migrations add $m.Name `
        --context $m.Context `
        --project $Project `
        --output-dir $m.OutDir `
        -- `
        --connName $ConnName
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations add failed for migration $($m.Name) ($($m.Context)) with exit code $LASTEXITCODE"
    }
    $outFile = Join-Path $ProjectDir $m.OutFile
    Write-Host "-> Creating migration SQL $($m.Name) ($($m.Context))"
    dotnet ef migrations script 0 $m.Name `
        --context $m.Context `
        --project $Project `
        --output $outFile `
        -- `
        --connName $ConnName
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations script failed for migration $($m.Name) ($($m.Context)) with exit code $LASTEXITCODE"
    }
}
$existingFiles = @()
foreach ($relPath in $filesInOrder) {
    $fullPath = Join-Path $ProjectDir $relPath
    if (Test-Path $fullPath) {
        $existingFiles += $fullPath
    } else {
        Write-Warning "Missing SQL file (skipping): $fullPath"
    }
}
if ($existingFiles.Count -eq 0) {
    throw "No input SQL files found. Nothing to merge."
}
$deployPath = Join-Path $ProjectDir $SqlOutFileName
@"
/*
    Database deploy script (PgSQL)
    Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    ConnName: $ConnName
*/
"@ | Set-Content -Path $deployPath -Encoding UTF8
foreach ($file in $existingFiles) {
    $name = [System.IO.Path]::GetFileName($file)
    Write-Host "Appending: $name"
    @(
        ""
        "/*** BEGIN $name ***/"
        ""
    ) | Add-Content -Path $deployPath
    Get-Content $file | Add-Content -Path $deployPath
    @(
        ""
        "GO"
        "/*** END $name ***/"
        ""
    ) | Add-Content -Path $deployPath
}
Write-Host ""
Write-Host "✅ Done. Deploy script created at:"
Write-Host "  $deployPath"
```

---

# 7. Simplifying Migration Setup with a Design-Time Factory

EF Core requires a **design-time DbContext factory** when using `dotnet ef` commands.  
In many projects, this becomes repetitive and tightly coupled to a specific database provider or connection.

EfCore.Boost introduces a clean, flexible pattern that keeps migration projects **provider-agnostic** and easy to maintain.

---

## The idea

Instead of hardcoding provider logic or connection strings into the migration project:

- use a shared **design-time factory base**
- resolve configuration dynamically
- select the connection via a parameter (`--connName`)
- let a central factory handle provider selection

This keeps your migration project lightweight and reusable across environments.

---

## Example (from test project)

```csharp
namespace TestDb.Migrate
{
    /// <summary>
    /// Design-time factory used by EF Core tooling (dotnet ef).
    ///
    /// SecureContextFactory resolves:
    /// - connection strings
    /// - provider (SqlServer, PostgreSQL, MySQL, etc.)
    /// - environment-specific settings (local, docker, Azure, ...)
    ///
    /// You can optionally pass a connection name:
    ///   dotnet ef migrations add Init -- --connName=PgLocal
    /// </summary>
    public sealed class DbTestContextFactory : DesignDbContextFactoryBase<DbTest>
    {
        protected override DbTest CreateContext(IConfigurationRoot configuration, string connName)
            => SecureContextFactory.CreateDbContextForMigrations<DbTest, DbTestContextFactory>(configuration, connName);
    }
}
```

---

## appsettings.json (in migration project)

The migration project contains its own configuration file, typically:

```json
{
  "DBConnections": {
    "MsSqlLocal": {
      "Provider": "SqlServer",
      "ConnectionString": "Server=localhost;Database=TestDb;Trusted_Connection=True;"
    },
    "PgLocal": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=localhost;Database=testdb;Username=postgres;Password=postgres"
    },
    "MySqlLocal": {
      "Provider": "MySql",
      "ConnectionString": "Server=localhost;Database=testdb;User=root;Password=root"
    }
  }
}
```

The `connName` parameter maps directly to this section.

---

## Running migrations

```bash
dotnet ef migrations add Init -- --connName=MsSqlLocal
dotnet ef migrations add Init -- --connName=PgLocal
dotnet ef migrations add Init -- --connName=MySqlLocal
```

Each run:

- selects a provider via configuration
- uses the same migration project
- produces provider-specific migrations

---

## Test project reference

You can find a working example here:

https://github.com/your-repo/EfCore.Boost/tree/main/test/TestDb.Migrate

(Adjust the link to match your actual repository structure.)

---

## How it fits with multi-provider migrations

Combined with the snapshot strategy:

- the **factory selects provider and connection**
- scripts ensure **only one snapshot is active**
- EF Core behaves as if it is working with a single provider

---

## Key takeaway

EfCore.Boost separates concerns cleanly:

- **factory → decides provider and connection**
- **configuration → defines environments**
- **scripts → control active snapshot**
- **EF Core → generates migrations**

This results in a flexible, low-friction setup without needing multiple migration projects.


---

## Summary

Key ideas from this guide:

- EF migrations were designed assuming one provider, one model → multiple providers require discipline
- PowerShell (or any scripting) is the right place to manage that discipline
- Only one snapshot can be active at a time per migration operation
- Storing snapshots per provider is a reliable and understandable pattern
- Production histories should be archived intentionally
- Manual SQL belongs respectfully beside EF migrations
- BoostTest shows a working blueprint, not a mandate

EfCore.Boost does **not** own your migration workflow.  
It simply lives comfortably inside one that has been thoughtfully designed.

