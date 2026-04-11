<#
.SYNOPSIS
EFCore.Boost helper that creates EF Core migrations for the MsSQL DbContext.
Go to the PS folder in terminal and run the script (i.e. .\MsMigrate.ps1)
.DESCRIPTION
The end product/purpose of the migratons is to create SQL-scripts for database initializations for all 3 database flavors, MsSQL, MySql and Postgres.
For our BoostX-db to work identically for all 3 databases we need speciffic migrations, SQL-code and Database objects for each flavor.
Thus different Init and Snapshot code for each flavor.
This needs to be handled by castrating *.cs code for other flavors when building and creatinf for this one.
It is implemented by removing current flavor migration before rebuilding and by changing the file ending for the other flavors so they are not compiled.
The folders are created for each migration flavor under the /Migrations folder (MsSQL, MySql and PgSql).
#>

param(
    [string]$ProjectDir,
    [string]$ConnName = "BoostXMs",
    [string]$SqlOutFileName = "Migrations\DbDeploy_MsSQL.sql"
)

# If no ProjectDir passed, infer from script location:
# script lives in /PS under the project folder
if (-not $ProjectDir) {
    $ProjectDir = Split-Path $PSScriptRoot -Parent
}

$ErrorActionPreference = "Stop"

Write-Host "=== EF MsSQL: Creating initial migrations for Microsoft SQL-Server ==="

# Build paths relative to project dir
$Project = Join-Path $ProjectDir "BoostX.Migrate.csproj"

Write-Host "PSScriptRoot: $PSScriptRoot"
Write-Host "ProjectDir:   $ProjectDir"
Write-Host "Project name: $Project"
Write-Host "ConnName:     $ConnName"
Write-Host ""

# Import helpers relative to this script
$helperPath = Join-Path $PSScriptRoot "helpers.ps1"
. $helperPath

# Make sure we have MsSQL as current provider
Set-MigrationProvider -ProjectDir $ProjectDir -Provider "MsSQL"

$migrations = @(
    @{ Name = "InitBoostCTX"; Context = "BoostCTX"; OutDir = "Migrations/MsSQL/SnapBoostCTX"; OutFile = "Migrations/MsSQL/InitBoostCTX.sql" }
)

$filesInOrder = @(
    "Migrations/MsSQL/InitBoostCTX.sql",
    "SQL/MsSQL.sql"
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
    Database deploy script (MsSQL)
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
Write-Host "Done. Deploy script created at:"
Write-Host "  $deployPath"