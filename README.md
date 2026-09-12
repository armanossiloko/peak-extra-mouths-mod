# ExtraMouths

A PEAK mod that scales **map forage food** (berries, coconuts, mushrooms, and similar) when more than four players are connected.

Only the **host** needs this mod.

## Formula

When **5 or more** players are connected (host-side):

```text
multiplier = (connectedPlayers + ExtraPlayers) / BaselinePlayers
```

Defaults: `ExtraPlayers = 2`, `BaselinePlayers = 4`, `MinPlayersToScale = 5`.

| Connected players | Effective mouths | Multiplier |
|-------------------|------------------|------------|
| 1–4               | —                | **vanilla** (mod skipped) |
| 5                 | 7                | 1.75x |
| 6                 | 8                | 2.0x |
| 10                | 12               | 3.0x |

## What gets scaled (defaults)

- Berry bushes
- Berry vines
- Ground forage (e.g. mushrooms)
- Food spawn pools on plain spawners (coconuts, winterberries, kingberries, clusterberries, nests, cactus fruit, redwood fungi, etc.)

Luggage is **not** scaled by default. Campfire marshmallows are unchanged (use a lobby-size mod such as PEAK Unlimited for those).

## Configuration

After one launch, edit:

`BepInEx/config/ExtraMouths.cfg`

| Key | Default | Meaning |
|-----|---------|---------|
| `ExtraPlayers` | `2` | Extra mouths beyond the live lobby |
| `BaselinePlayers` | `4` | Vanilla balance party size |
| `MinPlayersToScale` | `5` | Below this → full vanilla rates |
| `BerryBushes` / `BerryVines` / `GroundSpawns` / `FoodSpawnPools` | `true` | Toggle food categories |
| `Luggages` | empty | Optional luggage display names to multiply |

## Install

1. Use r2modman / Gale with BepInEx for PEAK, **or** drop the built DLL into `BepInEx/plugins/`.
2. Do **not** run this together with other item-multiplier mods that patch the same spawners.

## Build

Requires [PEAK](https://store.steampowered.com/app/3527290/PEAK/) installed so game assemblies can be referenced.

```bash
dotnet build -c Release
```

The build auto-detects PEAK under common Steam library locations. If it cannot find the game, set one of:

```bash
# Environment variable (recommended)
export PEAK_GAME_DIR="/path/to/Steam/steamapps/common/PEAK"

# Or pass MSBuild property
dotnet build -c Release -p:PeakGameRootDir="/path/to/Steam/steamapps/common/PEAK"
```

Optional local overrides (gitignored): copy `Config.Build.user.props.example` to `Config.Build.user.props`.

Optional deploy into a BepInEx plugins folder after build:

```bash
dotnet build -c Release -p:DeployToPeak=true -p:PeakPluginsDir="/path/to/BepInEx/plugins/ExtraMouths"
```

Thunderstore package:

```bash
./build.sh
# or: dotnet build -c Release -target:PackTS -v d
```

## Credits

- Author: **Arman Ossi Loko**
- Spawn-multiplier approach adapted from [ItemMultiplierBis](https://github.com/Wesmania/peak-item-multiplier-bis) (MIT), which improved on IceMods' ItemMultiplier
