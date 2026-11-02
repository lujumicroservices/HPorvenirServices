# Index METS XML directly from FTP (legacy; Option A). Prefer sync + blob index (Option B):
#   .\scripts\sync-xml-from-ftp.ps1 1919 2010 4
#   .\scripts\index-all.ps1 1919 2026 20 8
#
# Usage:
#   .\scripts\index-from-ftp.ps1 1919 1919 12

param(
    [Parameter(Mandatory = $true)][int]$StartYear,
    [Parameter(Mandatory = $true)][int]$EndYear,
    [int]$Parallelism = 12
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "Invoke-ElasticIndexer.ps1") ftp $StartYear $EndYear hporvenir $Parallelism
