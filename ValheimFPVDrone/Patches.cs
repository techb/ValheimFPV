using HarmonyLib;
using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Detects whether the BepInEx Configuration Manager window is open.
    /// Uses reflection so the mod compiles without a hard reference to ConfigurationManager.
    /// </summary>
    internal static class ConfigManagerUtil
    {
        private static System.Reflection.PropertyInfo _prop;
        private static BepInEx.BaseUnityPlugin _instance;
        private static bool _searched;

        public static bool IsOpen()
        {
            if (!_searched)
            {
                _searched = true;
                BepInEx.Bootstrap.Chainloader.PluginInfos
                    .TryGetValue("com.bepis.bepinex.configurationmanager", out var info);
                _instance = info?.Instance;
                _prop = _instance?.GetType().GetProperty("DisplayingWindow");
            }
            if (_instance == null || _prop == null) return false;
            return (bool)_prop.GetValue(_instance);
        }

        /// <summary>Returns true whenever player input should be suppressed.</summary>
        public static bool ShouldBlockInput()
        {
            return (DroneController.Instance != null && DroneController.Instance.IsFlying)
                || IsOpen()
                || (DroneCalibration.Instance != null && DroneCalibration.Instance.IsOpen);
        }
    }

    /// <summary>
    /// Harmony patches to:
    /// 1. Bootstrap the DroneController onto the game
    /// 2. Block player input while flying the drone
    /// 3. Prevent game actions (attack, interact, etc.) during drone mode
    /// 4. Block player input while the BepInEx Configuration Manager is open
    /// </summary>

    // ── Bootstrap: attach DroneController and DroneCalibration when the game starts ──
    [HarmonyPatch(typeof(FejdStartup), "Awake")]
    public static class FejdStartup_Awake_Patch
    {
        static void Postfix(FejdStartup __instance)
        {
            GameObject controllerObj = new GameObject("FPVDroneController");
            controllerObj.AddComponent<DroneController>();
            controllerObj.AddComponent<DroneCalibration>();
            Object.DontDestroyOnLoad(controllerObj);

            Plugin.Log.LogInfo("DroneController initialized.");
        }
    }

    // ── Block player movement while flying / config menu / calibration open ──
    [HarmonyPatch(typeof(Player), "Update")]
    public static class Player_Update_Patch
    {
        static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !ConfigManagerUtil.ShouldBlockInput()) return;
            try
            {
                var body = __instance.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    public static class Player_PlayerAttackInput_Patch
    {
        static bool Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return true;
            return !ConfigManagerUtil.ShouldBlockInput();
        }
    }

    [HarmonyPatch(typeof(Player), "UseHotbarItem")]
    public static class Player_UseHotbarItem_Patch
    {
        static bool Prefix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return true;
            return !ConfigManagerUtil.ShouldBlockInput();
        }
    }

    [HarmonyPatch(typeof(Character), "Jump")]
    public static class Character_Jump_Patch
    {
        static bool Prefix(Character __instance)
        {
            Player player = __instance as Player;
            if (player == null || player != Player.m_localPlayer) return true;
            return !ConfigManagerUtil.ShouldBlockInput();
        }
    }

    // ── Block game pause menu interfering with drone controls ──
    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    public static class GameCamera_LateUpdate_Patch
    {
        static bool Prefix()
        {
            // When flying, skip the game camera update entirely
            if (DroneController.Instance != null && DroneController.Instance.IsFlying)
            {
                return false;
            }
            return true;
        }
    }

    // ── Prevent the minimap from capturing mouse input ──
    [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
    public static class Minimap_OnMapLeftClick_Patch
    {
        static bool Prefix()
        {
            if (DroneController.Instance != null && DroneController.Instance.IsFlying)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Minimap), "OnMapRightClick")]
    public static class Minimap_OnMapRightClick_Patch
    {
        static bool Prefix()
        {
            if (DroneController.Instance != null && DroneController.Instance.IsFlying)
                return false;
            return true;
        }
    }

    // ── Hide the HUD elements that don't apply during drone flight ──
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    public static class Hud_UpdateCrosshair_Patch
    {
        static bool Prefix()
        {
            if (DroneController.Instance != null && DroneController.Instance.IsFlying)
                return false;
            return true;
        }
    }
}
