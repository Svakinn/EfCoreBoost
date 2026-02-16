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

EF keeps a **single model snapshot** per DbContext.  
That snapshot represents “what EF thinks the database currently looks like”.

When only one provider is used, this is fine.  
When multiple database engines are used:

- provider differences leak into snapshots
- each provider evolves slightly differently
- EF assumes **only one truth**
- migration diffs become confused or wrong

### The practical reality

At any given time, for a DbContext, **only one provider’s snapshot can be active** for EF to create a new migration correctly.

So a common and effective pattern is:

- have one migration folder per provider
- keep snapshots for each provider in their own area
- temporarily disable / hide snapshots for providers *you are not migrating right now*
- generate migration
- put everything back where it belongs

There are many ways to do this.  
This document shows one working example using file renaming.

### About production migration histories

If you are building a **formal public release** intended to be deployed onto production databases, you absolutely want to:

- preserve the correct snapshot state for each provider
- keep that snapshot aside safely
- later use it when generating strictly correct **incremental** migrations

Exactly how you manage long-lived official histories is outside the scope of EfCore.Boost itself.  
You are expected to adapt these scripting principles into your own production migration process.

---

## 4. Strategies 

1️⃣ Decide which provider you are currently generating a migration for  
2️⃣ Temporarily rename or move all `*ModelSnapshot.cs` files belonging to other providers  
3️⃣ Ensure only the active provider’s snapshot is visible to EF  
4️⃣ Run `dotnet ef migrations add …` normally  
5️⃣ Return renamed snapshots to their rightful folders afterward  

### Key takeaway

You are not tricking EF.  
You are helping EF live in a world it was not originally designed for.

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
function Set-DefaultAppConnName {
    param(
        [string]$SolutionRoot,
        [string]$Name
    )
    #it can be a little confusing where dotnet migration tool chooses to load appettings.json
    $settingsPath = Join-Path $SolutionRoot "xUnitTests\appsettings.json"
    if (-not (Test-Path $settingsPath)) {
        throw "appsettings.json not found at: $settingsPath"
    }
    Write-Host "Updating DefaultAppConnName -> '$Name'"
    $content = Get-Content $settingsPath -Raw
    $updated = $content -replace '("DefaultAppConnName"\s*:\s*")([^"]*)(")', "`$1$Name`$3"
    Set-Content $settingsPath $updated -Encoding UTF8
}

