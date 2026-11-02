# Process staging container hporvenir → hemerotecav2 + Elasticsearch, then refresh navegacion libre.
#
# Usage:
#   .\scripts\process-staging.ps1
#   .\scripts\process-staging.ps1 8
#   .\scripts\process-staging.ps1 8 skip-nav

param(
    [ValidateRange(1, 20)][int]$Parallelism = 8,
    [string]$ExtraArg = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "sandbox\sandbox.csproj"

Push-Location $repoRoot
try {
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($ExtraArg) {
        dotnet run --project $project -c Release --no-build -- $Parallelism $ExtraArg
    } else {
        dotnet run --project $project -c Release --no-build -- $Parallelism
    }
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
