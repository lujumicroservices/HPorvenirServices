# Unified indexer (Option B): FTP→Blob XML sync for 1919-2010, then index all years from blob.
# Multiple years indexed in parallel after sync completes.
#
# Usage:
#   .\scripts\index-all.ps1 1919 2026 20 8
#     ^parallelism per year  ^max concurrent years

param(
    [int]$StartYear = 1919,
    [int]$EndYear = 2026,
    [int]$Parallelism = 20,
    [int]$MaxConcurrentYears = 0
)

$ErrorActionPreference = "Stop"

if ($MaxConcurrentYears -le 0) {
    $cores = (Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
    $MaxConcurrentYears = [Math]::Max(2, [Math]::Floor($cores / 2))
    Write-Host "Auto MaxConcurrentYears = $MaxConcurrentYears ($cores logical processors)"
}

& (Join-Path $PSScriptRoot "Invoke-ElasticIndexer.ps1") all $StartYear $EndYear hporvenir $Parallelism $MaxConcurrentYears
