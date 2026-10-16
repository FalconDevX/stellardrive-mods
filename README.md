<div align="center">

# StellarDrive Mods

**A collection of unofficial gameplay, building and quality-of-life mods for _StellarDrive Demo_, built on MelonLoader and Harmony.**

[![Game](https://img.shields.io/badge/game-StellarDrive%20Demo-1b2838?logo=steam&logoColor=white)](https://store.steampowered.com/search/?term=StellarDrive)
[![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3-6b4fbb)](https://github.com/LavaGang/MelonLoader)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/language-C%23-239120?logo=csharp&logoColor=white)](src)
[![Harmony](https://img.shields.io/badge/patching-Harmony-orange)](https://github.com/pardeike/Harmony)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)](#requirements)
[![Mods](https://img.shields.io/badge/mods-13-brightgreen)](dist/VERSIONS.txt)
[![Last commit](https://img.shields.io/github/last-commit/FalconDevX/stellardrive-mods)](https://github.com/FalconDevX/stellardrive-mods/commits/main)

</div>

> [!NOTE]
> These mods are a fan project and are not affiliated with or endorsed by Curious Owl, the developer of StellarDrive.

---

## Table of contents

- [Mods at a glance](#mods-at-a-glance)
- [Highlights](#highlights)
- [Installation](#installation)
- [Updating and uninstalling](#updating-and-uninstalling)
- [Multiplayer](#multiplayer)
- [Configuration](#configuration)
- [Mod details](#mod-details)
- [For mod developers](#for-mod-developers)
- [License](#license)

---

## Mods at a glance

| Mod | Version | Summary | Who needs it |
|---|:---:|---|---|
| **[Mining](#mining)** | 0.16.9 | Ore deposits, excavators, hydrogen engines, nuclear reactors, rotors, large doors, tanks, chests, pontoons, laser cannon, jetpack, info panels, tool crafting, blueprints and more | All players |
| **[Water](#water)** | 0.4.4 | Real, swimmable water in the lakes of Otrera b with buoyancy, splashes and drowning | All players |
| **[Pick Block](#pick-block)** | 0.3.4 | Middle-click to pick a part, hold to build continuously, area building up to 9x9 | Each player |
| **[Ship Safety](#ship-safety)** | 0.5.8 | Fixes the negative-mass "launch at many c" bug, adds speed limits and ship-to-ship collision | Host (collision: all) |
| **[Resource Tweaks](#resource-tweaks)** | 0.5.1 | 3x resource yield, stacks of 100, faster mining | Host |
| **[Warp Unlock](#warp-unlock)** | 0.1.2 | Reveals the hidden Warp Drive research in the demo | All players |
| **[Planet Unlock](#planet-unlock)** | 0.1.2 | *Experimental:* loads every planet of the 442 Otrera system | All players |
| **[Shared Piloting](#shared-piloting)** | 0.1.1 | Multiple pilot seats: whoever moves the mouse steers | Host |
| **[Velocity Indicator](#velocity-indicator)** | 0.1.0 | Stable ship velocity readout when entering orbit | Each player |
| **[Crouch](#crouch)** | 0.1.4 | Crouch with Left Ctrl | Each player |
| **[Drop Items](#drop-items)** | 0.1.2 | Drop items from the toolbar or inventory | Each player |
| **[Flora](#flora)** | 0.4.1 | Decorative flower fields on the planet | Each player |
| **[FPS Counter](#fps-counter)** | 0.1.0 | FPS counter in the top-right corner (F3) | Each player |

Current versions are always listed in [`dist/VERSIONS.txt`](dist/VERSIONS.txt).

## Highlights

- **New progression:** over a dozen new research nodes (Mining, Hydrogen Propulsion, Heavy Hydrogen Engines, Nuclear Power, Large Reactor, Mechanics, Laser Weapons, Jetpack, Info Panels, Large Doors, Large Tanks, Large Storage, Tool Crafting), with the research tree grouped into colored category rows.
- **Industrial chain:** mine ore and uranium, electrolyze ice or water into hydrogen and oxygen, and power ships with chemical, hydrogen or nuclear energy.
- **Living lakes:** ships float on inflatable pontoons, sink without them, and players can swim and drown.
- **Faster building:** pick parts like in Minecraft, hold to place in a row, place whole squares of hull at once.
- **Stability fixes:** a long-standing physics bug that flings ships millions of kilometres away is fixed, along with several performance improvements for large worlds.
- **Plays well with others:** `mod.json` metadata is compatible with the [StellarModManager](https://github.com/jollyname/StellarModManager) registry.

---

## Installation

### Requirements

- Windows
- **StellarDrive Demo** from Steam
- [Git for Windows](https://git-scm.com/download/win)

MelonLoader is installed automatically if it is missing.

### First install

1. Clone the repository wherever you want to keep the mods:

   ```bash
   git clone https://github.com/FalconDevX/stellardrive-mods.git
   ```

2. **Close the game**, open the `stellardrive-mods` folder and double-click **`install.bat`**.
   The script locates the game through Steam (including additional library folders), installs MelonLoader 0.7.3 if needed and copies the mods into `<game>\Mods`.

3. Launch the game normally through Steam. A black MelonLoader console window opens alongside it; this is expected.

If the game is installed somewhere the script cannot find, pass the path explicitly:

```bat
install.bat "D:\SteamLibrary\steamapps\common\StellarDrive Demo"
```

## Updating and uninstalling

| Action | How |
|---|---|
| Update | Close the game and double-click **`update.bat`** (runs `git pull`, then reinstalls the mods). |
| Disable one mod | Delete its `.dll` from `StellarDrive Demo\Mods`. |
| Remove all mods | Delete every `.dll` from `StellarDrive Demo\Mods`. |
| Remove MelonLoader | Run `.\tools\uninstall-melonloader.ps1`. |

## Multiplayer

StellarDrive uses FishNet for multiplayer. Some mods only need to run on the host, while others change terrain, parts or research and must be installed by everyone (see the [table above](#mods-at-a-glance)). To avoid desyncs, **all players should run the same mod versions** from [`dist/VERSIONS.txt`](dist/VERSIONS.txt).

## Configuration

Every mod exposes its settings through MelonPreferences in:

```
StellarDrive Demo\UserData\MelonPreferences.cfg
```

The file is created the first time the game starts with mods installed. Settings marked *All players must use the same value* in their descriptions must match between host and clients.

A few commonly tweaked options:

| Mod | Option | Default | Description |
|---|---|---|---|
| Resource Tweaks | `DropMultiplier` | `3` | Resource yield multiplier |
| Resource Tweaks | `MaxStackSize` | `100` | Maximum stack size |
| Resource Tweaks | `InventoryRows` | `0` | Rows of slots visible in the Tab inventory window (0 = all that fit on the screen) |
| Mining | `HydrogenThrustMultiplier` | `1.8` | Hydrogen thruster strength relative to the chemical one |
| Mining | `ReactorPower` | `45000` | Reactor output in watts (the large reactor gives 5x) |
| Mining | `RotorLeverAngle` | `90` | Rotor angle for a signal of 1 |
| Mining | `RotorCollision` | `false` | Stop a turning rotor before its moving part hits something |
| Mining | `KeepNearbyShipsLoaded` | `true` | Keep nearby ships physics-loaded across spaces to avoid hitches |
| Mining | `KeepNearbyShipsDistance` | `3000` | Range of `KeepNearbyShipsLoaded` in meters; lower it if many ships cost FPS |
| Mining | `DetailDistance` | `30` | Distance beyond which small ship details are hidden (m) |
| Pick Block | `AreaMaxSize` | `9` | Largest square side for area building |
| Ship Safety | `MaxSpeed` | `5000` | Ship speed limit (m/s) |
| Ship Safety | `GuestShipCollision` | `true` | Collision between parked ships |
| Water | `LakeDepth` | `10` | How far lake beds are lowered (m) |
| Water | `UnderwaterAirSeconds` | `30` | Breath time underwater with a full oxygen tank |
| FPS Counter | `ToggleKey` | `F3` | Toggles the counter |

---

## Mod details

### Mining

The largest mod in the collection. It adds resource extraction, new power sources, propulsion and a set of new parts. **All players need it.**

<details>
<summary><b>Deposits and excavator</b></summary>

- Ore deposits appear on planets as fields of rocks that show which ore they contain. Glowing green **uranium** deposits spawn on Otrera b.
- The **Excavator** (research *Mining*, after *Chemical Propulsion*) can be placed directly on the ground (it adapts to slopes) or on a ship floor above a deposit.
- Connect power (sockets with a grey cap by the legs) and a signal of `1` (yellow-capped socket), e.g. from a lever. The drill extends and mines up to 4 stacks of 100 ore, accessible like a chest.
- Navigation keys: **N** nearest deposit, **U** nearest uranium, **K** nearest glass.

</details>

<details>
<summary><b>Hydrogen propulsion</b></summary>

- **Hydrogen thruster** (research *Hydrogen Propulsion*): needs hydrogen, oxygen and power, is 1.8x stronger than the chemical thruster and burns with a sky-blue flame.
- **Electrolyzer:** turns ice or water plus power into hydrogen, oxygen and water. Hydrogen exits on the left by the orange tank, oxygen on the right by the light blue tank; power and water enter at the back, ice goes into the front container.
- **Water pump (1x1):** above a lake on Otrera b it lowers a hose (up to 30 m, length controllable by signal) and pumps water to two outlets.
- **Heavy hydrogen engines** (research *Heavy Hydrogen Engines*): sizes **M** (2x2x3, 4x thrust), **L** (3x3x4, 4 nozzles, 9x) and **XL** (4x4x6, large nozzle plus 6 small, 20x), with proportionally higher consumption. Every nozzle shows its own sky-blue flame; the XL flame is longer. All hydrogen engines have their own rough, sputtering roar (deep combustion rumble, popping and crackle, high-pressure jet hiss); larger engines sound deeper and carry further. Cutting the throttle winds the roar down with a shutdown sound instead of cutting it off. Volume: `HydrogenEngineVolume` in the Mining preferences.

</details>

<details>
<summary><b>Nuclear power</b></summary>

- **Reactor** (research *Nuclear Power*): uranium + water + a signal of `1` produces 45,000 W (the Large Nuclear Reactor 225,000 W). A detailed small modular reactor in a steel frame: shielded pressure vessel with a domed, bolted closure head and a bank of control-rod drives, two steam generators with steam lines, two coolant pumps with motors, a pressurizer, glowing coolant windows, a fuel hatch and a control console; all four sockets (two power, water, signal) sit on one service panel. Only the windows, screens and lamps glow, the rest keeps its real colors. It spins its cooling fan under the top grille, lights its surroundings and hums while running.
- **Large reactor** (research *Large Reactor*): 4x4x4, 100,000 W, uses 5x more uranium and water. The same design at double size, with all four sockets on one service panel.

</details>

<details>
<summary><b>Building parts and storage</b></summary>

- **Rotor** (research *Mechanics*): rotates the structure on its plate. A lever works out of the box (`0` = home, `1` = 90°); signals above 1 are interpreted as degrees. The turning part passes through obstacles unless `RotorCollision` is enabled.
- **Heavy Gyroscope** (research *Mechanics*, Propulsion tab): a 2x2x2 gyroscope built on the normal one, with a heavy flywheel, bolted bearing housings, balance weights, power conditioners with cabling and status lamps that brighten while it turns. 8 times the turning strength and 8 times the power (up to 6.4 kW). Wired like the normal gyroscope: four sockets low on each side, at the same spots as on the normal one, just twice as far apart.
- **Large doors and gates** (research *Large Doors*): doors 1x2, 3x2, 3x3, 4x4, 6x3 and gates 10x5, 16x8, 24x8, 32x8, with heavy opening animation and custom sound. The 1x2 door is a slatted roller shutter with its own lighter sound. They block passage until fully open.
- **Yellow Barrier** (research *Large Doors*, Doors tab): a 1 m round safety railing with hazard stripes that always stands upright on the floor; a barrier placed near another snaps to its end, and joined barriers share one post. Barriers have a solid 1.2 m high, 10 cm thick collider, so you cannot walk or fall through them, but building aims through them, so floors, walls and other parts snap right next to a railing. Barriers join end to end, in L corners and T joints; a barrier that would overlap or cross another one, or stand inside stairs, cannot be placed.
- **Metal Stairs** (research *Large Doors*, Doors tab): 45-degree steel stairs, 1 m wide and 2 m long, climbing one deck (2 m) in 8 steps with handrails on both sides; you walk up and down a smooth ramp collider and stay on your feet at the top and bottom edges instead of hopping; the handrails are solid, so you cannot fall off the side. Cut a 1 x 2 hole in the floor above. Aim at a flight with stairs selected to add the next flight above (upper half) or below (lower half); other parts are built through the stairs as if they were not there, so floors snap next to them. A barrier placed along the hole edge on the upper floor or continuing a handrail below snaps onto the handrail post. The handrails end in barrier posts at both floors, and a Yellow Barrier placed next to them snaps on and shares the post.
- **Retractable Stairs** (research *Large Doors*, Doors tab): a 1 x 2 floor plate with stairs folded inside, without handrails. Signal 1 on the socket at the back end slowly slides 45-degree stairs out of the front end and down until they stop just above the ground or another obstacle (up to 40 m); if the ship settles lower they pull back to stay on the ground. Signal 0 pulls them back in. Place it at the edge of a deck with the front facing out, e.g. as a boarding ramp to the ground. Your own ship does not stop the stairs.
- **Large tanks** (research *Large Tanks*): Tank 2x2 (800) and Tank 4x4 (6400), with four pipe sockets, each in its own housing that reaches from the frame to the tank wall. The build preview of large tanks is centered on the crosshair.
- **Larger vents, water pumps and electrolyzers** (research *Large Tanks*, next to the small part in the build menu): Air Vent M / L (2x / 3x size, fill rooms with oxygen 4x / 9x faster), Water Pump M / L (thicker hose lowered into lakes like the small pump, 4x / 9x the water and power) and industrial Electrolyzer M / L (own model: long electrolysis stack with radiators, 2 / 3 hydrogen and oxygen tanks, 4x / 9x the output and power). Pipe and power sockets on the larger parts sit exactly on cell centres at the same height as on the small parts, so pipes line up.
- **Large chests** (research *Large Storage*): Medium Chest (2x2x2, 30 slots), Large Chest (3x3x3, 60 slots) and Huge Chest (4x4x4, 120 slots). All use the same slot size; scroll the mouse wheel over the grid to see more rows.
- **Info panels** (research *Info Panels*): XS (1x0.5, 1 line), S (1x1, 1 line), M (2x1, 2 lines) and L (3x2, 4 lines) screens that show signal values from inputs A-D. Each panel takes up exactly its real size on the wall, so panels sit next to each other without overlapping. A framed screen with a connector strip along the bottom; all sockets are on the bottom edge facing down, so cables come from below: signals A-D from the left, power (yellow mark) on the right. Without power the screen stays dark. Press **E** on a panel to write your own text: one label per line, separated by `;` (e.g. `Fuel;Oxygen`); a line with a connected signal shows the label and the value, a line without a signal shows only the text (e.g. `HANGAR 2`). The text is saved with the ship and synced to all players.
- **Power Meter** (research *Info Panels*, Control tab): a small 2-cell-wide box that sits flat on the surface, with a power socket (yellow) and a signal output (dark) side by side on the front. Plug it into a power network and it sends the network load in percent (power needed / generator capacity; 100 = fully used, above 100 = overloaded). Wire the signal to an info panel to see the load live. Uses no power itself.
- The **Paint Tool** menu shows a color palette (grey column plus 11 hues, light to dark rows) instead of HSV sliders, and its toolbar icon is a detailed paint spray gun with a rainbow paint cup and pressure gauge.
- The build menu (**Tab**) is wider and split into tabs: Control, Propulsion, Power, Fluids, Doors and Utility.

</details>

<details>
<summary><b>Tool crafting and blueprints</b></summary>

- **Tool Workbench** (research *Tool Crafting*, Utility tab, 2x1): a full workbench model; press **F** to open a crafting window in the style of the research menu and craft the Build Tool, Recycler Tool, Mining Laser, Paint Tool and Blueprint Tool from materials in your inventory.
- **Blueprint Tool** (its own handheld scanner model): LMB on one corner of a ship area, LMB on the opposite corner, LMB again to save it (hull, parts, colors and cables). RMB cancels.
- **B** opens the blueprint catalog with a top-down preview, size, parts and material cost; rename or delete blueprints there. Files are stored in `UserData/Blueprints`.
- Pick a blueprint in the catalog, then with the Blueprint Tool aim at a ship (to extend it) or at the ground (to create a new ship): scroll rotates, Shift+scroll changes height, LMB places a blue hologram, RMB on a hologram removes it.
- Aim the **Build Tool** at a hologram and click to build it at once if your inventory holds all materials; parts that already exist are skipped.
- Crafting and blueprints work for the host.

</details>

<details>
<summary><b>Pontoons, weapons, jetpack and camera</b></summary>

- **Inflatable pontoons:** feed oxygen into the inlet to inflate them. They slowly leak while air is supplied and deflate about 30 s after the supply is cut. With the Water mod each pontoon supports about 2000 kg; on land they act as skids.
- **Laser cannon** (research *Laser Weapons*): fires while its signal is above 0, has recoil and overheats after about 6 s of continuous fire, with status lights and venting steam.
- The advanced pilot seat gets extra **L** and **R** signal outputs for the mouse buttons.
- **Jetpack** (research *Jetpack*): the last backpack slot holds wood as fuel; hold Jump in mid-air to fly. Shift-click wood to load it. Press **L** in flight for altitude hold: you hover at the current height and steer freely with WASD, Jump climbs and Crouch descends; it turns off on landing. Each player has their own hold.
- **V** in a pilot seat toggles a chase camera behind the ship.

</details>

<details>
<summary><b>Performance and diagnostics</b></summary>

- Cached hull bounds, throttled cable visuals and faster room/oxygen recalculation reduce lag on large worlds. Ships within 3 km stay physics-loaded when you move between areas, so their colliders are not rebuilt (no hitch).
- Small ship details are not drawn beyond 30 m (`DetailDistance`).
- **F11** writes a performance report to `UserData/Mining/fps_report.txt`; the cause of death is logged to `UserData/Mining/death_log.txt`.
- Custom sounds: drop a `.wav` named after a built-in sound (e.g. `reactor.wav`) into `UserData/Mining` to replace it.

</details>

### Water

Turns the blue terrain of Otrera b's lakes into real water. **All players need it** (it lowers lake beds by 10 m).

- Stylized shader with turquoise shallows, deep blue depths, caustics and shoreline foam; a light, translucent surface when seen from below.
- Ships in a lake brake, bounce and slowly sink. With inflated pontoons from Mining they float, with buoyancy computed per pontoon so ships stay upright.
- Gentle waves, sandy lake beds, impact splashes, pontoon spray and wakes.
- Submerged ships take on water, which drains from the cockpit and hull once they surface; droplets run down the glass.
- **Swimming:** WASD to swim where you look, Space up, Crouch down, Shift faster.
- Breath lasts 30 s underwater with a full oxygen tank, after which the player drowns.
- The shader source lives in [`src/Water/UnityProject`](src/Water/UnityProject) (Unity 6000.3.24f1).

### Pick Block

- **Middle mouse** on a part selects the same part for building.
- **Hold left mouse** to keep placing the part in each next free spot.
- **Alt + mouse wheel** on a floor, wall, window, diagonal wall or slope resizes the build square (1x1 to 9x9, with an outline and a label by the crosshair); one click places the whole square. The wheel alone still rotates parts.

### Ship Safety

- Fixes a game bug where fuel in tanks is counted as negative, giving the ship negative mass and flinging it at many times the speed of light millions of kilometres from the planet.
- Guards against mass errors, sudden speed jumps and enforces a 5000 m/s limit.
- Ships parked on or next to each other collide properly and can be moved with the tool afterwards (`GuestShipCollision`, needs the mod on both sides; everything else works host-only).

### Resource Tweaks

3x resources from tool-less mining, stack size of 100, faster mining (trees -50% time, everything else -25%), a 45-slot personal inventory and a taller inventory window under Tab that shows more rows of normal-sized slots (`InventoryRows`). Host only, the window size is per player.

### Warp Unlock

Reveals the Warp Drive research hidden in the demo, together with its prerequisites, so you can fly to other planets. Hides the "Demo Complete" screen (sensors come from the unlocked *Sensors* research). All players need it.

### Planet Unlock

> [!WARNING]
> Experimental.

Loads every planet of the 442 Otrera system (the demo only loads Otrera b) so they can be reached by warp. All players need it.

### Shared Piloting

Supports multiple pilot seats: whoever moves the mouse steers the ship, and the idle seat's built-in gyroscope no longer locks rotation. Host only.

### Velocity Indicator

The ship velocity indicator no longer jumps sideways when entering orbit (it shows speed relative to the planet surface) and no longer shows a false zero. Each player needs it.

### Crouch

Crouch with **Left Ctrl**: lower character height and camera, slower movement. Standing or crouching on a ship part (for example an engine) never pushes the ship.

### Drop Items

Drop items with **Q** from the selected toolbar slot or **X** from the inventory slot under the cursor. Hold **Shift** to drop half the stack.

### Flora

Decorative flower fields on the planet surface.

### FPS Counter

FPS counter in the top-right corner, toggled with **F3**.

---

## For mod developers

Mods are written in C# targeting .NET Framework 4.7.2 and patch the game with Harmony.

### Requirements

- .NET SDK 8 or newer (`dotnet --list-sdks`)
- StellarDrive Demo, by default at `C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo`.
  For a different location, copy [`GamePath.props.example`](GamePath.props.example) to `GamePath.props` or set the `STELLARDRIVE_DIR` environment variable.
- MelonLoader in the game: `.\tools\install-melonloader.ps1`

### Workflow

```powershell
.\tools\new-mod.ps1 -Name MoreIron      # scaffold a new mod in src/MoreIron
dotnet build                            # build all mods and copy them to <game>\Mods
.\tools\run-game.ps1 -Follow            # start the game and tail MelonLoader\Latest.log
.\tools\pack.ps1                        # build Release into dist\Mods for players
```

In VS Code use `Ctrl+Shift+B` (build) or the **build + run game** task.

- Log: `<game>\MelonLoader\Latest.log`
- Settings: `<game>\UserData\MelonPreferences.cfg`
- Clean game: `.\tools\uninstall-melonloader.ps1`

### Project structure

```
Directory.Build.props    shared settings: net472, references to MelonLoader and all game assemblies
Directory.Build.targets  path checks and automatic copy of built DLLs to <game>\Mods
src/<Mod>/               one folder per mod (csproj, MelonMod class, Patches/, mod.json)
tools/                   MelonLoader install/uninstall, run-game, new-mod, pack, player installer
dist/Mods/               prebuilt DLLs for players (updated by tools\pack.ps1)
install.bat, update.bat  player install and update entry points
```

### Tips

- Game logic is split across modules in `StellarDrive_Data\Managed` (`Ships.dll`, `Items.Model.dll`, `Planets.dll`, `Players.dll`, `Research.dll`, `Saving.dll`, `UI.dll`, ...), not just `Assembly-CSharp.dll`. Browse them with [ILSpy](https://github.com/icsharpcode/ILSpy) or dnSpyEx.
- The game uses the **new Input System** (`UnityEngine.InputSystem.Keyboard.current`); `UnityEngine.Input` does not work.
- Access private fields in patches with `___fieldName` parameters or `AccessTools.Field(...)`.
- Multiplayer runs on FishNet: gameplay-changing mods usually need to run on the host, often on every client too.
- Decompiled game code is never committed (`decompiled/` is in `.gitignore`).
- [`src/HelloStellar`](src/HelloStellar) is a minimal example: logging, a preference entry (`DebugKey`, F8 prints the camera position), scene-load handling and a Harmony patch on `Items.Model.ItemSettingsList.ResetDict`.

---

## License

This repository does not include a license file yet, so all rights are reserved by default. StellarDrive and its assets belong to Curious Owl.
