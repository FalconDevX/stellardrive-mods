# Instaluje (lub aktualizuje) MelonLoader w folderze gry.
# Użycie: .\tools\install-melonloader.ps1 [-Version v0.7.3] [-GameDir "..."]
param(
    [string]$Version = "v0.7.3",
    [string]$GameDir = $(if ($env:STELLARDRIVE_DIR) { $env:STELLARDRIVE_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo" })
)
$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $GameDir "StellarDrive.exe"))) {
    throw "Nie znaleziono StellarDrive.exe w '$GameDir'"
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("MelonLoader_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory $tmp | Out-Null
try {
    $zip = Join-Path $tmp "MelonLoader.x64.zip"
    $url = "https://github.com/LavaGang/MelonLoader/releases/download/$Version/MelonLoader.x64.zip"
    Write-Host "Pobieranie $url"
    Invoke-WebRequest $url -OutFile $zip
    Expand-Archive $zip (Join-Path $tmp "x")
    Copy-Item (Join-Path $tmp "x\*") $GameDir -Recurse -Force
    foreach ($d in "Mods", "UserLibs", "Plugins") { New-Item -ItemType Directory -Force (Join-Path $GameDir $d) | Out-Null }
    Write-Host "MelonLoader $Version zainstalowany w $GameDir"
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
