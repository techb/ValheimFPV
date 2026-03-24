using System.Collections.Generic;
using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Real-time axis monitor and calibration wizard for USB RC transmitters.
    ///
    /// Press CalibrateKey (default F7) to open the monitor, which shows all six
    /// WinMM joystick axes as live bidirectional bars. From there, press ENTER to
    /// launch the calibration wizard, which walks through each flight function
    /// (Throttle, Roll, Pitch, Yaw) and auto-detects which physical axis to assign
    /// by watching for the greatest stick movement. Results are saved to the
    /// BepInEx config file on confirmation.
    ///
    /// During calibration the player character is frozen (same as drone mode).
    /// </summary>
    public class DroneCalibration : MonoBehaviour
    {
        public static DroneCalibration Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private enum WizardStep { None, Throttle, Roll, Pitch, Yaw, Done }
        private WizardStep _step = WizardStep.None;

        // Per-axis min/max observed during the current wizard step
        private readonly float[] _rangeMin = new float[6];
        private readonly float[] _rangeMax = new float[6];

        // Results detected by the wizard (axis index 0-5)
        private int _wThrottle, _wRoll, _wPitch, _wYaw;

        // Live axis values refreshed in OnGUI
        private readonly float[] _axes = new float[6];

        // GUI resources
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Texture2D _bgTex, _whiteTex;
        private GUIStyle _styleTitle, _styleBody, _styleSmall, _styleWarn;
        private bool _guiInit;

        private static readonly string[] AxisNames =
            { "X  (0)", "Y  (1)", "Z  (2)", "Rx (3)", "U  (4)", "V  (5)" };

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Toggle monitor open/close
            if (Input.GetKeyDown(Plugin.CalibrateKey.Value))
            {
                if (IsOpen && _step == WizardStep.None)
                    IsOpen = false;
                else if (!IsOpen)
                {
                    IsOpen = true;
                    _step = WizardStep.None;
                }
            }

            if (!IsOpen) return;

            // Wizard: track axis ranges every frame while a step is active
            if (_step != WizardStep.None && _step != WizardStep.Done)
            {
                for (int i = 0; i < 6; i++)
                {
                    float v = DroneInput.GetRawAxisWinMM(i);
                    if (v < _rangeMin[i]) _rangeMin[i] = v;
                    if (v > _rangeMax[i]) _rangeMax[i] = v;
                }
            }

            if (_step == WizardStep.None)
            {
                // Monitor: ENTER starts wizard
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    BeginWizardStep(WizardStep.Throttle);
            }
            else if (_step == WizardStep.Done)
            {
                // Done: ENTER or SPACE saves, ESC discards
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                    || Input.GetKeyDown(KeyCode.Space))
                    CommitCalibration();

                if (Input.GetKeyDown(KeyCode.Escape))
                    _step = WizardStep.None;
            }
            else
            {
                // Active step: SPACE confirms, ESC cancels wizard
                if (Input.GetKeyDown(KeyCode.Space))
                    AdvanceWizard();

                if (Input.GetKeyDown(KeyCode.Escape))
                    _step = WizardStep.None;
            }
        }

        // ── Wizard logic ──────────────────────────────────────────────────────────

        private void BeginWizardStep(WizardStep step)
        {
            _step = step;
            // Seed min/max with current values so any movement registers as range
            for (int i = 0; i < 6; i++)
            {
                float cur = DroneInput.GetRawAxisWinMM(i);
                _rangeMin[i] = cur;
                _rangeMax[i] = cur;
            }
        }

        private void AdvanceWizard()
        {
            int best = FindBestAxis();
            switch (_step)
            {
                case WizardStep.Throttle: _wThrottle = best; BeginWizardStep(WizardStep.Roll);  break;
                case WizardStep.Roll:     _wRoll     = best; BeginWizardStep(WizardStep.Pitch); break;
                case WizardStep.Pitch:    _wPitch    = best; BeginWizardStep(WizardStep.Yaw);   break;
                case WizardStep.Yaw:      _wYaw      = best; _step = WizardStep.Done;           break;
            }
        }

        private int FindBestAxis()
        {
            int best = 0;
            float bestRange = 0f;
            for (int i = 0; i < 6; i++)
            {
                float r = _rangeMax[i] - _rangeMin[i];
                if (r > bestRange) { bestRange = r; best = i; }
            }
            return best;
        }

        private void CommitCalibration()
        {
            Plugin.ThrottleAxis.Value = _wThrottle;
            Plugin.RollAxis.Value     = _wRoll;
            Plugin.PitchAxis.Value    = _wPitch;
            Plugin.YawAxis.Value      = _wYaw;

            // Auto-detect throttle range: if the stick never went below -0.5 during
            // the throttle step, it's a half-range axis (RadioMaster USB HID style).
            float observedMin = _rangeMin[_wThrottle];
            Plugin.ThrottleRangeMin.Value = observedMin < -0.5f ? -1f : 0f;

            Plugin.Instance.Config.Save();
            Plugin.Log.LogInfo(
                $"[FPVDrone] Calibration saved — T=Axis{_wThrottle}  R=Axis{_wRoll}" +
                $"  P=Axis{_wPitch}  Y=Axis{_wYaw}  ThrottleRangeMin={Plugin.ThrottleRangeMin.Value}");
            _step = WizardStep.None;
        }

        // ── GUI ───────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;

            InitGUI();

            for (int i = 0; i < 6; i++)
                _axes[i] = DroneInput.GetRawAxisWinMM(i);

            const float PW = 500f;
            float px = (Screen.width - PW) * 0.5f;
            float py = 50f;

            switch (_step)
            {
                case WizardStep.None: DrawMonitor(px, py, PW); break;
                case WizardStep.Done: DrawDone(px, py, PW);    break;
                default:              DrawWizard(px, py, PW);  break;
            }
        }

        private void DrawMonitor(float px, float py, float pw)
        {
            const float pad = 10f, rowH = 26f, lineH = 22f;
            bool showAxisWarn = DroneInput.WinMMAvailable && !DroneInput.JoystickHasRAxis;
            float ph = pad + 26 + 6 + 6 * rowH + 6 + lineH + lineH + 10 + (showAxisWarn ? lineH + 4 : 0) + lineH + pad;

            DrawBg(px, py, pw, ph);
            float cy = py + pad;

            DrawLabel(px + pad, cy, pw - pad * 2, 26, "FPV DRONE  —  INPUT MONITOR", _styleTitle);
            cy += 32;

            for (int i = 0; i < 6; i++)
            {
                DrawAxisBar(px + pad, cy, pw - pad * 2, 18, AxisNames[i], _axes[i], AssignTag(i));
                cy += rowH;
            }
            cy += 6;

            string connText = DroneInput.WinMMAvailable
                ? "Controller: " + DroneInput.ControllerName
                : "No WinMM joystick detected — is the radio in USB Joystick mode?";
            DrawLabel(px + pad, cy, pw - pad * 2, lineH, connText,
                DroneInput.WinMMAvailable ? _styleSmall : _styleWarn);
            cy += lineH;

            DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                "Mapped:  T=Axis" + Plugin.ThrottleAxis.Value +
                "  R=Axis" + Plugin.RollAxis.Value +
                "  P=Axis" + Plugin.PitchAxis.Value +
                "  Y=Axis" + Plugin.YawAxis.Value,
                _styleSmall);
            cy += lineH + 10;

            if (showAxisWarn)
            {
                DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                    "! Yaw/R axis not exposed by WinMM — in EdgeTX set CH4 to HID Rx (not Rz). " +
                    "Axes=" + DroneInput.JoystickAxesCount +
                    " hasU=" + DroneInput.JoystickHasUAxis +
                    " hasV=" + DroneInput.JoystickHasVAxis,
                    _styleWarn);
                cy += lineH + 4;
            }

            DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                "[ENTER] Calibration wizard     [" + Plugin.CalibrateKey.Value + "] Close",
                _styleWarn);
        }

        private void DrawWizard(float px, float py, float pw)
        {
            const float pad = 10f, rowH = 26f, lineH = 22f;
            float ph = pad + 26 + 6 + lineH + 6 + 6 * rowH + 8 + lineH + pad;

            DrawBg(px, py, pw, ph);
            float cy = py + pad;

            DrawLabel(px + pad, cy, pw - pad * 2, 26, GetWizardTitle(), _styleTitle);
            cy += 32;

            DrawLabel(px + pad, cy, pw - pad * 2, lineH, GetWizardPrompt(), _styleWarn);
            cy += lineH + 6;

            int best = FindBestAxis();
            for (int i = 0; i < 6; i++)
            {
                float range = _rangeMax[i] - _rangeMin[i];
                string label = AxisNames[i] + (range > 0.02f ? "  rng " + range.ToString("F2") : "");
                string tag = (i == best && range > 0.05f) ? "BEST" : "";
                DrawAxisBar(px + pad, cy, pw - pad * 2, 18, label, _axes[i], tag);
                cy += rowH;
            }
            cy += 8;

            DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                "[SPACE] Confirm this axis     [ESC] Cancel wizard", _styleSmall);
        }

        private void DrawDone(float px, float py, float pw)
        {
            const float pad = 10f, rowH = 24f, lineH = 22f;
            float ph = pad + 26 + 8 + 5 * rowH + 8 + lineH + lineH + pad;

            DrawBg(px, py, pw, ph);
            float cy = py + pad;

            DrawLabel(px + pad, cy, pw - pad * 2, 26, "CALIBRATION COMPLETE", _styleTitle);
            cy += 34;

            float observedMin = _rangeMin[_wThrottle];
            string tRangeLabel = observedMin < -0.5f ? "full-range" : "half-range";
            DrawLabel(px + pad, cy, pw - pad * 2, rowH, "Throttle  ->  Axis " + _wThrottle + "  (" + tRangeLabel + ")", _styleBody); cy += rowH;
            DrawLabel(px + pad, cy, pw - pad * 2, rowH, "Roll      ->  Axis " + _wRoll,     _styleBody); cy += rowH;
            DrawLabel(px + pad, cy, pw - pad * 2, rowH, "Pitch     ->  Axis " + _wPitch,    _styleBody); cy += rowH;
            DrawLabel(px + pad, cy, pw - pad * 2, rowH, "Yaw       ->  Axis " + _wYaw,      _styleBody); cy += rowH;
            DrawLabel(px + pad, cy, pw - pad * 2, rowH,
                "ThrottleRangeMin  ->  " + (observedMin < -0.5f ? "-1  (stick goes to -1)" : "0  (stick floor is 0)"),
                _styleSmall); cy += rowH + 8;

            DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                "[ENTER / SPACE] Save & apply     [ESC] Discard", _styleWarn);
            cy += lineH;
            DrawLabel(px + pad, cy, pw - pad * 2, lineH,
                "Tip: if a stick is reversed, toggle its Invert setting in the config (F1).",
                _styleSmall);
        }

        // ── Drawing helpers ───────────────────────────────────────────────────────

        private void DrawAxisBar(float bx, float by, float bw, float bh,
                                  string label, float value, string tag)
        {
            const float labelW = 140f;
            const float valW   = 50f;
            float barW = bw - labelW - valW - 4f;

            bool hasTag = !string.IsNullOrEmpty(tag);

            // Label
            GUI.Label(new Rect(bx, by, labelW, bh + 6),
                hasTag ? "[" + tag + "] " + label : label,
                hasTag ? _styleWarn : _styleSmall);

            float barX = bx + labelW;
            value = Mathf.Clamp(value, -1f, 1f);

            // Bar background
            Color orig = GUI.color;
            GUI.color = new Color(0.14f, 0.14f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(barX, by + 2, barW, bh - 2), _whiteTex);

            // Bidirectional fill from center
            float cx = barX + barW * 0.5f;
            GUI.color = hasTag
                ? new Color(0.95f, 0.80f, 0.15f, 1f)
                : new Color(0.25f, 0.90f, 0.35f, 1f);
            float half = Mathf.Abs(value) * barW * 0.5f;
            if (half > 0.5f)
            {
                float fx = value >= 0f ? cx : cx - half;
                GUI.DrawTexture(new Rect(fx, by + 2, half, bh - 2), _whiteTex);
            }

            // Center tick
            GUI.color = new Color(1f, 1f, 1f, 0.30f);
            GUI.DrawTexture(new Rect(cx - 0.5f, by, 1f, bh + 2), _whiteTex);

            GUI.color = orig;

            // Numeric value
            GUI.Label(new Rect(barX + barW + 4f, by, valW, bh + 6), value.ToString("F2"), _styleSmall);
        }

        private void DrawBg(float x, float y, float w, float h)
        {
            Color orig = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, w, h), _bgTex);
            GUI.color = orig;
        }

        private static void DrawLabel(float x, float y, float w, float h, string text, GUIStyle style)
        {
            GUI.Label(new Rect(x, y, w, h), text, style);
        }

        private string AssignTag(int axisIndex)
        {
            string tag = "";
            if (Plugin.ThrottleAxis.Value == axisIndex) tag += "T";
            if (Plugin.RollAxis.Value     == axisIndex) tag += "R";
            if (Plugin.PitchAxis.Value    == axisIndex) tag += "P";
            if (Plugin.YawAxis.Value      == axisIndex) tag += "Y";
            return tag;
        }

        private string GetWizardTitle()
        {
            switch (_step)
            {
                case WizardStep.Throttle: return "WIZARD  —  Step 1/4: THROTTLE";
                case WizardStep.Roll:     return "WIZARD  —  Step 2/4: ROLL";
                case WizardStep.Pitch:    return "WIZARD  —  Step 3/4: PITCH";
                case WizardStep.Yaw:      return "WIZARD  —  Step 4/4: YAW";
                default:                  return "WIZARD";
            }
        }

        private string GetWizardPrompt()
        {
            switch (_step)
            {
                case WizardStep.Throttle: return "Move THROTTLE stick fully up & down, then press SPACE";
                case WizardStep.Roll:     return "Move ROLL stick fully left & right, then press SPACE";
                case WizardStep.Pitch:    return "Move PITCH stick fully up & down, then press SPACE";
                case WizardStep.Yaw:      return "Move YAW stick fully left & right, then press SPACE";
                default:                  return "";
            }
        }

        // ── GUI initialisation ────────────────────────────────────────────────────

        private void InitGUI()
        {
            if (_guiInit) return;
            _guiInit = true;

            _bgTex    = MakeTex(new Color(0.04f, 0.05f, 0.10f, 0.93f));
            _whiteTex = MakeTex(Color.white);

            Texture2D titleBg = MakeTex(new Color(0.10f, 0.10f, 0.28f, 1f));

            _styleTitle = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold };
            _styleTitle.normal.textColor     = Color.white;
            _styleTitle.normal.background    = titleBg;
            _styleTitle.padding              = new RectOffset(6, 6, 3, 3);

            _styleBody = new GUIStyle { fontSize = 14 };
            _styleBody.normal.textColor = new Color(0.92f, 0.92f, 0.92f, 1f);

            _styleSmall = new GUIStyle { fontSize = 12 };
            _styleSmall.normal.textColor = new Color(0.62f, 0.62f, 0.67f, 1f);

            _styleWarn = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold };
            _styleWarn.normal.textColor = new Color(1.00f, 0.88f, 0.20f, 1f);
        }

        private Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private void OnDestroy()
        {
            foreach (var t in _textures)
                if (t) Destroy(t);
        }
    }
}
