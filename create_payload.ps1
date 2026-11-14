$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$payloadDir = Join-Path $root "payload"
$zipPath = Join-Path $root "payload.zip"

Get-ChildItem -Path $payloadDir -Include "__pycache__" -Recurse -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $payloadDir,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

Write-Host "payload.zip olusturuldu: $zipPath" -ForegroundColor Green
