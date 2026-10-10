@echo off
rem Instalacja / aktualizacja modow StellarDrive. Kliknij dwa razy (gra musi byc zamknieta).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1" %*
pause
