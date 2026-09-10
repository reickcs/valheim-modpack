using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;

namespace ValheimQoL
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class ValheimQoLPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "richard.valheimqol";
        public const string PluginName = "ValheimQoL";
        public const string PluginVersion = "0.10.0";

        // Every ConfigEntry below is wrapped in _configSync.AddConfigEntry, which
        // makes the server the source of truth once a client connects to one --
        // same mechanism AzuCraftyBoxes/ServerCharacters already use (confirmed by
        // their own "ConfigSync RPC registered" boot log lines). ModRequired is
        // left false: a client without ValheimQoL at all can still connect, they
        // just don't get synced values (nothing to sync into). A client WITH it
        // installed always ends up matching whatever the server has, no manual
        // config-file distribution needed anymore.
        private static ConfigSync _configSync;

        // Static so the (static) Harmony patch classes can log -- BepInEx's
        // own Logger property is per-instance, only reachable from here.
        internal static ManualLogSource Log;

        // Player - Gameplay: auto-pickup
        public static SyncedConfigEntry<float> AutoPickupRange;

        // Player - Gameplay: stamina
        public static SyncedConfigEntry<float> StaminaUseMultiplier;
        public static SyncedConfigEntry<bool> FreeBuildToolStamina;

        // Player - Gameplay: carry weight
        public static SyncedConfigEntry<float> MaxCarryWeightMultiplier;

        // Player - Gameplay: food
        public static SyncedConfigEntry<bool> FoodBuffsDontDecay;

        // Player - Gameplay: equipment
        public static SyncedConfigEntry<bool> AutoEquipShieldWithOneHandedWeapon;

        // Player - Camera
        public static SyncedConfigEntry<float> CameraFov;
        public static SyncedConfigEntry<float> CameraMaxZoom;

        // Player - Building
        public static SyncedConfigEntry<float> BuildRotationDegrees;

        // Crafting & Production - Workbench
        public static SyncedConfigEntry<float> WorkbenchRangeMultiplier;
        public static SyncedConfigEntry<bool> WorkbenchIgnoreRoofRequirement;

        // Crafting & Production - Smelter/Fermenter/Beehive/CookingStation
        public static SyncedConfigEntry<float> ProductionSpeedMultiplier;
        public static SyncedConfigEntry<float> ProductionCapacityMultiplier;

        // Crafting & Production - Fire Sources
        public static SyncedConfigEntry<bool> FireplaceInfiniteFuel;
        public static SyncedConfigEntry<bool> FireplaceAutoRefuelEnabled;
        public static SyncedConfigEntry<float> FireplaceAutoRefuelRange;
        public static SyncedConfigEntry<float> FireplaceAutoRefuelInterval;

        // Building - Structural Integrity
        public static SyncedConfigEntry<bool> BuildingIgnoreSupport;
        public static SyncedConfigEntry<bool> BuildingIgnoreWeatherDecay;

        // Items
        public static SyncedConfigEntry<float> StackSizeMultiplier;

        // Player - Gathering
        public static SyncedConfigEntry<float> GatheringYieldMultiplier;

        // Portals
        public static SyncedConfigEntry<bool> AllowOreThroughPortals;

        // Inventory
        public static SyncedConfigEntry<bool> InventoryFillFirstSlot;

        // Creatures - Tamed Pets
        public static SyncedConfigEntry<bool> TamedPetsCantDie;

        // Wagon
        public static SyncedConfigEntry<float> WagonBaseMassMultiplier;
        public static SyncedConfigEntry<float> WagonItemWeightMultiplier;

        // Map
        public static SyncedConfigEntry<float> MapExploreRadiusMultiplier;

        // Player - Camera
        public static SyncedConfigEntry<bool> DisableScreenShake;

        // Server - Game Difficulty
        public static SyncedConfigEntry<float> DifficultyDamageScalePerPlayer;
        public static SyncedConfigEntry<float> DifficultyHealthScalePerPlayer;
        public static SyncedConfigEntry<int> DifficultyMaxScalingPlayers;

        // Server - Character Storage
        public static SyncedConfigEntry<bool> ServerCharacterStorageEnabled;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            _configSync = new ConfigSync(PluginGUID)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = PluginVersion,
                ModRequired = true
            };

            // Numeric settings all carry an AcceptableValueRange so ConfigurationManager
            // (and any similar BepInEx config UI) renders them as a slider instead of a
            // plain text box. Out-of-range values (e.g. from a hand-edited .cfg) are
            // silently clamped to the nearest bound by BepInEx itself, not rejected.
            AutoPickupRange = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "AutoPickupRange", 4f,
                new ConfigDescription(
                    "Radius (meters) within which items are automatically picked up. Vanilla default is 2.",
                    new AcceptableValueRange<float>(0f, 20f))));

            StaminaUseMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "StaminaUseMultiplier", 0.75f,
                new ConfigDescription(
                    "Multiplier applied to all stamina costs (building, running, jumping, attacking, dodging). 1 = vanilla, 0.5 = half cost, 0 = no stamina cost at all.",
                    new AcceptableValueRange<float>(0f, 2f))));

            FreeBuildToolStamina = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "FreeBuildToolStamina", true,
                "If true, hammer, hoe, and cultivator use no stamina at all -- Prefix on private Player.GetBuildStamina, which every build/remove/repair/terrain/planting action reads its cost from (GetRightItem().m_shared.m_attack.m_attackStamina). Combat/run/jump/dodge/swim stamina (StaminaUseMultiplier above) are untouched."));

            MaxCarryWeightMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "MaxCarryWeightMultiplier", 1.5f,
                new ConfigDescription(
                    "Multiplier applied to max carry weight before food/potion bonuses. Vanilla base is 300. 1 = vanilla.",
                    new AcceptableValueRange<float>(1f, 5f))));

            FoodBuffsDontDecay = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "FoodBuffsDontDecay", false,
                "Vanilla ramps a food's HP/stamina/eitr bonus down as its timer runs low (Player.UpdateFood: bonus *= (time-remaining/burn-time)^0.3). If true, each food gives its full bonus for its entire duration and only drops off exactly when it expires."));

            AutoEquipShieldWithOneHandedWeapon = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gameplay", "AutoEquipShieldWithOneHandedWeapon", false,
                "If true, equipping any one-handed weapon with an empty off-hand automatically equips a shield from inventory too (a Valheim Plus feature)."));

            CameraFov = _configSync.AddConfigEntry(Config.Bind(
                "Player - Camera", "FieldOfView", 65f,
                new ConfigDescription(
                    "Camera field of view in degrees. Vanilla default is 65.",
                    new AcceptableValueRange<float>(30f, 120f))));

            CameraMaxZoom = _configSync.AddConfigEntry(Config.Bind(
                "Player - Camera", "MaxZoomDistance", 6f,
                new ConfigDescription(
                    "Maximum third-person camera zoom-out distance. Vanilla default is 6.",
                    new AcceptableValueRange<float>(2f, 30f))));

            BuildRotationDegrees = _configSync.AddConfigEntry(Config.Bind(
                "Building - General", "FreeRotationDegrees", 22.5f,
                new ConfigDescription(
                    "Degrees per rotation step while placing pieces. Vanilla default is 22.5 (16 steps/circle). Lower = finer rotation control, e.g. 5 for near-free rotation.",
                    new AcceptableValueRange<float>(1f, 45f))));

            WorkbenchRangeMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Workbench", "RangeMultiplier", 2f,
                new ConfigDescription(
                    "Multiplier applied to every crafting station's build/discover range. 1 = vanilla.",
                    new AcceptableValueRange<float>(0.5f, 5f))));

            WorkbenchIgnoreRoofRequirement = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Workbench", "IgnoreRoofRequirement", true,
                "If true, crafting stations that normally require a roof (forge, workbench, etc.) can be used without one."));

            ProductionSpeedMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Production Buildings", "SpeedMultiplier", 1f,
                new ConfigDescription(
                    "Multiplier applied to processing time for smelters/kilns/blast furnaces, fermenters, beehives, and cooking stations. Values below 1 speed production up (e.g. 0.5 = twice as fast).",
                    new AcceptableValueRange<float>(0.1f, 3f))));

            ProductionCapacityMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Production Buildings", "CapacityMultiplier", 1f,
                new ConfigDescription(
                    "Multiplier applied to ore/fuel/honey capacity for smelters/kilns/blast furnaces and beehives. 1 = vanilla.",
                    new AcceptableValueRange<float>(0.5f, 5f))));

            FireplaceInfiniteFuel = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Fire Sources", "InfiniteFuel", true,
                "If true, fireplaces/hearths never consume fuel at all (Fireplace.m_infiniteFuel) -- no chests needed nearby. Matches Valheim Plus's [FireSource] torches/fires toggle. Takes priority over AutoRefuelEnabled below: FireplaceAutoRefuel's own check already no-ops once a fireplace has infinite fuel."));

            FireplaceAutoRefuelEnabled = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Fire Sources", "AutoRefuelEnabled", true,
                "If true, fireplaces/hearths automatically pull fuel from nearby containers instead of running out. Only matters when InfiniteFuel above is false."));

            FireplaceAutoRefuelRange = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Fire Sources", "AutoRefuelRange", 4f,
                new ConfigDescription(
                    "Radius (meters) around a fireplace to search for containers with fuel, when AutoRefuelEnabled is true.",
                    new AcceptableValueRange<float>(1f, 15f))));

            FireplaceAutoRefuelInterval = _configSync.AddConfigEntry(Config.Bind(
                "Crafting - Fire Sources", "AutoRefuelIntervalSeconds", 10f,
                new ConfigDescription(
                    "How often (seconds) each fireplace checks for fuel to auto-pull, when AutoRefuelEnabled is true.",
                    new AcceptableValueRange<float>(1f, 60f))));

            BuildingIgnoreSupport = _configSync.AddConfigEntry(Config.Bind(
                "Building - Structural Integrity", "IgnoreSupportRequirement", false,
                "If true, pieces no longer take damage/collapse from lacking structural support."));

            BuildingIgnoreWeatherDecay = _configSync.AddConfigEntry(Config.Bind(
                "Building - Structural Integrity", "IgnoreWeatherDecay", false,
                "If true, unroofed wood pieces no longer decay from weather exposure."));

            StackSizeMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Items", "StackSizeMultiplier", 3f,
                new ConfigDescription(
                    "Multiplier applied to every stackable item's max stack size. 1 = vanilla. Only affects items with a stack size greater than 1.",
                    new AcceptableValueRange<float>(1f, 10f))));

            GatheringYieldMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Player - Gathering", "YieldMultiplier", 1f,
                new ConfigDescription(
                    "Multiplier applied to resource drop counts from trees, ore rocks, and hand-pickable resources (berries, mushrooms, flint, etc). 1 = vanilla.",
                    new AcceptableValueRange<float>(0.5f, 5f))));

            AllowOreThroughPortals = _configSync.AddConfigEntry(Config.Bind(
                "Portals", "AllowOreThroughPortals", false,
                "If true, ore/metal and other items vanilla normally blocks from portal travel (ItemData.m_teleportable == false) can be teleported like anything else. Off by default: this is a real pacing/balance change (forces boat runs), not just a convenience — a deliberate group decision, set on the server."));

            InventoryFillFirstSlot = _configSync.AddConfigEntry(Config.Bind(
                "Inventory", "FillFirstSlotFirst", false,
                "If true, all picked-up items fill the first available inventory slot instead of vanilla's per-item-type placement rules."));

            TamedPetsCantDie = _configSync.AddConfigEntry(Config.Bind(
                "Creatures - Tamed Pets", "CantDie", false,
                "If true, tamed creatures (boars, wolves, lox) can never be reduced below 1 HP by combat damage."));

            WagonBaseMassMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Wagon", "BaseMassMultiplier", 1f,
                new ConfigDescription(
                    "Multiplier on a cart's base mass (affects how much stamina/force is needed to pull it when empty). 1 = vanilla.",
                    new AcceptableValueRange<float>(0.1f, 3f))));

            WagonItemWeightMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Wagon", "ItemWeightMassMultiplier", 1f,
                new ConfigDescription(
                    "Multiplier on how much a cart's cargo weight contributes to its pulling difficulty. Lower = cargo weighs the cart down less. 1 = vanilla.",
                    new AcceptableValueRange<float>(0.1f, 3f))));

            DifficultyDamageScalePerPlayer = _configSync.AddConfigEntry(Config.Bind(
                "Server - Game Difficulty", "DamageScalePerExtraPlayer", 0.04f,
                new ConfigDescription(
                    "How much extra damage enemies deal per nearby player above 1. Vanilla default is 0.04 (4%).",
                    new AcceptableValueRange<float>(0f, 0.5f))));

            DifficultyHealthScalePerPlayer = _configSync.AddConfigEntry(Config.Bind(
                "Server - Game Difficulty", "HealthScalePerExtraPlayer", 0.3f,
                new ConfigDescription(
                    "How much extra health enemies get per nearby player above 1. Vanilla default is 0.3 (30%).",
                    new AcceptableValueRange<float>(0f, 2f))));

            DifficultyMaxScalingPlayers = _configSync.AddConfigEntry(Config.Bind(
                "Server - Game Difficulty", "MaxScalingPlayers", 5,
                new ConfigDescription(
                    "Number of nearby players beyond which difficulty scaling stops increasing. Vanilla default is 5.",
                    new AcceptableValueRange<int>(1, 20))));

            MapExploreRadiusMultiplier = _configSync.AddConfigEntry(Config.Bind(
                "Map", "ExploreRadiusMultiplier", 2f,
                new ConfigDescription(
                    "Multiplier on how large an area of the map gets revealed as you walk around. Vanilla base radius is 100. 1 = vanilla.",
                    new AcceptableValueRange<float>(1f, 5f))));

            DisableScreenShake = _configSync.AddConfigEntry(Config.Bind(
                "Player - Camera", "DisableScreenShake", true,
                "If true, forces camera shake off (hits, explosions, etc.) regardless of each player's own in-game Settings > Game > Camera Shake preference. Vanilla already exposes a per-client toggle for this; this setting makes it consistent for everyone connected."));

            ServerCharacterStorageEnabled = _configSync.AddConfigEntry(Config.Bind(
                "Server - Character Storage", "Enabled", false,
                "If true, characters are stored server-side (keyed by SteamID) instead of trusting each client's local %appdata% file -- replaces the old ServerCharacters mod, built from scratch (see modpack.yaml for why). Defaults to OFF: this touches real save data, verify a connect/save/reconnect round-trip actually works before relying on it. Safe either way -- the local .fch save always happens first and is never skipped, so a network failure never loses local progress."));

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
