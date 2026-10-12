# StellarDrive Mods

Nieoficjalne mody do **StellarDrive Demo** (Curious Owl) na **MelonLoader 0.7.3**.
Nie są powiązane z twórcą gry.

| Mod | Co robi |
|---|---|
| **Resource Tweaks** | 3x więcej surowców z wydobycia (bez narzędzi), stacki do 100, szybsze kopanie (drzewa -50% czasu, reszta -25%) |
| **Crouch** | Kucanie na lewy Ctrl (niższa postać i kamera, wolniejszy chód) |
| **Flora** | Pola kwiatów na planecie (tylko dekoracja) |
| **Pick Block** | Z narzędziem budowania: środkowy przycisk myszy na części = buduj tą samą częścią; przytrzymany lewy przycisk stawia część w każdym kolejnym wolnym miejscu (jak w Minecrafcie) |
| **Shared Piloting** | Kilka foteli pilota: obraca statkiem ten, kto rusza myszą (wbudowany żyroskop bezczynnego fotela nie blokuje). Wystarczy u hosta |
| **Ship Safety** | Naprawia błąd gry, który wyrzuca statek z prędkością wielu c miliony km od planety (paliwo w zbiornikach liczone na minusie → ujemna masa statku). Dodatkowo pilnuje masy, skoków prędkości i limitu 5000 m/s. Wystarczy u hosta |
| **Velocity Indicator** | Wskaźnik prędkości na statku nie przeskakuje nagle w bok przy wejściu na orbitę (pokazuje prędkość względem powierzchni planety) i nie pokazuje fałszywego zera. Każdy gracz potrzebuje moda |
| **Warp Unlock** | Pokazuje w drzewie badań dema ukryte badanie Warp Drive (i badania wymagane przed nim), żeby dało się latać do innych planet. Chowa "Demo Complete" (czujniki są w odblokowanym badaniu "Sensors"). Obaj gracze potrzebują moda |
| **Mining** | Złoża na planetach (pola kamieni z rudą, widać jaka to ruda) i koparka do zbudowania po badaniu "Mining" (za Chemical Propulsion). Stawiasz ją prosto na ziemi (dopasowuje się do zbocza) albo na podłodze statku, na złożu. Podłączasz prąd (gniazda z szarym daszkiem przy nogach) i sygnał 1, np. z dźwigni (gniazdo z żółtym daszkiem) - wtedy wysuwa wiertło i wydobywa rudę (do 4 stosów po 100, otwierasz jak skrzynię). Klawisz N pokazuje kierunek do najbliższego złoża, U - do uranu, K - do szkła. Obaj gracze potrzebują moda. Badanie "Hydrogen Propulsion" dodaje silnik wodorowy (wodór + prąd, np. z reaktora, albo wodór + tlen; wszystkie trzy naraz = dopalanie x1,4; 1,8x mocniejszy od chemicznego) i elektrolizer (lód albo woda + prąd = wodór, tlen i woda) oraz pompę wody 1x1: nad jeziorem na Otrera b F opuszcza wąż (do 20 m) albo sygnał ustawia jego długość (1 = cały, 0 = zwinięty), pompa z prądem nalewa wodę do dwóch wyjść (prawy bok); bez jeziora przetłacza wodę z rury na wejściu (lewy bok). Badanie "Mechanics" odblokowuje Rotor z gry (obraca konstrukcję na swojej płycie do kąta w stopniach z sygnału, np. z Constant). Złoża uranu (zielone, świecące) na Otrera b; badanie "Nuclear Power" daje reaktor (uran + woda + sygnał 1 na gnieździe z tyłu = 20 000 W; przy pracy świeci, kręci rdzeniami i buczy). Badanie "Large Tanks" (za Gas Handling): zbiornik na płyny 4x4x4 (6400 jednostek, 4 gniazda na rury). Badanie "Large Doors": drzwi 3x2, 3x3, 4x4, 6x3 i bramy 10x5, 16x8 - jasnoszare jak płytki kadłuba, ciężkie (wolne otwieranie, własny dźwięk), F z każdego miejsca. F9: raport wydajności (UserData/Mining/fps_report.txt); po śmierci przyczyna w UserData/Mining/death_log.txt. Badanie "Jetpack": ostatni slot plecaka to paliwo (drewno), w powietrzu przytrzymaj skok |
| **FPS Counter** | Licznik FPS w prawym górnym rogu (F3 włącza/wyłącza) |
| **Drop Items** | Wyrzucanie przedmiotów: Q z wybranego slotu paska, X ze slotu pod kursorem w ekwipunku (z Shiftem połowa stosu) |
| **Water** | Prawdziwa woda w akwenach Otrera b (w grze to tylko niebieski teren): stylizowany shader (turkusowa płycizna z widocznym dnem, błękitna głębia, świetlne kaustyki, biała piana przy brzegu). Dno akwenów obniżone o 10 m (opcja `LakeDepth`) - zmienia teren, więc obaj gracze potrzebują moda. Shader: `src/Water/UnityProject` (Unity 6000.3.24f1) |
| **Planet Unlock** | EKSPERYMENTALNE: wczytuje wszystkie planety układu 442 Otrera (demo wczytuje tylko Otrera b), żeby dało się do nich dolecieć warpem. Obaj gracze potrzebują moda |

Ustawienia (mnożnik, stack, gęstość roślin...) są w `StellarDrive Demo\UserData\MelonPreferences.cfg`
(plik pojawia się po pierwszym uruchomieniu gry z modami).

## Instalacja dla graczy

Potrzebujesz [Gita](https://git-scm.com/download/win) i gry **StellarDrive Demo** ze Steama.

**Pierwszy raz:**
1. Otwórz terminal (np. PowerShell) w folderze, gdzie chcesz trzymać mody, i wpisz:
   ```
   git clone https://github.com/FalconDevX/stellardrive-mods.git
   ```
2. **Zamknij grę**, wejdź do folderu `stellardrive-mods` i kliknij dwa razy **`install.bat`**.
   Skrypt sam znajdzie grę, zainstaluje MelonLoader (jeśli go nie ma) i skopiuje mody.
3. Uruchom grę normalnie przez Steam. Otworzy się też czarne okno konsoli MelonLoadera - tak ma być.

**Aktualizacja** (gdy pojawią się zmiany): zamknij grę i kliknij dwa razy **`update.bat`**
(robi `git pull` i instaluje nowe wersje).

Gra jest w innym miejscu? Uruchom w terminalu: `install.bat "D:\SteamLibrary\steamapps\common\StellarDrive Demo"`.

**Multiplayer:** wszyscy gracze powinni mieć te same wersje modów (`dist\VERSIONS.txt`).
Wyłączenie modów: usuń pliki `.dll` z `StellarDrive Demo\Mods`.

---

# Dla twórców modów

Mody to C# (net472) + Harmony. Format `mod.json` jest zgodny z rejestrem
[StellarModManager](https://github.com/jollyname/StellarModManager).
Po zmianach w kodzie: `.\tools\pack.ps1` (buduje Release do `dist\Mods`), potem commit i push.

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
tools/                   install/uninstall MelonLoadera, run-game, new-mod, pack (Release -> dist), install (dla graczy)
dist/Mods/               gotowe .dll dla graczy (aktualizowane przez tools\pack.ps1)
install.bat, update.bat  instalacja / aktualizacja dla graczy
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
