@echo off
chcp 65001 > nul
cd /d "%~dp0"
if exist "BoxesPy_Studio.exe" (
    start "" "BoxesPy_Studio.exe"
) else (
    start "" "python\python.exe" "boxes\scripts\boxesserver.py" --host 127.0.0.1 --port 18950 --static_path "static" --no_reload
    timeout /t 2 > nul
    start "" "http://127.0.0.1:18950/Menu"
)
