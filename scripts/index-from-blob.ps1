# Index from Azure Blob in parallel. XML is read from hporvenirxml/ (see appsettings XmlBlobPrefix).
# Safe to run while ftp-sync is still running — use -AllowConcurrent so sync is not killed.
#
# Only index years that are fully synced to blob. Re-run later for newly synced years.
#
# Usage:
#   .\scripts\index-from-blob.ps1 1919 1954 12 4
#     ^start  ^end   ^files/year  ^years in parallel

param(
    [Parameter(Mandatory = $true)][int]$StartYear,
    [Parameter(Mandatory = $true)][int]$EndYear,
    [int]$Parallelism = 12,
    [int]$MaxConcurrentYears = 4
)

$ErrorActionPreference = "Stop"
# SkipBuild: sync process locks DLLs; both can share the same Release build.
& (Join-Path $PSScriptRoot "Invoke-ElasticIndexer.ps1") -AllowConcurrent -SkipBuild blob $StartYear $EndYear hporvenir $Parallelism $MaxConcurrentYears
