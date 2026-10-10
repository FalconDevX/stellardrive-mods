# StellarDrive Mods

Własne mody do **StellarDrive** (Curious Owl, Unity 6000.3 / Mono) na **MelonLoader 0.7.3** + Harmony.
Format paczek (`mod.json` + `.dll` w GitHub Release) jest zgodny z rejestrem
[StellarModManager](https://github.com/jollyname/StellarModManager), więc gotowe mody da się tam później zgłosić.

## Wymagania
- .NET SDK 8+ (`dotnet --list-sdks`)
- Gra: domyślnie `C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo`
  (inna ścieżka → skopiuj `GamePath.props.example` jako `GamePath.props` albo ustaw `STELLARDRIVE_DIR`)
- MelonLoader w grze: `.\tools\install-melonloader.ps1`

## Struktura
```
Directory.Build.props    wspólne ustawienia: net472, referencje do MelonLoadera i wszystkich DLL gry
Directory.Build.targets  sprawdzenie ścieżek + auto-kopiowanie .dll do <gra>\Mods po buildzie
src/<Mod>/               jeden folder = jeden mod (csproj, klasa MelonMod, Patches/, mod.json)
tools/                   install/uninstall MelonLoadera, run-game, new-mod
```

## Codzienna praca
```powershell
.\tools\new-mod.ps1 -Name MoreIron      # nowy mod w src/MoreIron
dotnet build                            # build wszystkich modów + kopia do <gra>\Mods
.\tools\run-game.ps1 -Follow            # start gry + podgląd MelonLoader\Latest.log
```
W VS Code: `Ctrl+Shift+B` (build) albo task **build + run game**.

- Log: `<gra>\MelonLoader\Latest.log`
- Konfiguracja modów (MelonPreferences): `<gra>\UserData\MelonPreferences.cfg`
- Wyłączenie moda: usuń jego `.dll` z `<gra>\Mods`. Czysta gra: `.\tools\uninstall-melonloader.ps1`

## Wskazówki
- Logika gry jest w modułach w `StellarDrive_Data\Managed` (`Ships.dll`, `Items.Model.dll`, `Planets.dll`,
  `Players.dll`, `Research.dll`, `Saving.dll`, `UI.dll`…), nie tylko w `Assembly-CSharp.dll`.
  Do przeglądania: [ILSpy](https://github.com/icsharpcode/ILSpy) / dnSpyEx.
- Gra używa **nowego Input Systemu** (`UnityEngine.InputSystem.Keyboard.current`), `UnityEngine.Input` nie zadziała.
- Prywatne pola w patchach: parametry `___nazwaPola` albo `AccessTools.Field(...)`.
- Gra ma multiplayer (FishNet) - mody zmieniające rozgrywkę zwykle musi mieć host (a często wszyscy gracze).
- Zdekompilowanego kodu gry nie commitujemy (`decompiled/` jest w `.gitignore`).

## Przykład: `src/HelloStellar`
Logowanie, wpis w konfiguracji (`DebugKey`, domyślnie F8 - wypisuje pozycję kamery),
reakcja na wczytanie sceny i patch Harmony na `Items.Model.ItemSettingsList.ResetDict`.
