# Tworzy nowy mod w src/<Name> i dodaje go do solucji.
# Użycie: .\tools\new-mod.ps1 -Name MoreIron [-Author mateu]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')] [string]$Name,
    [string]$Author = "mateu"
)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$dir = Join-Path $root "src\$Name"
if (Test-Path $dir) { throw "Mod '$Name' już istnieje: $dir" }
New-Item -ItemType Directory -Force (Join-Path $dir "Patches") | Out-Null

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>$Name</AssemblyName>
    <RootNamespace>$Name</RootNamespace>
    <Version>0.1.0</Version>
  </PropertyGroup>
</Project>
"@ | Set-Content (Join-Path $dir "$Name.csproj") -Encoding utf8

@"
using $Name;
using MelonLoader;

[assembly: MelonInfo(typeof(${Name}Mod), "$Name", "0.1.0", "$Author")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace $Name
{
    public class ${Name}Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Załadowano.");
        }
    }
}
"@ | Set-Content (Join-Path $dir "${Name}Mod.cs") -Encoding utf8

# mod.json bez BOM - parsery JSON (np. w mod managerach) potrafią się na nim wywalić.
$json = @"
{
  "id": "stellardrive-$($Name.ToLower())",
  "name": "$Name",
  "author": "$Author",
  "version": "0.1.0",
  "description": ""
}
"@
[IO.File]::WriteAllText((Join-Path $dir "mod.json"), $json, (New-Object Text.UTF8Encoding $false))

dotnet sln (Join-Path $root "StellarDriveMods.sln") add (Join-Path $dir "$Name.csproj") --solution-folder src | Out-Null
Write-Host "Utworzono $dir"