function Set-MigrationProvider {
    <#
    .SYNOPSIS
      Enables migrations for a single provider and disables the others by renaming .cs <-> .xcs.
    .DESCRIPTION
      This enables us to build migrations for each of the 3 provider types that each reqruie its own implementation of initial migration.
      So when we do migration for MsSQL, possible migration-cs files must be deleted or better yet renamed so there is ony one project-buildable-migration
      active at each point.

    .PARAMETER require
      Root of the solution (the folder containing xUnitTests, etc.)

    .PARAMETER Provider
      One of: MsSQL, PgSQL, MySQL
    #>
    param(
        [Parameter(Mandatory = $true)] [string]$SolutionRoot,
        [Parameter(Mandatory = $true)] [ValidateSet("MsSQL","PgSQL","MySQL")] [string]$Provider
    )

   $projFolder = Join-Path $SolutionRoot "xUnitTests"
    if (-not (Test-Path $projFolder)) {
        throw "xUnitTests folder not found at: $projFolder"
    }

    Write-Host "xUnitTests: $projFolder"
    Write-Host "Activating migrations for provider: $projFolder"
    Write-Host ""

    $providers = @("MsSQL","PgSQL","MySQL")
    foreach ($p in $providers) {
        $migDir = Join-Path $projFolder "TestDb\Migrations\$p"
        if (-not (Test-Path $migDir)) {
            Write-Host "Skipping missing folder: $migDir"
            continue
        }

        $enable = ($p -eq $Provider)
        if ($enable) {
            Write-Host "Preparing CLEAN migration folder for provider '$p' at $migDir"

            # Remove ALL .cs and .xcs files under this provider (including Mg* subdirs)
            Get-ChildItem -Path $migDir -Recurse -File |
                Where-Object { $_.Extension -in '.cs', '.xcs' } |
                Remove-Item -Force
        }
        else {
            Write-Host "Disabling migrations under $migDir (provider '$p')"

            # Rename all .cs files under this provider (including subdirs) to .cs.xcs
            Get-ChildItem -Path $migDir -Recurse -File |
                Where-Object { $_.Extension -eq '.cs' } |
                ForEach-Object {
                    $newName = $_.FullName + ".xcs"
                    Rename-Item $_.FullName $newName -Force
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

- prepares PostgreSQL as the active migration target
- ensures other provider snapshots do not interfere
- runs EF migrations
- collects resulting SQL
- merges them with manual SQL scripts where required


```powershell
<#

param(
    [string]$SolutionRoot,
    [string]$SqlOutFileName = "Migrations\DbDeploy_PgSQL.PgSQL"
)

# If no SolutionRoot passed, infer from script location
if (-not $SolutionRoot) {
    $DbDir   = Split-Path $PSScriptRoot -Parent    
    $ProjectDir   = Split-Path $DbDir -Parent    
    $SolutionRoot = Split-Path $ProjectDir -Parent           
}
$ErrorActionPreference = "Stop"

Write-Host "=== EF PgSQL: Creating initial migrations for Postgres ==="
Write-Host "Solution root: $SolutionRoot"

# Build relative paths dynamically
$Startup = Join-Path $SolutionRoot "xUnitTests\xUnitTests.csproj"
$Project = $Startup

Write-Host "Project path:  $Project"
Write-Host "Startup path:  $Startup"
Write-Host ""

# Import helpers:  resolve path relative to this script
$helperPath = Join-Path $PSScriptRoot "helpers.ps1"
. $helperPath   # ← dot + space + path = "dot-source"

# Make sure we have Postgres as default database in config
Set-DefaultAppConnName -SolutionRoot $SolutionRoot -Name "TestPg"
Set-MigrationProvider   -SolutionRoot $SolutionRoot -Provider "PgSQL"

$migrations = @(
    @{Name="InitDbTest"; Context="DbTest"; OutDir="TestDb/Migrations/PgSQL/MgDbTest"; OutFile="TestDb/Migrations/PgSQL/InitDbTest.PgSQL"  }
)
$filesInOrder = @(
    "TestDb/Migrations/PgSQL/InitDbTest.PgSQL",
    "TestDb/PgSQL.PgSQL"
)
$mergedSQLFile = "";

foreach ($m in $migrations) {
    Write-Host "-> Creating migration $($m.Name) ($($m.Context))"
    dotnet ef migrations add $m.Name `
        --context $m.Context `
        --project $Project `
        --output-dir $m.OutDir  #`
        #--startup-project $Startup
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet ef failed for migration $($m.Name) ($($m.Context)) with exit code $LASTEXITCODE"
        break
    }
    Write-Host "-> Creating migration SQL $($m.Name) ($($m.Context))"
    $outFil = Join-Path $ProjectDir $($m.OutFile)
    Write-Host "Command: dotnet ef migrations script 0 $($m.Name) --context $($m.Context) --project $($Project) --startup-project $($Startup) --output $($m.OutFile)"
    dotnet ef migrations script 0 $m.Name `
        --context $m.Context `
        --project $Project `
        --output $outFil #`
         #--startup-project $Startup `
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet ef failed for migration $($m.Name) ($($m.Context)) with exit code $LASTEXITCODE"
        break
    }
}
# Merge the migration with our custom SQL into one deployment script
# Resolve to full paths relative to DbAppBase
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
# Create or overwrite output file
$deployPath = Join-Path $DbDir $SqlOutFileName
"/* 
    Database deploy script (PgSQL)
    Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
*/" | Set-Content -Path $deployPath -Encoding UTF8

# Merge with section headers
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
        "/*** END $name ***/"
        ""
    ) | Add-Content -Path $deployPath
}

Write-Host ""
Write-Host "✅ Done. Deploy script created at:"
Write-Host "   $deployPath"
```

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
