using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using pworld.Scripts.Extensions;
using UnityEngine;

namespace ExtraMouths;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<int> ExtraPlayers { get; private set; } = null!;
    internal static ConfigEntry<int> BaselinePlayers { get; private set; } = null!;
    internal static ConfigEntry<int> MinPlayersToScale { get; private set; } = null!;
    internal static ConfigEntry<string> Luggages { get; private set; } = null!;
    internal static ConfigEntry<bool> GroundSpawns { get; private set; } = null!;
    internal static ConfigEntry<bool> BerryBushes { get; private set; } = null!;
    internal static ConfigEntry<bool> BerryVines { get; private set; } = null!;
    internal static ConfigEntry<bool> FoodSpawnPools { get; private set; } = null!;

    private static HashSet<string> luggageSet = [];

    /// <summary>
    /// Vanilla food-oriented spawn pools used by the base <see cref="Spawner"/> path
    /// (coconuts, winterberries, kingberries, clusterberries, mushrooms, etc.).
    /// Luggage and campfire pools are intentionally excluded.
    /// </summary>
    private static readonly HashSet<SpawnPool> FoodPools =
    [
        SpawnPool.MushroomCluster,
        SpawnPool.BerryBushBeach,
        SpawnPool.BerryBushJungle,
        SpawnPool.SpikyVine,
        SpawnPool.CoconutTree,
        SpawnPool.WillowTreeJungle,
        SpawnPool.JungleVine,
        SpawnPool.WinterberryTree,
        SpawnPool.Nest,
        SpawnPool.Cactus,
        SpawnPool.Redwood,
    ];

    private void Awake()
    {
        Log = Logger;

        ExtraPlayers = Config.Bind(
            "Scaling",
            "ExtraPlayers",
            2,
            "Treat the lobby as this many extra mouths to feed. Multiplier = (connectedPlayers + ExtraPlayers) / BaselinePlayers.");

        BaselinePlayers = Config.Bind(
            "Scaling",
            "BaselinePlayers",
            4,
            "Vanilla balance player count. Food is scaled relative to this.");

        MinPlayersToScale = Config.Bind(
            "Scaling",
            "MinPlayersToScale",
            5,
            "If connected players are below this, leave spawn rates fully vanilla. Default 5 so 4-player lobbies match the game.");

        BerryBushes = Config.Bind("Spawners", "BerryBushes", true, "Scale berry bushes.");
        BerryVines = Config.Bind("Spawners", "BerryVines", true, "Scale berry vines.");
        GroundSpawns = Config.Bind("Spawners", "GroundSpawns", true, "Scale ground forage (e.g. mushrooms).");
        FoodSpawnPools = Config.Bind(
            "Spawners",
            "FoodSpawnPools",
            true,
            "Scale base Spawner food pools (coconuts, winterberries, kingberries, clusterberries, nests, cactus fruit, etc.).");

        Luggages = Config.Bind(
            "Spawners",
            "Luggages",
            "",
            "Optional: comma-separated luggage display names to also multiply (e.g. \"Luggage,Big Luggage,Explorer's Luggage\"). Empty = luggage untouched.");

        luggageSet = BuildLuggageSet(Luggages.Value);
        Luggages.SettingChanged += (_, _) => luggageSet = BuildLuggageSet(Luggages.Value);

        var harmony = new Harmony(Id);
        try
        {
            harmony.PatchAll();
            Log.LogInfo($"{Name} v{Version} loaded. Formula: (players + {ExtraPlayers.Value}) / {BaselinePlayers.Value}, skip if players < {MinPlayersToScale.Value}.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to patch: {ex}");
        }
    }

    private static HashSet<string> BuildLuggageSet(string raw)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return set;
        }

        foreach (var part in raw.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                set.Add(trimmed);
            }
        }

        return set;
    }

    internal static int GetConnectedPlayerCount()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            return PhotonNetwork.CurrentRoom.PlayerCount;
        }

        var list = PhotonNetwork.PlayerList;
        return list?.Length ?? 0;
    }

    /// <summary>
    /// Returns false when the lobby is below the minimum — patches should no-op (vanilla rates).
    /// </summary>
    internal static bool ShouldScale()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return false;
        }

        int players = GetConnectedPlayerCount();
        int min = Math.Max(1, MinPlayersToScale.Value);
        return players >= min;
    }

    /// <summary>
    /// Effective spawn multiplier. Always 1 when <see cref="ShouldScale"/> is false.
    /// Example: 5 players + 2 extra → 7/4 = 1.75x; 10 players → 12/4 = 3x.
    /// Four-player lobbies stay vanilla when MinPlayersToScale is 5.
    /// </summary>
    internal static float GetMultiplier()
    {
        if (!ShouldScale())
        {
            return 1f;
        }

        int players = GetConnectedPlayerCount();
        int baseline = Math.Max(1, BaselinePlayers.Value);
        int extra = Math.Max(0, ExtraPlayers.Value);
        return (players + extra) / (float)baseline;
    }

    internal static List<Transform> AddMoreSlotsUntil(List<Transform> spawnSpots, int target, bool addRandom = false)
    {
        if (spawnSpots.Count == 0 || (target <= 0 && !addRandom))
        {
            return [];
        }

        int count = spawnSpots.Count;
        var newSpots = new List<Transform>(Math.Max(0, target) + (addRandom ? 1 : 0));

        while (target >= count)
        {
            newSpots.AddRange(spawnSpots);
            target -= count;
        }

        if (target > 0)
        {
            spawnSpots.Shuffle();
            newSpots.AddRange(spawnSpots.GetRange(0, target));
        }

        if (addRandom)
        {
            newSpots.Add(spawnSpots[UnityEngine.Random.Range(0, count)]);
        }

        return newSpots;
    }

    internal static List<Transform> AddMoreSlots(List<Transform> spawnSpots, float multiplier)
    {
        if (spawnSpots.Count == 0 || multiplier <= 0f)
        {
            return [];
        }

        // Multiplier is absolute: 1.0 keeps the same slot count, 2.0 doubles it.
        float toAdd = spawnSpots.Count * multiplier;
        int guaranteed = Mathf.FloorToInt(toAdd);
        float lastChance = toAdd - guaranteed;
        bool addLast = UnityEngine.Random.value < lastChance;
        return AddMoreSlotsUntil(spawnSpots, guaranteed, addLast);
    }

    internal static void MultiplySpawnRange(ref Vector2 range, ref List<Transform> spawnSpots, out Vector2 original)
    {
        original = range;
        float multiplier = GetMultiplier();
        range *= multiplier;

        float maxSpawns = Math.Max(range.x, range.y);
        if (maxSpawns > spawnSpots.Count)
        {
            var newSpots = AddMoreSlotsUntil(spawnSpots, Mathf.CeilToInt(maxSpawns));
            spawnSpots.Clear();
            spawnSpots.AddRange(newSpots);
        }
    }

    [HarmonyPatch(typeof(BerryBush))]
    public static class BerryBushPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BerryBush.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPrefix(BerryBush __instance, ref List<Transform> spawnSpots, out Vector2 __state)
        {
            __state = __instance.possibleBerries;
            if (!BerryBushes.Value || !ShouldScale())
            {
                return;
            }

            MultiplySpawnRange(ref __instance.possibleBerries, ref spawnSpots, out __state);
            var n = __instance.possibleBerries;
            Log.LogInfo($"[BerryBush] players={GetConnectedPlayerCount()} mult={GetMultiplier():0.###}x range {__state.x}-{__state.y} → {n.x}-{n.y}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BerryBush.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPostfix(BerryBush __instance, Vector2 __state)
        {
            // Always restore; Prefix may have temporarily widened the range.
            __instance.possibleBerries = __state;
        }
    }

    [HarmonyPatch(typeof(BerryVine))]
    public static class BerryVinePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BerryVine.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPrefix(BerryVine __instance, ref List<Transform> spawnSpots, out Vector2 __state)
        {
            __state = __instance.possibleBerries;
            if (!BerryVines.Value || !ShouldScale())
            {
                return;
            }

            MultiplySpawnRange(ref __instance.possibleBerries, ref spawnSpots, out __state);
            var n = __instance.possibleBerries;
            Log.LogInfo($"[BerryVine] players={GetConnectedPlayerCount()} mult={GetMultiplier():0.###}x range {__state.x}-{__state.y} → {n.x}-{n.y}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BerryVine.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPostfix(BerryVine __instance, Vector2 __state)
        {
            __instance.possibleBerries = __state;
        }
    }

    [HarmonyPatch(typeof(GroundPlaceSpawner))]
    public static class GroundPlaceSpawnerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GroundPlaceSpawner.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPrefix(GroundPlaceSpawner __instance, ref List<Transform> spawnSpots, out Vector2 __state)
        {
            __state = __instance.possibleItems;
            if (!GroundSpawns.Value || !ShouldScale())
            {
                return;
            }

            float multiplier = GetMultiplier();
            __instance.possibleItems *= multiplier;

            Vector2 v = __instance.possibleItems;
            float chanceHigherX = v.x - Mathf.Floor(v.x);
            float chanceHigherY = v.y - Mathf.Floor(v.y);
            v.x = UnityEngine.Random.value < chanceHigherX ? Mathf.Ceil(v.x) : Mathf.Floor(v.x);
            v.y = UnityEngine.Random.value < chanceHigherY ? Mathf.Ceil(v.y) : Mathf.Floor(v.y);
            __instance.possibleItems = v;

            float maxSpawns = Math.Max(v.x, v.y);
            if (maxSpawns > spawnSpots.Count)
            {
                var newSpots = AddMoreSlotsUntil(spawnSpots, Mathf.CeilToInt(maxSpawns));
                spawnSpots.Clear();
                spawnSpots.AddRange(newSpots);
            }

            Log.LogInfo($"[GroundPlace] players={GetConnectedPlayerCount()} mult={multiplier:0.###}x range {__state.x}-{__state.y} → {v.x}-{v.y}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(GroundPlaceSpawner.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPostfix(GroundPlaceSpawner __instance, Vector2 __state)
        {
            __instance.possibleItems = __state;
        }
    }

    [HarmonyPatch(typeof(Spawner))]
    public static class SpawnerPatch
    {
        private static bool IsConfiguredLuggage(Spawner spawner)
        {
            if (luggageSet.Count == 0)
            {
                return false;
            }

            return spawner is Luggage luggage && luggageSet.Contains(luggage.displayName);
        }

        private static bool IsFoodPoolSpawner(Spawner spawner)
        {
            if (!FoodSpawnPools.Value)
            {
                return false;
            }

            // BerryBush / BerryVine / GroundPlaceSpawner override SpawnItems; this path is for
            // plain Spawner instances (coconut trees, winterberry trees, etc.).
            if (spawner is BerryBush or BerryVine or GroundPlaceSpawner or Luggage)
            {
                return false;
            }

            return FoodPools.Contains(spawner.GetSpawnPool());
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Spawner.SpawnItems), typeof(List<Transform>))]
        public static void SpawnItemsPrefix(Spawner __instance, ref List<Transform> spawnSpots)
        {
            if (!ShouldScale())
            {
                return;
            }

            bool scaleLuggage = IsConfiguredLuggage(__instance);
            bool scaleFood = IsFoodPoolSpawner(__instance);
            if (!scaleLuggage && !scaleFood)
            {
                return;
            }

            float multiplier = GetMultiplier();
            var newSpots = AddMoreSlots(spawnSpots, multiplier);
            spawnSpots.Clear();
            spawnSpots.AddRange(newSpots);
            Log.LogInfo($"[Spawner:{__instance.gameObject.name}] pool={__instance.spawnPool} players={GetConnectedPlayerCount()} mult={multiplier:0.###}x → {spawnSpots.Count} slots");
        }
    }
}
