function Set-DefaultAppConnName {
    param(
        [string]$SolutionRoot,
        [string]$Name
    )
    #it can be a littleconfusing where dotnet migration tool chooses to load appettings.json
    $settingsPath = Join-Path $SolutionRoot "BoostTest\appsettings.json"
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

    .PARAMETER SolutionRoot
      Root of the solution (the folder containing BoostTests, etc.)

    .PARAMETER Provider
      One of: MsSQL, PgSQL, MySQL
    #>
    param(
        [Parameter(Mandatory = $true)] [string]$SolutionRoot,
        [Parameter(Mandatory = $true)] [ValidateSet("MsSQL","PgSQL","MySQL")] [string]$Provider
    )

   $projFolder = Join-Path $SolutionRoot "BoostTest"
    if (-not (Test-Path $projFolder)) {
        throw "BoostTest folder not found at: $projFolder"
    }

    Write-Host "BoostTest: $projFolder"
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
