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
| `Luggages` | empty | Optional luggage display names to multiply (see below) |

### Luggage names (`Luggages`)

Comma-separated **exact** prefab display names. Do **not** wrap the value in quotation marks in the `.cfg` (quotes become part of the name and only middle entries like `Big Luggage` will match).

```text
Luggage,Big Luggage,Explorer's Luggage
```

Known vanilla names:

| Display name | Notes |
|---|---|
| `Luggage` | Regular / small luggage |
| `Big Luggage` | Tall white luggage |
| `Explorer's Luggage` | Orange explorer briefcase (straight apostrophe `'`) |
| `Ancient Luggage` | Ancient luggage |
| `Ancient Statue` | Respawn statues (also a luggage spawner) |
| `Clown Luggage` | Clown luggage |

There is no public PEAK API or official name list; these come from the game’s Luggage `displayName` fields.

## Install

1. Use r2modman / Gale with BepInEx for PEAK, **or** drop the built DLL into `BepInEx/plugins/`.
2. Do **not** run this together with other item-multiplier mods that patch the same spawners.

## Build

Requires [PEAK](https://store.steampowered.com/app/3527290/PEAK/) installed (auto-detected under common Steam paths), or set `PEAK_GAME_DIR` / `-p:PeakGameRootDir=`.

```bash
dotnet build -c Release
```

Optional deploy: `-p:DeployToPeak=true -p:PeakPluginsDir="/path/to/BepInEx/plugins/ExtraMouths"`  
Optional overrides: copy `Config.Build.user.props.example` → `Config.Build.user.props` (gitignored).

## Thunderstore packaging (CI)

Every push to `master` runs [.github/workflows/thunderstore.yml](.github/workflows/thunderstore.yml):

1. Builds a Thunderstore ZIP (using stripped [PEAKGameLibs](https://www.nuget.org/packages/PEAKGameLibs) for compile references)
2. Uploads a workflow artifact named **`FakeGameDevelopers-ExtraMouths`**

Download that artifact and upload the resulting `FakeGameDevelopers-ExtraMouths.zip` straight to Thunderstore — no unpacking/repacking needed. Publish under the **FakeGameDevelopers** team.

### Manual publish from Actions

1. Actions → **Thunderstore** → **Run workflow**
2. Enable **Publish the package to Thunderstore**
3. Requires repo secret `TCLI_AUTH_TOKEN` (Thunderstore team → Service Accounts)

### Auto-publish on every master push

1. Add secret `TCLI_AUTH_TOKEN`
2. Add repository variable `AUTO_PUBLISH_THUNDERSTORE` = `true`

Without that variable, pushes only build artifacts (safe default).

### Local package build

```bash
dotnet build -c Release -target:PackTS
# zip lands in artifacts/thunderstore/
```

## Credits

- Author: **Arman Ossi Loko**
- Spawn-multiplier approach adapted from [ItemMultiplierBis](https://github.com/Wesmania/peak-item-multiplier-bis) (MIT), which improved on IceMods' ItemMultiplier
