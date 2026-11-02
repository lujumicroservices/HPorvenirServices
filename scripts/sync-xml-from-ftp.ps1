# Sync METS XML from FTP into Azure Blob (Option B). Index afterward with blob or all mode.
#
# Usage:
#   .\scripts\sync-xml-from-ftp.ps1 1961 2010 6 2
#     ^start  ^end   ^threads (4-8)  ^years in parallel (max 2)

param(
    [Parameter(Mandatory = $true)][int]$StartYear,
    [Parameter(Mandatory = $true)][int]$EndYear,
    [ValidateRange(4, 8)][int]$Parallelism = 6,
    [ValidateRange(1, 2)][int]$MaxConcurrentYears = 2
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "Invoke-ElasticIndexer.ps1") -AllowConcurrent -SkipBuild ftp-sync $StartYear $EndYear hporvenir $Parallelism $MaxConcurrentYears
