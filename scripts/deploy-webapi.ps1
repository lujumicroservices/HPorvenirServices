# Deploy HPorvenir.Web.Api to Azure App Service (Linux) with Elasticsearch Cloud settings.
# Usage: .\scripts\deploy-webapi.ps1
# Requires: Azure CLI logged in, elastic.cloud.settings.json in HPorvenir.Elastic/

param(
    [string]$AppName = "hporvenirappv2",
    [string]$ResourceGroup = "com.porvenir",
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$cloudSettingsPath = Join-Path $ProjectRoot "HPorvenir.Elastic\elastic.cloud.settings.json"
if (-not (Test-Path $cloudSettingsPath)) {
    throw "Missing $cloudSettingsPath. Copy from elastic.cloud.settings.template.json and fill in credentials."
}

$elastic = (Get-Content $cloudSettingsPath | ConvertFrom-Json).Elastic
$publishDir = Join-Path $ProjectRoot "_deploy\publish"
$zipPath = Join-Path $ProjectRoot "_deploy\webapi-linux.zip"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path (Split-Path $zipPath) -Force | Out-Null

Write-Host "Building HPorvenir.Web.Api..."
Push-Location $ProjectRoot
dotnet publish HPorvenir.Web.Api\HPorvenir.Web.Api.csproj -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
Pop-Location

Copy-Item $cloudSettingsPath (Join-Path $publishDir "elastic.cloud.settings.json") -Force

Write-Host "Creating Linux-compatible zip..."
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem | Out-Null

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $publishDir -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($publishDir.Length + 1).Replace("\", "/")
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName)
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Setting Azure App Service Elastic Cloud configuration..."
$settingArgs = @(
    "Elastic__UseElasticCloud=$($elastic.UseElasticCloud.ToString().ToLower())",
    "Elastic__Url=$($elastic.Url)",
    "Elastic__ApiKey=$($elastic.ApiKey)",
    "Elastic__SearchAlias=$($elastic.SearchAlias)",
    "Elastic__RequestTimeoutSeconds=$($elastic.RequestTimeoutSeconds)"
)
if ($elastic.CloudId) {
    $settingArgs += "Elastic__CloudId=$($elastic.CloudId)"
}

az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroup `
    --settings $settingArgs

if ($LASTEXITCODE -ne 0) { throw "Failed to set app settings" }

az webapp config set `
    --name $AppName `
    --resource-group $ResourceGroup `
    --startup-file "dotnet HPorvenir.Web.Api.dll"

if ($LASTEXITCODE -ne 0) { throw "Failed to set startup command" }

Write-Host "Deploying to $AppName..."
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $AppName `
    --src-path $zipPath `
    --type zip `
    --async false

if ($LASTEXITCODE -ne 0) { throw "Deployment failed" }

Write-Host "Done. Verify: https://$AppName.azurewebsites.net/ping"
