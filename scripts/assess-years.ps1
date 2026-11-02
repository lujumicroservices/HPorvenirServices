# Size assessment per year (FTP XML + Blob PDF). Writes indexer-state/year-size-assessment.json
# Usage:
#   .\scripts\assess-years.ps1
#   .\scripts\assess-years.ps1 1919 2026 2

param(
    [int]$StartYear = 1919,
    [int]$EndYear = 2026,
    [int]$ParallelScans = 2
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "Invoke-ElasticIndexer.ps1") assess $StartYear $EndYear hporvenir $ParallelScans
