# Test w osobnej instancji gry (tylko dla twórcy moda): kopia świata, parametry startowe zamiast zmian w ustawieniach,
# gra zamyka się sama po teście. Nie dotyka gry uruchomionej przez gracza.
# Użycie: .\tools\dev-test.ps1 -Mode test|perf|jetpack|pump|tank|doors [-World test_wszystko] [-TimeoutSec 300]
param(
    [ValidateSet("test", "perf", "jetpack", "pump", "tank", "doors")] [string]$Mode = "test",
    [string]$World = "test_wszystko",
    [int]$TimeoutSec = 300,
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo"
)
$ErrorActionPreference = "Stop"
$saves = Join-Path $env:USERPROFILE "AppData\LocalLow\CuriousOwlGames\StellarDrive\WorldSaves"
$tmp = Join-Path $saves "zz_mining_tmp"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force -Confirm:$false }
Copy-Item (Join-Path $saves $World) $tmp -Recurse
$worldFile = (Join-Path $tmp "world.json").Replace("\", "/")
$gameArgs = @("--mining-dev-world=$worldFile", "--mining-dev-dump", "--mining-dev-$Mode", "--mining-dev-quit")
$p = Start-Process (Join-Path $GameDir "StellarDrive.exe") -WorkingDirectory $GameDir -ArgumentList $gameArgs -PassThru
if (-not $p.WaitForExit($TimeoutSec * 1000)) { Stop-Process -Id $p.Id -Force }   # tylko nasza instancja
Start-Sleep -Seconds 2
Remove-Item $tmp -Recurse -Force -Confirm:$false
Write-Host "Test zakończony (PID $($p.Id))"
