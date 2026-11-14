$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$zipPath = Join-Path $root "payload.zip"

if (-not (Test-Path $zipPath)) {
    & "$root\create_payload.ps1"
}

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:"$root\app.ico" `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    /resource:"$zipPath,payload.zip" `
    /out:"$root\Setup_BoxesPy_Studio.exe" `
    "$root\SetupProgram.cs"

Write-Host "Setup_BoxesPy_Studio.exe derlendi." -ForegroundColor Green
