function Set-MigrationProvider {
    <#
    .SYNOPSIS
      Enables migrations for a single provider and disables the others by renaming .cs <-> .xcs.

    .DESCRIPTION
      We build migrations separately for each provider because each provider may require its own
      initial migration implementation and its own active snapshot chain.
      The selected provider folder is cleaned so a fresh migration can be created there.
      Other provider folders are disabled by renaming active .cs files to .xcs.

    .PARAMETER ProjectDir
      Full path to the migration project folder, e.g.:
      C:\tfssvaki\WebLit\DBRepoWork\TestDb.Migrate

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
