# Buduje mody w wersji Release i kopiuje gotowe .dll do dist\Mods (ten folder jest w repo,
# żeby gracze nie musieli nic kompilować - wystarczy git pull + install.bat).
# Użycie: .\tools\pack.ps1
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
# Mody dla graczy (HelloStellar to tylko przykład dla programistów).
$mods = @("ResourceTweaks", "Crouch", "Flora", "PickBlock", "SharedPiloting", "ShipSafety", "VelocityIndicator", "WarpUnlock", "PlanetUnlock")

$dist = Join-Path $root "dist\Mods"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($mod in $mods) {
    $proj = Join-Path $root "src\$mod\$mod.csproj"
    dotnet build $proj -c Release -nologo -v q -p:DeployToGame=false
    if ($LASTEXITCODE -ne 0) { throw "Build $mod nie powiódł się" }
    Copy-Item (Join-Path $root "src\$mod\bin\Release\$mod.dll") $dist
}

$versions = foreach ($mod in $mods) {
    $json = Get-Content (Join-Path $root "src\$mod\mod.json") -Raw | ConvertFrom-Json
    "$($json.name) $($json.version)"
}
$versions | Set-Content (Join-Path $root "dist\VERSIONS.txt") -Encoding utf8

Write-Host "Gotowe: $dist"
$versions | ForEach-Object { Write-Host "  $_" }
