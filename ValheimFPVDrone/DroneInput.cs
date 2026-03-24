using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Reads raw joystick axes from any USB HID controller (RadioMaster, FrSky, etc.)
    /// and applies deadzone + inversion. Provides normalized stick values for the
    /// drone physics controller.
    ///
    /// Axis reading strategy:
    ///   1. Try Unity's legacy Input Manager named axes ("Horizontal", "Vertical", etc.)
    ///   2. If the name isn't registered in Valheim's InputManager.asset (throws), fall
    ///      back to WinMM joyGetPosEx which reads raw HID joystick data directly.
    ///      This is why left-stick axes (throttle=Z, yaw=RX) work even though Valheim
    ///      doesn't define "Joy1 Axis 3" / "Joy1 Axis 4" in its Input Manager.
    /// </summary>
    public static class DroneInput
    {
        // ── WinMM P/Invoke ──────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public uint dwSize, dwFlags;
            public uint dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV;
            public uint dwReserved1, dwReserved2;
        }

        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct JOYCAPS
        {
            public ushort wMid, wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax;
            public uint wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax;
            public uint wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        private static extern int joyGetDevCaps(uint uJoyID, ref JOYCAPS pjc, uint cbjc);

        private const uint JOY_RETURNALL = 0xFF;
        private const int JOYERR_NOERROR = 0;
        private const float JOY_AXIS_RANGE = 65535f;
        private const uint JOYCAPS_HASR = 0x0002;
        private const uint JOYCAPS_HASU = 0x0004;
        private const uint JOYCAPS_HASV = 0x0008;

        // Cached WinMM joystick ID (uint.MaxValue = not found)
        private static uint _winmmJoyId = uint.MaxValue;
        // Whether we've searched for a WinMM joystick in the current poll cycle
        private static bool _winmmSearched = false;
        // Axis indices whose Unity Input Manager name threw — permanently use WinMM for these
        private static readonly HashSet<int> _unityAxisFailed = new HashSet<int>();
        // Cached device capabilities
        private static uint _joyCaps = 0;
        private static uint _joyNumAxes = 0;
        // ────────────────────────────────────────────────────────────────────────────

        /// <summary>True once a WinMM joystick has been found.</summary>
        public static bool WinMMAvailable => _winmmJoyId != uint.MaxValue;
        /// <summary>Number of axes reported by joyGetDevCaps (0 if not queried).</summary>
        public static uint JoystickAxesCount => _joyNumAxes;
        /// <summary>True if the device reports an R axis (HID Rx = typical yaw/rudder).</summary>
        public static bool JoystickHasRAxis => (_joyCaps & JOYCAPS_HASR) != 0;
        /// <summary>True if the device reports a U axis (HID Ry).</summary>
        public static bool JoystickHasUAxis => (_joyCaps & JOYCAPS_HASU) != 0;
        /// <summary>True if the device reports a V axis (HID Rz).</summary>
        public static bool JoystickHasVAxis => (_joyCaps & JOYCAPS_HASV) != 0;

        /// <summary>Read a WinMM joystick axis directly, bypassing Unity's Input Manager.
        /// Returns [-1, 1]. Safe to call at any time; returns 0 if no joystick found.</summary>
        public static float GetRawAxisWinMM(int axisIndex) => ReadAxisWinMM(axisIndex);

        /// <summary>Throttle: 0.0 (idle) to 1.0 (full), or -1 to 1 if center-zero mode.</summary>
        public static float Throttle { get; private set; }

        /// <summary>Roll: -1.0 (left) to 1.0 (right).</summary>
        public static float Roll { get; private set; }

        /// <summary>Pitch: -1.0 (nose down) to 1.0 (nose up).</summary>
        public static float Pitch { get; private set; }

        /// <summary>Yaw: -1.0 (left) to 1.0 (right).</summary>
        public static float Yaw { get; private set; }

        /// <summary>True if any joystick is detected.</summary>
        public static bool ControllerConnected { get; private set; }

        /// <summary>Detected controller name.</summary>
        public static string ControllerName { get; private set; } = "None";

        private static float _pollTimer = 0f;
        private const float POLL_INTERVAL = 2.0f;

        public static void Update()
        {
            DetectController();

            float rawThrottle = ReadAxis(Plugin.ThrottleAxis.Value);
            float rawRoll = ReadAxis(Plugin.RollAxis.Value);
            float rawPitch = ReadAxis(Plugin.PitchAxis.Value);
            float rawYaw = ReadAxis(Plugin.YawAxis.Value);

            // Apply inversions
            if (Plugin.InvertThrottle.Value) rawThrottle = -rawThrottle;
            if (Plugin.InvertRoll.Value) rawRoll = -rawRoll;
            if (Plugin.InvertPitch.Value) rawPitch = -rawPitch;
            if (Plugin.InvertYaw.Value) rawYaw = -rawYaw;

            // Apply deadzones
            Roll = ApplyDeadzone(rawRoll, Plugin.StickDeadzone.Value);
            Pitch = ApplyDeadzone(rawPitch, Plugin.StickDeadzone.Value);
            Yaw = ApplyDeadzone(rawYaw, Plugin.StickDeadzone.Value);

            // Throttle processing
            float deadzonedThrottle = ApplyDeadzone(rawThrottle, Plugin.ThrottleDeadzone.Value);

            if (Plugin.ThrottleCenterZero.Value)
            {
                // Spring-loaded throttle: center = 0, full up = 1, full down = -1
                Throttle = deadzonedThrottle;
            }
            else
            {
                // Non-spring throttle: remap [rangeMin, 1] → [0, 1].
                // rangeMin = -1 for radios that output the full [-1,1] range.
                // rangeMin =  0 for radios that output a half-range [0,1]
                //              (RadioMaster in USB HID mode typically does this).
                float tMin = Mathf.Clamp(Plugin.ThrottleRangeMin.Value, -1f, 0f);
                float tRange = 1f - tMin;
                Throttle = Mathf.Clamp01((deadzonedThrottle - tMin) / tRange);
            }

            // Keyboard fallback when no controller detected or for testing
            if (!ControllerConnected || IsKeyboardOverrideActive())
            {
                ApplyKeyboardFallback();
            }
        }

        private static void DetectController()
        {
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer < POLL_INTERVAL) return;
            _pollTimer = 0f;

            // Refresh WinMM joystick search each poll cycle so reconnects are detected
            _winmmSearched = false;
            EnsureWinMMJoystick();

            string[] joysticks = Input.GetJoystickNames();
            ControllerConnected = false;
            ControllerName = "None";

            if (joysticks != null)
            {
                for (int i = 0; i < joysticks.Length; i++)
                {
                    if (!string.IsNullOrEmpty(joysticks[i]))
                    {
                        ControllerConnected = true;
                        ControllerName = joysticks[i];
                        break;
                    }
                }
            }

            // If Unity didn't find it, check WinMM
            if (!ControllerConnected && _winmmJoyId != uint.MaxValue)
            {
                ControllerConnected = true;
                ControllerName = "USB Joystick (WinMM)";
            }
        }

        private static float ReadAxis(int axisIndex)
        {
            // Prefer WinMM when a joystick is connected — Unity's Input Manager only
            // defines "Horizontal" / "Vertical" for joystick axes in Valheim, so axes
            // 2+ silently return 0 from GetAxisRaw rather than throwing.
            if (WinMMAvailable)
                return ReadAxisWinMM(axisIndex);

            // WinMM not available: fall back to Unity Input Manager
            if (!_unityAxisFailed.Contains(axisIndex))
            {
                string axisName = GetAxisName(axisIndex);
                try
                {
                    return Input.GetAxisRaw(axisName);
                }
                catch
                {
                    _unityAxisFailed.Add(axisIndex);
                    Plugin.Log.LogInfo(
                        $"[FPVDrone] Axis {axisIndex} (\"{axisName}\") not in Input Manager — using WinMM.");
                }
            }
            return 0f;
        }

        // WinMM axis index → JOYINFOEX field mapping:
        //   0 = dwXpos  (HID X  = aileron/roll,  joystick axis 1)
        //   1 = dwYpos  (HID Y  = elevator/pitch, joystick axis 2)
        //   2 = dwZpos  (HID Z  = throttle,        joystick axis 3)
        //   3 = dwRpos  (HID RX = rudder/yaw,       joystick axis 4)
        //   4 = dwUpos  (HID RY,                    joystick axis 5)
        //   5 = dwVpos  (HID RZ,                    joystick axis 6)
        private static float ReadAxisWinMM(int axisIndex)
        {
            EnsureWinMMJoystick();
            if (_winmmJoyId == uint.MaxValue) return 0f;

            var info = new JOYINFOEX
            {
                dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX)),
                dwFlags = JOY_RETURNALL
            };

            if (joyGetPosEx(_winmmJoyId, ref info) != JOYERR_NOERROR) return 0f;

            uint raw;
            switch (axisIndex)
            {
                case 0: raw = info.dwXpos; break;
                case 1: raw = info.dwYpos; break;
                case 2: raw = info.dwZpos; break;
                case 3: raw = info.dwRpos; break;
                case 4: raw = info.dwUpos; break;
                case 5: raw = info.dwVpos; break;
                default: return 0f;
            }

            // WinMM reports [0, 65535] with 32767/32768 at center.
            // Normalize to [-1, 1].
            return (raw / JOY_AXIS_RANGE) * 2f - 1f;
        }

        private static void EnsureWinMMJoystick()
        {
            if (_winmmSearched) return;
            _winmmSearched = true;

            uint newId = uint.MaxValue;
            for (uint id = 0; id < 16; id++)
            {
                var probe = new JOYINFOEX
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX)),
                    dwFlags = JOY_RETURNALL
                };
                if (joyGetPosEx(id, ref probe) == JOYERR_NOERROR)
                {
                    newId = id;
                    break;
                }
            }

            if (newId != _winmmJoyId)
            {
                _winmmJoyId = newId;
                if (_winmmJoyId != uint.MaxValue)
                {
                    var caps = new JOYCAPS();
                    if (joyGetDevCaps(_winmmJoyId, ref caps, (uint)Marshal.SizeOf(typeof(JOYCAPS))) == JOYERR_NOERROR)
                    {
                        _joyCaps = caps.wCaps;
                        _joyNumAxes = caps.wNumAxes;
                        Plugin.Log.LogInfo(
                            $"[FPVDrone] WinMM joystick connected (ID {_winmmJoyId}): \"{caps.szPname}\"" +
                            $"  axes={caps.wNumAxes}  caps=0x{caps.wCaps:X4}" +
                            $"  hasR={(_joyCaps & JOYCAPS_HASR) != 0}" +
                            $"  hasU={(_joyCaps & JOYCAPS_HASU) != 0}" +
                            $"  hasV={(_joyCaps & JOYCAPS_HASV) != 0}");
                    }
                    else
                    {
                        _joyCaps = 0;
                        _joyNumAxes = 0;
                        Plugin.Log.LogInfo($"[FPVDrone] WinMM joystick connected (ID {_winmmJoyId}).");
                    }
                }
                else
                {
                    _joyCaps = 0;
                    _joyNumAxes = 0;
                    Plugin.Log.LogWarning("[FPVDrone] WinMM joystick disconnected.");
                }
            }
        }

        private static string GetAxisName(int axisIndex)
        {
            // Valheim's Input Manager defines "Horizontal" and "Vertical" mapped to
            // joystick axes 1 and 2.  Axes 3+ are typically not defined, so they fall
            // back to WinMM via ReadAxis above.
            switch (axisIndex)
            {
                case 0: return "Horizontal";
                case 1: return "Vertical";
                case 2: return "Joy1 Axis 3";
                case 3: return "Joy1 Axis 4";
                case 4: return "Joy1 Axis 5";
                case 5: return "Joy1 Axis 6";
                case 6: return "Joy1 Axis 7";
                case 7: return "Joy1 Axis 8";
                default: return $"Joy1 Axis {axisIndex + 1}";
            }
        }

        private static float ApplyDeadzone(float value, float deadzone)
        {
            if (Mathf.Abs(value) < deadzone) return 0f;
            // Rescale so that the edge of the deadzone maps to 0
            float sign = Mathf.Sign(value);
            return sign * ((Mathf.Abs(value) - deadzone) / (1f - deadzone));
        }

        private static bool IsKeyboardOverrideActive()
        {
            return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                   Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                   Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) ||
                   Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E);
        }

        private static void ApplyKeyboardFallback()
        {
            // WASD + QE + Space/Shift as keyboard fallback
            float kbRoll = 0f, kbPitch = 0f, kbYaw = 0f, kbThrottle = Throttle;

            if (Input.GetKey(KeyCode.A)) kbRoll = -1f;
            if (Input.GetKey(KeyCode.D)) kbRoll = 1f;
            if (Input.GetKey(KeyCode.W)) kbPitch = 1f;
            if (Input.GetKey(KeyCode.S)) kbPitch = -1f;
            if (Input.GetKey(KeyCode.Q)) kbYaw = -1f;
            if (Input.GetKey(KeyCode.E)) kbYaw = 1f;

            // Throttle: Space = increase, Shift = decrease (incremental)
            if (Input.GetKey(KeyCode.Space))
                kbThrottle = Mathf.Min(1f, Throttle + Time.unscaledDeltaTime * 1.5f);
            if (Input.GetKey(KeyCode.LeftShift))
                kbThrottle = Mathf.Max(0f, Throttle - Time.unscaledDeltaTime * 1.5f);

            // Blend: if keyboard is active, use keyboard values
            if (Mathf.Abs(kbRoll) > 0.01f) Roll = kbRoll;
            if (Mathf.Abs(kbPitch) > 0.01f) Pitch = kbPitch;
            if (Mathf.Abs(kbYaw) > 0.01f) Yaw = kbYaw;
            Throttle = kbThrottle;
        }
    }
}
