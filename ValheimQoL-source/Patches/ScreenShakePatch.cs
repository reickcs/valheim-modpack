using HarmonyLib;

namespace ValheimQoL.Patches
{
    // GameCamera.AddShake is the single entry point every CamShaker (hits,
    // explosions, etc.) calls into. Vanilla already has its own per-client
    // toggle for this (m_cameraShakeEnabled, read from PlatformPrefs
    // "CameraShake" in GameCamera.Awake), but that's a personal Settings
    // menu preference, not something a server can enforce. Skipping the
    // whole method here makes it consistent for everyone regardless of
    // their own Settings.
    [HarmonyPatch(typeof(GameCamera), "AddShake")]
    internal static class ScreenShakePatch
    {
        private static bool Prefix()
        {
            return !ValheimQoLPlugin.DisableScreenShake.Value;
        }
    }
}
