# Uruchamia grę (z MelonLoaderem) i opcjonalnie śledzi log MelonLoadera na żywo.
# Użycie: .\tools\run-game.ps1 [-Follow]
param(
    [switch]$Follow,
    [string]$GameDir = $(if ($env:STELLARDRIVE_DIR) { $env:STELLARDRIVE_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo" })
)
$ErrorActionPreference = "Stop"

Get-Process StellarDrive -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process (Join-Path $GameDir "StellarDrive.exe") -WorkingDirectory $GameDir

if ($Follow) {
    $log = Join-Path $GameDir "MelonLoader\Latest.log"
    Start-Sleep -Seconds 3
    Get-Content $log -Wait -Tail 50
}
