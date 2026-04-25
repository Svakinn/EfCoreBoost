function Convert-ToMigrationCsFileName {
    param([Parameter(Mandatory = $true)][string]$FileName)

    if ($FileName.EndsWith(".cs.xcs")) {
        return $FileName.Substring(0, $FileName.Length - 4)
    }

    return [System.IO.Path]::ChangeExtension($FileName, ".cs")
}

function Convert-ToMigrationXcsFileName {
    param([Parameter(Mandatory = $true)][string]$FileName)

    if ($FileName.EndsWith(".cs")) {
        return $FileName.Substring(0, $FileName.Length - 3) + ".xcs"
    }

    return [System.IO.Path]::ChangeExtension($FileName, ".xcs")
}

function Disable-MigrationProviderFiles {
    param(
        [Parameter(Mandatory = $true)][string]$MigrationDir
    )

    Get-ChildItem -Path $MigrationDir -Recurse -File |
        Where-Object { $_.Extension -eq ".cs" } |
        ForEach-Object {
            $newName = Convert-ToMigrationXcsFileName $_.FullName
            Rename-Item $_.FullName $newName -Force
        }
}

function Enable-MigrationProviderFiles {
    param(
        [Parameter(Mandatory = $true)][string]$MigrationDir
    )

    Get-ChildItem -Path $MigrationDir -Recurse -File |
        Where-Object { $_.Extension -eq ".xcs" } |
        ForEach-Object {
            $newName = Convert-ToMigrationCsFileName $_.FullName
            Rename-Item $_.FullName $newName -Force
        }
}

function Set-MigrationProvider {
    <#
    .SYNOPSIS
      Enables migrations for a single provider and disables the others by renaming .cs <-> .xcs.

    .DESCRIPTION
      Used when creating a fresh initial migration for one provider.
      The selected provider folder is cleaned so a fresh migration can be created there.
      Other provider folders are disabled by renaming active .cs files to .xcs.

    .PARAMETER ProjectDir
      Full path to the migration project folder.

    .PARAMETER Provider
      One of: MsSQL, PgSQL, MySQL
    #>
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
                Where-Object { $_.Extension -in ".cs", ".xcs" } |
                Remove-Item -Force
        }
        else {
            Write-Host "Disabling migrations under $migDir (provider '$p')"
            Disable-MigrationProviderFiles $migDir
        }
    }

    Write-Host ""
    Write-Host "Done. Active migration provider: $Provider"
}

function Set-MigrationProviderForAdd {
    <#
    .SYNOPSIS
      Enables existing migrations for one provider and disables the others.

    .DESCRIPTION
      Used when adding a new migration on top of existing provider migrations.
      The selected provider folder is NOT cleaned.
      Existing .xcs files for the selected provider are renamed back to .cs.
      Other provider folders are disabled by renaming active .cs files to .xcs.

    .PARAMETER ProjectDir
      Full path to the migration project folder.

    .PARAMETER Provider
      One of: MsSQL, PgSQL, MySQL
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ProjectDir,
        [Parameter(Mandatory = $true)][ValidateSet("MsSQL","PgSQL","MySQL")][string]$Provider
    )

    if (-not (Test-Path $ProjectDir)) {
        throw "Migration project folder not found at: $ProjectDir"
    }

    $projFolder = $ProjectDir

    Write-Host "Migration project: $projFolder"
    Write-Host "Activating existing migrations for provider: $Provider"
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
            Write-Host "Enabling existing migrations under $migDir (provider '$p')"
            Enable-MigrationProviderFiles $migDir
        }
        else {
            Write-Host "Disabling migrations under $migDir (provider '$p')"
            Disable-MigrationProviderFiles $migDir
        }
    }

    Write-Host ""
    Write-Host "Done. Active migration provider: $Provider"
}

function Get-MigrationProviderInfo {
    <#
    .SYNOPSIS
      Counts provider migrations, returns next migration number, and latest EF migration name.

    .DESCRIPTION
      Looks in Migrations\<Provider> for migration files.
      Designer files and model snapshots are ignored.
      Initial migration is recognized by names starting with Init.
      Numbered migrations are recognized by names ending with -number,
      for example SomeMigration-1, AnotherMigration-2.

      Also returns the latest full EF migration name, including timestamp prefix,
      for example: 20260425113000_AddBoostCTX-2.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ProjectDir,
        [Parameter(Mandatory = $true)][ValidateSet("MsSQL","PgSQL","MySQL")][string]$Provider
    )

    if (-not (Test-Path $ProjectDir)) {
        throw "Migration project folder not found at: $ProjectDir"
    }

    $migDir = Join-Path $ProjectDir "Migrations\$Provider"

    if (-not (Test-Path $migDir)) {
        throw "Migration provider folder not found at: $migDir"
    }

    $migrationFiles =
        Get-ChildItem -Path $migDir -Recurse -File |
        Where-Object {
            $_.Extension -in ".cs", ".xcs" `
            -and $_.Name -notlike "*.Designer.*" `
            -and $_.Name -notlike "*ModelSnapshot.*" `
            -and $_.BaseName -match "^\d{14}_.+"
        } |
        Sort-Object BaseName

    $migrationNames = @()
    $numbers = @()
    $initialExists = $false

    foreach ($file in $migrationFiles) {
        $fullMigrationName = $file.BaseName

        if ($fullMigrationName.EndsWith(".cs")) {
            $fullMigrationName = $fullMigrationName.Substring(0, $fullMigrationName.Length - 3)
        }

        $shortName = $fullMigrationName

        if ($shortName -match "^\d{14}_(.+)$") {
            $shortName = $Matches[1]
        }

        $migrationNames += $shortName

        if ($shortName -like "Init*") {
            $initialExists = $true
        }

        if ($shortName -match "-\s*(\d+)$") {
            $numbers += [int]$Matches[1]
        }
    }

    $lastNumber = 0

    if ($numbers.Count -gt 0) {
        $lastNumber = ($numbers | Measure-Object -Maximum).Maximum
    }

    $nextNumber = $lastNumber + 1
    $lastMigrationName = $null

    if ($migrationFiles.Count -gt 0) {
        $lastMigrationName = $migrationFiles[-1].BaseName
    }

    [PSCustomObject]@{
        Provider = $Provider
        MigrationDir = $migDir
        MigrationCount = $migrationNames.Count
        InitialExists = $initialExists
        LastNumber = $lastNumber
        NextNumber = $nextNumber
        LastMigrationName = $lastMigrationName
        Migrations = $migrationNames
    }
}
