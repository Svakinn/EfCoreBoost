<#
.SYNOPSIS
Creates EF Core migrations for the MySQL DbContext.
Just run by powershell in its current direcory
.DESCRIPTION
The end product/purpose of the migratons is to create SQL-scripts for database initializations for all 3 database flavors, MsSQL, MySQL and Postgres.
For our Testdb to work identically for all 3 databases we need speciffic migrations, SQL-code and Database objects for each flavor.
Thus different Init and Snapshot code for each flavor.
This needs to be handled by castrating *.cs code for other flavors when building and creatinf for this one.
It is implemented by removing current flavor migration before rebuilding and by changing the file ending for the other flavors so they are not compiled.
The folders are created for each migration flavor under the /Migrations folder (MsSql, MySQL and PgSql).
#>

param(
    [string]$SolutionRoot,
    [string]$SqlOutFileName = "Migrations\DbDeploy_MySQL.MySQL"
)

# If no SolutionRoot passed, infer from script location
if (-not $SolutionRoot) {
    $DbDir   = Split-Path $PSScriptRoot -Parent    
    $ProjectDir   = Split-Path $DbDir -Parent    
    $SolutionRoot = Split-Path $ProjectDir -Parent           
}
$ErrorActionPreference = "Stop"

Write-Host "=== EF MySQL: Creating initial migrations for MySQL ==="
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

# Make sure we have MySQL as default database in config
Set-DefaultAppConnName -SolutionRoot $SolutionRoot -Name "TestMy"
Set-MigrationProvider   -SolutionRoot $SolutionRoot -Provider "MySQL"

# Comment below out if you are not requiering Docker for MySQL access
# The .net MySQL provider reqires to know actual server version before connecting, thus design time or not, our factory needs to connect to server
# to check its version.  Therefore we have this check for docker, but only for MySQL databases
#if (-not (Test-DockerRunning)) {
#    Write-Error "Docker does not seem to be running. Please start Docker Desktop (or your Docker service) and try again."
#    exit 1
#}
#or:
#Communiacte to the context-factory that we do not require direct cnnection to MySQL (for version info) -> generate script works with or without actual db-connection
$env:EF_DESIGN_DEBUG = "1" 

$migrations = @(
    @{Name="InitDbTest"; Context="DbTest"; OutDir="TestDb/Migrations/MySQL/MgDbTest"; OutFile="TestDb/Migrations/MySQL/InitDbTest.MySQL"  }
)
$filesInOrder = @(
    "TestDb/Migrations/MySQL/InitDbTest.MySQL",
    "TestDb/MySQL.MySQL"
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
#Merge the migration with our custom sql into one deoployment script
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
    Database deploy script (MySQL)
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