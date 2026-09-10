using System.IO;
using AzuCraftyBoxes.Util;

namespace AzuCraftyBoxes.Patches;

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class ObjectDBAwakePatch
{
    [HarmonyPriority(Priority.VeryHigh)]
    static void Postfix(ObjectDB __instance)
    {
        if (!__instance.m_StatusEffects.Contains(SE_ContainerPull.SE_ContainerPulling))
        {
            __instance.m_StatusEffects.Add(SE_ContainerPull.SE_ContainerPulling);
        }

        __instance.UpdateRegisters();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.SetLocalPlayer))]
static class PlayerSetLocalPlayerPatch
{
    static void Postfix(Player __instance)
    {
        SE_ContainerPull.CheckAndSetStatusEffect(__instance);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
static class PlayerOnSpawnedPatch
{
    static void Postfix(Player __instance, bool spawnValkyrie)
    {
        SE_ContainerPull.CheckAndSetStatusEffect(__instance);
    }
}

// After a death and respawn, also re‑apply the saved state.
[HarmonyPatch(typeof(Player), nameof(Player.OnRespawn))]
static class PlayerOnRespawnPatch
{
    static void Postfix(Player __instance)
    {
        SE_ContainerPull.CheckAndSetStatusEffect(__instance);
    }
}

public class SE_ContainerPull
{
    public static readonly int s_statusEffectPreventPulling = "Pull from containers".GetStableHashCode();
    public static StatusEffect SE_ContainerPulling = null!;

    public static void CreateEffect()
    {
        SE_ContainerPulling = ScriptableObject.CreateInstance<StatusEffect>();
        SE_ContainerPulling.name = "PreventPulling";
        SE_ContainerPulling.m_name = "Preventing Pulling";
        SE_ContainerPulling.m_icon = LoadSprite("pullingicon.png");
        SE_ContainerPulling.m_tooltip = "Prevents pulling from nearby containers & backpacks";
        SE_ContainerPulling.m_startMessageType = MessageHud.MessageType.TopLeft;
        SE_ContainerPulling.m_startMessage = "";
        SE_ContainerPulling.m_stopMessageType = MessageHud.MessageType.TopLeft;
        SE_ContainerPulling.m_stopMessage = "";
    }

    private static byte[] ReadEmbeddedFileBytes(string name)
    {
        using MemoryStream stream = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
        return stream.ToArray();
    }

    // Texture2D.LoadImage lives in UnityEngine.ImageConversionModule, which in
    // this Valheim build is compiled against netstandard 2.1 -- a version
    // conflict against net472's implicit netstandard 2.0 facade that isn't
    // worth chasing for one decorative status-effect icon. The status effect
    // itself (preventing container pulling) still works with no icon; a
    // status effect with a null m_icon just doesn't render one.
    private static Sprite LoadSprite(string name)
    {
        return null!;
    }

    public static void CheckAndSetStatusEffect(Player instance = null)
    {
        if (instance == Player.m_localPlayer)
            instance.ApplyPullingStatusEffect();
    }
}