# Usuwa MelonLoader z gry (przywraca czystą grę). Folder Mods zostaje, chyba że podasz -RemoveMods.
param(
    [switch]$RemoveMods,
    [string]$GameDir = $(if ($env:STELLARDRIVE_DIR) { $env:STELLARDRIVE_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo" })
)
$ErrorActionPreference = "Stop"

$targets = @("version.dll", "MelonLoader", "UserLibs", "Plugins")
if ($RemoveMods) { $targets += "Mods", "UserData" }
foreach ($t in $targets) {
    $p = Join-Path $GameDir $t
    if (Test-Path $p) { Remove-Item $p -Recurse -Force -Confirm:$false; Write-Host "Usunięto $p" }
}
