# Rebuild metadata/missingDatesv3.json used by navegacion libre.
# Scans hemerotecav2 day folders and uploads the JSON to the metadata container.
#
# Usage:
#   .\scripts\update-navegacion.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "sandbox\sandbox.csproj"

Push-Location $repoRoot
try {
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project -c Release --no-build -- nav-only
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
