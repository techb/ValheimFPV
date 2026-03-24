using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Renders an OSD-style (On-Screen Display) HUD for the FPV drone.
    /// Displays flight telemetry similar to what you'd see in Betaflight OSD
    /// or DJI goggles overlay.
    /// </summary>
    public class DroneHUD : MonoBehaviour
    {
        private DronePhysics _physics;
        private GUIStyle _hudStyle;
        private GUIStyle _hudStyleSmall;
        private GUIStyle _hudStyleCenter;
        private GUIStyle _hudStyleWarning;
        private Texture2D _crosshairTex;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;
        private bool _stylesInitialized = false;
        private float _rssi = 99f; // simulated signal

        public void SetDronePhysics(DronePhysics physics)
        {
            _physics = physics;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _hudStyle = new GUIStyle();
            _hudStyle.fontSize = 16;
            _hudStyle.fontStyle = FontStyle.Bold;
            _hudStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            _hudStyle.alignment = TextAnchor.MiddleLeft;

            _hudStyleSmall = new GUIStyle(_hudStyle);
            _hudStyleSmall.fontSize = 13;
            _hudStyleSmall.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);

            _hudStyleCenter = new GUIStyle(_hudStyle);
            _hudStyleCenter.alignment = TextAnchor.MiddleCenter;

            _hudStyleWarning = new GUIStyle(_hudStyle);
            _hudStyleWarning.normal.textColor = new Color(1f, 0.3f, 0.2f, 1f);

            // Crosshair texture
            _crosshairTex = new Texture2D(2, 2);
            _crosshairTex.SetPixels(new[] {
                Color.white, Color.white,
                Color.white, Color.white
            });
            _crosshairTex.Apply();

            // Bar textures
            _barBgTex = MakeSolidTexture(new Color(0f, 0f, 0f, 0.4f));
            _barFillTex = MakeSolidTexture(new Color(0.2f, 0.9f, 0.3f, 0.8f));

            _stylesInitialized = true;
        }

        private Texture2D MakeSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!Plugin.ShowHUD.Value || _physics == null) return;

            InitStyles();

            float w = Screen.width;
            float h = Screen.height;
            float margin = 20f;

            // ── Crosshair ──
            if (Plugin.ShowCrosshair.Value)
            {
                DrawCrosshair(w, h);
            }

            // ── Top center: Mode indicator ──
            GUI.Label(new Rect(w / 2 - 60, margin, 120, 25), "ACRO", _hudStyleCenter);

            // ── Top left: Controller info ──
            string controllerStr = DroneInput.ControllerConnected
                ? $"RC: {TruncateString(DroneInput.ControllerName, 25)}"
                : "RC: KEYBOARD FALLBACK";
            Color origColor = _hudStyleSmall.normal.textColor;
            _hudStyleSmall.normal.textColor = DroneInput.ControllerConnected
                ? new Color(0.3f, 1f, 0.3f, 0.8f)
                : new Color(1f, 0.8f, 0.2f, 0.8f);
            GUI.Label(new Rect(margin, margin, 300, 20), controllerStr, _hudStyleSmall);
            _hudStyleSmall.normal.textColor = origColor;

            // ── Left side: Speed + Vertical speed ──
            float speed = _physics.SpeedKmh;
            float vSpeed = _physics.VerticalSpeed;

            GUI.Label(new Rect(margin, h / 2 - 40, 150, 25),
                $"SPD {speed:F0} km/h", _hudStyle);
            GUI.Label(new Rect(margin, h / 2 - 15, 150, 25),
                $"V/S {vSpeed:F1} m/s", _hudStyleSmall);

            // ── Right side: Altitude ──
            float alt = _physics.Altitude;
            _hudStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(w - margin - 150, h / 2 - 40, 150, 25),
                $"ALT {alt:F0} m", _hudStyle);
            _hudStyle.alignment = TextAnchor.MiddleLeft;

            // ── Bottom left: Throttle bar ──
            DrawThrottleBar(margin, h - 180, 20, 140);

            // ── Bottom center: Heading ──
            float heading = _physics.GetHeading();
            GUI.Label(new Rect(w / 2 - 40, h - 50, 80, 25),
                $"HDG {heading:F0}°", _hudStyleCenter);

            // ── Bottom right: Rates info ──
            _hudStyleSmall.alignment = TextAnchor.MiddleRight;
            float maxRoll = BetaflightRates.GetMaxRollRate();
            float maxPitch = BetaflightRates.GetMaxPitchRate();
            float maxYaw = BetaflightRates.GetMaxYawRate();
            GUI.Label(new Rect(w - margin - 180, h - 95, 180, 18),
                $"R:{maxRoll:F0}  P:{maxPitch:F0}  Y:{maxYaw:F0} °/s", _hudStyleSmall);
            _hudStyleSmall.alignment = TextAnchor.MiddleLeft;

            // ── Bottom right: Motor output ──
            _hudStyleSmall.alignment = TextAnchor.MiddleRight;
            float motorPct = _physics.MotorOutput * 100f;
            GUI.Label(new Rect(w - margin - 180, h - 70, 180, 18),
                $"MTR {motorPct:F0}%", _hudStyleSmall);
            _hudStyleSmall.alignment = TextAnchor.MiddleLeft;

            // ── Stick indicators (bottom center-left and center-right) ──
            float tDisplay = Plugin.ThrottleCenterZero.Value
                ? DroneInput.Throttle
                : DroneInput.Throttle * 2f - 1f;
            DrawStickIndicator(w / 2 - 120, h - 140, DroneInput.Yaw, tDisplay, "T/Y");
            DrawStickIndicator(w / 2 + 50, h - 140, DroneInput.Roll, DroneInput.Pitch, "R/P");

            // ── Tilt warning ──
            float tilt = _physics.GetTiltAngle();
            if (tilt > 150f)
            {
                GUI.Label(new Rect(w / 2 - 60, h / 2 + 40, 120, 25), "!! INVERTED !!", _hudStyleWarning);
            }

            // ── Controls help (shown briefly or toggled) ──
            _hudStyleSmall.normal.textColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            GUI.Label(new Rect(margin, h - 35, 300, 18),
                $"[{Plugin.ToggleDroneKey.Value}] Exit  [{Plugin.ResetDroneKey.Value}] Reset", _hudStyleSmall);
            _hudStyleSmall.normal.textColor = origColor;
        }

        private void DrawCrosshair(float w, float h)
        {
            float cx = w / 2;
            float cy = h / 2;
            float size = 12f;
            float thickness = 2f;
            Color crossColor = new Color(1f, 1f, 1f, 0.5f);
            Color origColor = GUI.color;
            GUI.color = crossColor;

            // Horizontal line
            GUI.DrawTexture(new Rect(cx - size, cy - thickness / 2, size * 2, thickness), _crosshairTex);
            // Vertical line
            GUI.DrawTexture(new Rect(cx - thickness / 2, cy - size, thickness, size * 2), _crosshairTex);
            // Center gap (draw over center)
            GUI.color = new Color(0, 0, 0, 0);
            GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), _crosshairTex);

            GUI.color = origColor;
        }

        private void DrawThrottleBar(float x, float y, float width, float height)
        {
            // Background
            GUI.DrawTexture(new Rect(x, y, width, height), _barBgTex);

            // Fill
            float fillAmount;
            if (Plugin.ThrottleCenterZero.Value)
            {
                fillAmount = (DroneInput.Throttle + 1f) / 2f;
            }
            else
            {
                fillAmount = DroneInput.Throttle;
            }
            fillAmount = Mathf.Clamp01(fillAmount);

            float fillHeight = height * fillAmount;
            float fillY = y + height - fillHeight;

            // Color gradient: green → yellow → red
            Color barColor;
            if (fillAmount < 0.5f)
                barColor = Color.Lerp(new Color(0.2f, 0.8f, 0.3f, 0.8f), new Color(1f, 0.9f, 0.2f, 0.8f), fillAmount * 2f);
            else
                barColor = Color.Lerp(new Color(1f, 0.9f, 0.2f, 0.8f), new Color(1f, 0.3f, 0.2f, 0.8f), (fillAmount - 0.5f) * 2f);

            Texture2D fillTex = MakeSolidTexture(barColor);
            GUI.DrawTexture(new Rect(x, fillY, width, fillHeight), fillTex);

            // Label
            GUI.Label(new Rect(x + width + 4, y + height / 2 - 10, 60, 20),
                $"{(fillAmount * 100f):F0}%", _hudStyleSmall);
        }

        private void DrawStickIndicator(float x, float y, float horizontal, float vertical, string label)
        {
            float boxSize = 60f;
            float dotSize = 6f;

            // Background box
            GUI.DrawTexture(new Rect(x, y, boxSize, boxSize), _barBgTex);

            // Center lines
            Color orig = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(new Rect(x + boxSize / 2 - 0.5f, y, 1, boxSize), _crosshairTex);
            GUI.DrawTexture(new Rect(x, y + boxSize / 2 - 0.5f, boxSize, 1), _crosshairTex);
            GUI.color = orig;

            // Stick dot
            float dotX = x + (horizontal + 1f) / 2f * boxSize - dotSize / 2;
            float dotY = y + (1f - (vertical + 1f) / 2f) * boxSize - dotSize / 2;

            GUI.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            GUI.DrawTexture(new Rect(dotX, dotY, dotSize, dotSize), _crosshairTex);
            GUI.color = orig;

            // Label
            _hudStyleSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(x, y + boxSize + 2, boxSize, 15), label, _hudStyleSmall);
            _hudStyleSmall.alignment = TextAnchor.MiddleLeft;
        }

        private string TruncateString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > maxLen ? s.Substring(0, maxLen) + "..." : s;
        }

        private void OnDestroy()
        {
            if (_crosshairTex != null) Destroy(_crosshairTex);
            if (_barBgTex != null) Destroy(_barBgTex);
            if (_barFillTex != null) Destroy(_barFillTex);
        }
    }
}
