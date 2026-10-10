# Instalacja / aktualizacja modów u gracza: instaluje MelonLoader (jeśli go nie ma)
# i kopiuje gotowe mody z dist\Mods do folderu gry. Uruchamiane przez install.bat.
param(
    [string]$GameDir = $(if ($env:STELLARDRIVE_DIR) { $env:STELLARDRIVE_DIR } else { "" })
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

function Find-GameDir {
    $candidates = @()
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $candidates += Join-Path $steam "steamapps\common\StellarDrive Demo"
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $candidates += Join-Path ($m.Groups[1].Value -replace '\\\\', '\') "steamapps\common\StellarDrive Demo"
            }
        }
    }
    $candidates += "C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo"
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "StellarDrive.exe")) { return $c }
    }
    return $null
}

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "StellarDrive.exe"))) {
    throw "Nie znaleziono gry StellarDrive Demo. Uruchom: install.bat `"D:\sciezka\do\StellarDrive Demo`""
}
Write-Host "Gra: $GameDir"

if (Get-Process StellarDrive -ErrorAction SilentlyContinue) {
    throw "Gra jest uruchomiona - zamknij ją i spróbuj ponownie."
}

if (-not (Test-Path (Join-Path $GameDir "MelonLoader\net35\MelonLoader.dll"))) {
    Write-Host "Brak MelonLoadera - instaluję..."
    & (Join-Path $PSScriptRoot "install-melonloader.ps1") -GameDir $GameDir
}

$modsDir = Join-Path $GameDir "Mods"
New-Item -ItemType Directory -Force $modsDir | Out-Null
$dlls = Get-ChildItem (Join-Path $root "dist\Mods") -Filter *.dll
foreach ($dll in $dlls) {
    Copy-Item $dll.FullName $modsDir -Force
    Write-Host "  zainstalowano $($dll.Name)"
}

Write-Host ""
Write-Host "Gotowe! Uruchom grę normalnie przez Steam."
if (Test-Path (Join-Path $root "dist\VERSIONS.txt")) { Get-Content (Join-Path $root "dist\VERSIONS.txt") }
