$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32icon:"$root\app.ico" `
    /out:"$root\payload\BoxesPy_Studio.exe" `
    "$root\Program.cs"

Copy-Item "$root\payload\BoxesPy_Studio.exe" "$root\BoxesPy_Studio.exe" -Force
Write-Host "BoxesPy_Studio.exe derlendi." -ForegroundColor Green
