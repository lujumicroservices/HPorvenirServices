# Builds (if needed) and runs HPorvenir.Elastic without exe lock conflicts.
# Stops any stale HPorvenir.Elastic process before build/run.

param(
    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$IndexerArgs,

    [switch]$SkipBuild,
    [switch]$AllowConcurrent
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "HPorvenir.Elastic\HPorvenir.Elastic.csproj"

if (-not $AllowConcurrent) {
    $running = Get-Process -Name "HPorvenir.Elastic" -ErrorAction SilentlyContinue
    if ($running) {
        foreach ($p in $running) {
            Write-Host "Stopping stale HPorvenir.Elastic (PID $($p.Id))..."
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 2
    }
} else {
    Write-Host "AllowConcurrent: leaving existing HPorvenir.Elastic process(es) running."
}

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        dotnet build $project -c Release
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    dotnet run --project $project -c Release --no-build -- @IndexerArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
