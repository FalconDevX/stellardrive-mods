$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$mods = @("ResourceTweaks", "Crouch", "Flora", "PickBlock", "SharedPiloting", "ShipSafety", "VelocityIndicator", "WarpUnlock", "PlanetUnlock", "Mining", "DropItems", "FpsCounter", "Water")

$dist = Join-Path $root "dist\Mods"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force $dist | Out-Null

foreach ($mod in $mods) {
    $proj = Join-Path $root "src\$mod\$mod.csproj"
    dotnet build $proj -c Release -nologo -v q -p:DeployToGame=false
    if ($LASTEXITCODE -ne 0) { throw "Build of $mod failed" }
    Copy-Item (Join-Path $root "src\$mod\bin\Release\$mod.dll") $dist
}

$versions = foreach ($mod in $mods) {
    $json = Get-Content (Join-Path $root "src\$mod\mod.json") -Raw | ConvertFrom-Json
    "$($json.name) $($json.version)"
}
$versions | Set-Content (Join-Path $root "dist\VERSIONS.txt") -Encoding utf8

Write-Host "Done: $dist"
$versions | ForEach-Object { Write-Host "  $_" }
