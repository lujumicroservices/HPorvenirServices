# Test FTP connection using HPorvenir.Elastic/ftp.settings.json
# Usage: .\scripts\test-ftp.ps1 [remotePath]

param(
    [string]$RemotePath = "/",
    [string]$SettingsPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "HPorvenir.Elastic\ftp.settings.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SettingsPath)) {
    throw "Missing $SettingsPath. Copy from ftp.settings.template.json and fill in credentials."
}

$ftp = (Get-Content $SettingsPath | ConvertFrom-Json).Ftp
$uri = "ftp://$($ftp.Host):$($ftp.Port)$RemotePath"

Write-Host "Connecting to $uri as $($ftp.Username) (passive=$($ftp.UsePassive))..."

if ($ftp.UsePassive) {
    $request = [System.Net.FtpWebRequest]::Create($uri)
    $request.Credentials = New-Object System.Net.NetworkCredential($ftp.Username, $ftp.Password)
    $request.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectory
    $request.UsePassive = $true
    $request.UseBinary = $true
    $request.KeepAlive = $false
    $request.Timeout = 30000

    $response = $request.GetResponse()
    $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
    $listing = $reader.ReadToEnd()
    $reader.Close()
    $response.Close()

    Write-Host "Connected. Listing:"
    $listing -split "`r?`n" | Where-Object { $_.Trim() -ne "" } | ForEach-Object { Write-Host "  $_" }
}
else {
    curl.exe -s --ftp-pasv --user "$($ftp.Username):$($ftp.Password)" "$uri"
}

Write-Host "Done."
