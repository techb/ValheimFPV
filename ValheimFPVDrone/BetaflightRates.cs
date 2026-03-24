using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Implements the Betaflight "BETAFLIGHT" rate type, matching the three parameters
    /// shown in Betaflight Configurator: RC Rate, Rate, and RC Expo.
    ///
    /// Formula (from Betaflight source, flight/pid.c):
    ///   1. RC Expo softens center: rcCmd = rcCmd * |rcCmd|² * expo + rcCmd * (1 - expo)
    ///   2. Center sensitivity:     angleRate = rcRate * 200 * rcCmd
    ///                              (if rcRate > 2.0, extra high-rate term is added)
    ///   3. Rate boosts full stick: angleRate /= (1.0 - |rcCmd| * rate)
    ///   Capped at ±1998 deg/sec.
    /// </summary>
    public static class BetaflightRates
    {
        private const float MAX_RATE = 1998f; // Betaflight hard cap

        /// <summary>
        /// Convert stick input to angular rate in deg/sec.
        /// Parameters match Betaflight Configurator labels exactly.
        /// </summary>
        /// <param name="stickInput">Normalized stick, -1 to 1.</param>
        /// <param name="rcRate">RC Rate — center sensitivity. Typical: 1.0–1.5.</param>
        /// <param name="rate">Rate — full-stick boost (0.0–1.0). Typical: 0.7. Maps to "Rate" in BF Configurator.</param>
        /// <param name="rcExpo">RC Expo — center softening (0.0–1.0). Typical: 0.0. Maps to "RC Expo" in BF Configurator.</param>
        public static float CalcRate(float stickInput, float rcRate, float rate, float rcExpo)
        {
            // Step 1: Apply RC Expo — softens center stick without affecting full deflection.
            // f(x) = x * (|x|² * expo + (1 - expo))
            if (rcExpo > 0f)
            {
                float absIn = Mathf.Abs(stickInput);
                stickInput = stickInput * (absIn * absIn * rcExpo + (1f - rcExpo));
            }

            float absStick = Mathf.Abs(stickInput);

            // Step 2: RC Rate → center sensitivity (deg/sec at full stick, no Rate boost).
            // For rcRate > 2.0 Betaflight adds a steep extra term (rarely used).
            float angleRate;
            if (rcRate > 2.0f)
                angleRate = (rcRate * 200f + (rcRate - 2.0f) * 14142.135f) * stickInput;
            else
                angleRate = rcRate * 200f * stickInput;

            // Step 3: Rate → boosts rotation speed at stick endpoints.
            // At full stick (|x|=1): multiplier = 1 / (1 - rate). At center (|x|=0): no boost.
            if (rate > 0f)
            {
                float superFactor = 1.0f / Mathf.Max(1.0f - absStick * rate, 0.01f);
                angleRate *= superFactor;
            }

            return Mathf.Clamp(angleRate, -MAX_RATE, MAX_RATE);
        }

        public static float GetRollRate(float stickInput) =>
            CalcRate(stickInput, Plugin.RollRCRate.Value, Plugin.RollRate.Value, Plugin.RollRCExpo.Value);

        public static float GetPitchRate(float stickInput) =>
            CalcRate(stickInput, Plugin.PitchRCRate.Value, Plugin.PitchRate.Value, Plugin.PitchRCExpo.Value);

        public static float GetYawRate(float stickInput) =>
            CalcRate(stickInput, Plugin.YawRCRate.Value, Plugin.YawRate.Value, Plugin.YawRCExpo.Value);

        public static float GetMaxRollRate()  => Mathf.Abs(CalcRate(1f, Plugin.RollRCRate.Value,  Plugin.RollRate.Value,  Plugin.RollRCExpo.Value));
        public static float GetMaxPitchRate() => Mathf.Abs(CalcRate(1f, Plugin.PitchRCRate.Value, Plugin.PitchRate.Value, Plugin.PitchRCExpo.Value));
        public static float GetMaxYawRate()   => Mathf.Abs(CalcRate(1f, Plugin.YawRCRate.Value,   Plugin.YawRate.Value,   Plugin.YawRCExpo.Value));
    }
}

