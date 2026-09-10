using HarmonyLib;

namespace ValheimQoL.Patches
{
    [HarmonyPatch(typeof(GameCamera), "Awake")]
    internal static class CameraPatch
    {
        private static void Postfix(GameCamera __instance)
        {
            __instance.m_fov = ValheimQoLPlugin.CameraFov.Value;
            __instance.m_maxDistance = ValheimQoLPlugin.CameraMaxZoom.Value;
        }
    }
}
