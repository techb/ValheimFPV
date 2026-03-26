using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace ValheimFPVDrone
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.fpvdrone.valheim";
        public const string PluginName = "Valheim FPV Drone";
        public const string PluginVersion = "0.3.3";

        public static ManualLogSource Log;
        public static Plugin Instance;
        private Harmony _harmony;

        // ── Keybinds ──
        public static ConfigEntry<KeyCode> ToggleDroneKey;
        public static ConfigEntry<KeyCode> ResetDroneKey;
        public static ConfigEntry<KeyCode> CalibrateKey;

        // ── Rates (degrees/sec at full stick) ──
        public static ConfigEntry<float> RollRate;
        public static ConfigEntry<float> PitchRate;
        public static ConfigEntry<float> YawRate;
        public static ConfigEntry<float> RollRCExpo;
        public static ConfigEntry<float> PitchRCExpo;
        public static ConfigEntry<float> YawRCExpo;
        public static ConfigEntry<float> RollRCRate;
        public static ConfigEntry<float> PitchRCRate;
        public static ConfigEntry<float> YawRCRate;

        // ── Physics ──
        public static ConfigEntry<float> Gravity;
        public static ConfigEntry<float> MaxThrust;
        public static ConfigEntry<float> Mass;
        public static ConfigEntry<float> DragCoefficient;
        public static ConfigEntry<float> AngularDragCoefficient;
        public static ConfigEntry<float> MotorSpinUpTime;
        public static ConfigEntry<bool> ObstacleCollision;

        // ── Speed / limits ──
        public static ConfigEntry<float> MaxSpeed;
        public static ConfigEntry<float> IdleThrottlePercent;

        // ── Camera ──
        public static ConfigEntry<float> CameraTiltAngle;
        public static ConfigEntry<float> CameraFOV;

        // ── Controller axis mapping ──
        public static ConfigEntry<int> ThrottleAxis;
        public static ConfigEntry<int> RollAxis;
        public static ConfigEntry<int> PitchAxis;
        public static ConfigEntry<int> YawAxis;
        public static ConfigEntry<bool> InvertThrottle;
        public static ConfigEntry<bool> InvertRoll;
        public static ConfigEntry<bool> InvertPitch;
        public static ConfigEntry<bool> InvertYaw;
        public static ConfigEntry<float> StickDeadzone;
        public static ConfigEntry<float> ThrottleDeadzone;
        public static ConfigEntry<bool> ThrottleCenterZero;
        public static ConfigEntry<float> ThrottleRangeMin;

        // ── Rendering ──
        public static ConfigEntry<bool> ShowHUD;
        public static ConfigEntry<bool> ShowCrosshair;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded!");
            Log.LogWarning("=============================================================");
            Log.LogWarning($"  {PluginName} — WORK IN PROGRESS");
            Log.LogWarning("  Expect bugs, crashes, and missing features.");
            Log.LogWarning("  Report issues at the project repo.");
            Log.LogInfo($"[FPVDrone] Config file: {Config.ConfigFilePath}");
            Log.LogInfo($"[FPVDrone] Loaded axis mapping — T=Axis{ThrottleAxis.Value}  R=Axis{RollAxis.Value}  P=Axis{PitchAxis.Value}  Y=Axis{YawAxis.Value}  ThrottleRangeMin={ThrottleRangeMin.Value}");
        }

        private void BindConfig()
        {
            // ── Keybinds ──
            ToggleDroneKey = Config.Bind("Keybinds", "ToggleDroneKey", KeyCode.F8,
                "Key to spawn/enter/exit the FPV drone.");
            ResetDroneKey = Config.Bind("Keybinds", "ResetDroneKey", KeyCode.F9,
                "Key to reset drone position to player.");
            CalibrateKey = Config.Bind("Keybinds", "CalibrateKey", KeyCode.F7,
                "Key to open/close the input monitor and calibration wizard.");

            // ── Betaflight-style Rates ──
            RollRate = Config.Bind("Rates", "RollRate", 0.5f,
                "Roll Rate (0.0–1.0). Boosts rotation speed at full stick deflection. Matches 'Rate' in Betaflight Configurator.");
            PitchRate = Config.Bind("Rates", "PitchRate", 0.5f,
                "Pitch Rate (0.0–1.0). Matches 'Rate' in Betaflight Configurator.");
            YawRate = Config.Bind("Rates", "YawRate", 0.5f,
                "Yaw Rate (0.0–1.0). Matches 'Rate' in Betaflight Configurator.");
            RollRCExpo = Config.Bind("Rates", "RollRCExpo", 0.0f,
                "RC Expo for roll (0.0–1.0). Softens center stick feel without affecting full deflection. Matches 'RC Expo' in Betaflight Configurator.");
            PitchRCExpo = Config.Bind("Rates", "PitchRCExpo", 0.0f,
                "RC Expo for pitch (0.0–1.0).");
            YawRCExpo = Config.Bind("Rates", "YawRCExpo", 0.0f,
                "RC Expo for yaw (0.0–1.0).");
            RollRCRate = Config.Bind("Rates", "RollRCRate", 1.7f,
                "Betaflight roll RC rate (center sensitivity multiplier).");
            PitchRCRate = Config.Bind("Rates", "PitchRCRate", 1.7f,
                "Betaflight pitch RC rate.");
            YawRCRate = Config.Bind("Rates", "YawRCRate", 1.7f,
                "Betaflight yaw RC rate.");

            // ── Physics ──
            Gravity = Config.Bind("Physics", "Gravity", 9.81f,
                "Gravity acceleration (m/s²). Valheim default ~9.81.");
            MaxThrust = Config.Bind("Physics", "MaxThrust", 75.0f,
                "Max thrust force in Newtons. Roughly 3.5x weight for a racing quad feel.");
            Mass = Config.Bind("Physics", "Mass", 0.99f,
                "Drone mass in kg. Typical 5\" racing quad is 0.6–0.9 kg.");
            DragCoefficient = Config.Bind("Physics", "DragCoefficient", 0.02f,
                "Quadratic drag coefficient. Lower = faster top speed and faster fall. 0.02 gives ~120-140 km/h equilibrium speed.");
            AngularDragCoefficient = Config.Bind("Physics", "AngularDragCoefficient", 8.0f,
                "Angular drag. Higher = snappier stop when releasing sticks.");
            MotorSpinUpTime = Config.Bind("Physics", "MotorSpinUpTime", 0.03f,
                "Time (sec) for motors to reach target RPM. Simulates motor latency.");
            ObstacleCollision = Config.Bind("Physics", "ObstacleCollision", true,
                "If true, drone collides with player-built structures. Ground collision is always active.");

            // ── Speed ──
            MaxSpeed = Config.Bind("Speed", "MaxSpeed", 100.0f,
                "Max drone speed in m/s hard cap. 80 = ~290 km/h. Physics drag determines actual equilibrium speed — this just prevents runaway values.");
            IdleThrottlePercent = Config.Bind("Speed", "IdleThrottlePercent", 5.0f,
                "Motor idle percentage (0–100). Small value keeps motors spinning at zero throttle.");

            // ── Camera ──
            CameraTiltAngle = Config.Bind("Camera", "CameraTiltAngle", 30.0f,
                "FPV camera uptilt in degrees. 25-45 typical for racing.");
            CameraFOV = Config.Bind("Camera", "CameraFOV", 110.0f,
                "FPV camera field of view.");

            // ── Controller Axes ──
            // RadioMaster radios in USB HID joystick mode typically:
            //   Axis 1 (index 0) = Aileron (Roll)
            //   Axis 2 (index 1) = Elevator (Pitch)
            //   Axis 3 (index 2) = Throttle
            //   Axis 4 (index 3) = Rudder (Yaw)
            // Unity axes are 1-indexed in Input.GetAxis("Joystick Axis N")
            // but 0-indexed for our config; we add 1 when reading.
            ThrottleAxis = Config.Bind("Controller", "ThrottleAxis", 2,
                "Joystick axis index for throttle (0-based). RadioMaster default: 2 (3rd axis).");
            RollAxis = Config.Bind("Controller", "RollAxis", 0,
                "Joystick axis index for roll / aileron (0-based). RadioMaster default: 0.");
            PitchAxis = Config.Bind("Controller", "PitchAxis", 1,
                "Joystick axis index for pitch / elevator (0-based). RadioMaster default: 1.");
            YawAxis = Config.Bind("Controller", "YawAxis", 3,
                "Joystick axis index for yaw / rudder (0-based). RadioMaster default: 3.");
            InvertThrottle = Config.Bind("Controller", "InvertThrottle", false,
                "Invert throttle axis.");
            InvertRoll = Config.Bind("Controller", "InvertRoll", false,
                "Invert roll axis.");
            InvertPitch = Config.Bind("Controller", "InvertPitch", false,
                "Invert pitch axis.");
            InvertYaw = Config.Bind("Controller", "InvertYaw", false,
                "Invert yaw axis.");
            StickDeadzone = Config.Bind("Controller", "StickDeadzone", 0.02f,
                "Deadzone for roll/pitch/yaw sticks (0.0–0.2).");
            ThrottleDeadzone = Config.Bind("Controller", "ThrottleDeadzone", 0.02f,
                "Deadzone for throttle stick.");
            ThrottleCenterZero = Config.Bind("Controller", "ThrottleCenterZero", false,
                "If true, throttle center = 0 thrust (spring-loaded stick). If false, bottom = 0 (no spring).");
            ThrottleRangeMin = Config.Bind("Controller", "ThrottleRangeMin", 0.0f,
                "Raw throttle floor (-1 = full-range axis, 0 = half-range axis). " +
                "Set to 0 if your throttle reads ~0.5 at idle; set to -1 if it reads ~0.0. " +
                "The calibration wizard auto-detects this.");

            // ── HUD ──
            ShowHUD = Config.Bind("HUD", "ShowHUD", true,
                "Show OSD-style HUD while flying.");
            ShowCrosshair = Config.Bind("HUD", "ShowCrosshair", true,
                "Show center crosshair.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
