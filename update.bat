@echo off
cd /d "%~dp0"
git pull
if errorlevel 1 (
    echo Nie udalo sie pobrac zmian ^(git pull^).
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1" %*
pause
