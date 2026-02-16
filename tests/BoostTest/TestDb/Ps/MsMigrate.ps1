<#
.SYNOPSIS
Creates EF Core migrations for the MsSQL DbContext.
Just run by powershell in its current direcory
.DESCRIPTION
The end product/purpose of the migratons is to create SQL-scripts for database initializations for all 3 database flavors, MsSQL, MySql and Postgres.
For our Testdb to work identically for all 3 databases we need speciffic migrations, SQL-code and Database objects for each flavor.
Thus different Init and Snapshot code for each flavor.
This needs to be handled by castrating *.cs code for other flavors when building and creatinf for this one.
It is implemented by removing current flavor migration before rebuilding and by changing the file ending for the other flavors so they are not compiled.
The folders are created for each migration flavor under the /Migrations folder (MsSQL, MySql and PgSql).
#>

param(
    [string]$SolutionRoot,
    [string]$SqlOutFileName = "Migrations\DbDeploy_MsSQL.sql"
)

# If no SolutionRoot passed, infer from script location
if (-not $SolutionRoot) {
    $DbDir   = Split-Path $PSScriptRoot -Parent    
    $ProjectDir   = Split-Path $DbDir -Parent    
    $SolutionRoot = Split-Path $ProjectDir -Parent           
}
$ErrorActionPreference = "Stop"

Write-Host "=== EF MsSQL: Creating initial migrations for Microsoft SQL-Server ==="
Write-Host "Solution root: $SolutionRoot"

# Build relative paths dynamically
$Startup = Join-Path $SolutionRoot "BoostTest\BoostTest.csproj"
$Project = $Startup

Write-Host "Project path:  $Project"
Write-Host "Startup path:  $Startup"
Write-Host ""

# Import helpers:  resolve path relative to this script
$helperPath = Join-Path $PSScriptRoot "helpers.ps1"
. $helperPath   # ← dot + space + path = "dot-source"

# Make sure we have MsSQL as default database in config
Set-DefaultAppConnName -SolutionRoot $SolutionRoot -Name "TestMs"
Set-MigrationProvider   -SolutionRoot $SolutionRoot -Provider "MsSQL"

$migrations = @(
    @{Name="InitDbTest"; Context="DbTest"; OutDir="TestDb/Migrations/MsSQL/MgDbTest"; OutFile="TestDb/Migrations/MsSQL/InitDbTest.sql"  }
)
$filesInOrder = @(
    "TestDb/Migrations/MsSQL/InitDbTest.sql",
    "TestDb/MsSQL.sql"
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
# Merge the migration with our custom SQL into one deoployment script
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
    Database deploy script (MsSQL)
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
        "GO"
        "/*** END $name ***/"
        ""
    ) | Add-Content -Path $deployPath
}

Write-Host ""
Write-Host "✅ Done. Deploy script created at:"
Write-Host "   $deployPath"