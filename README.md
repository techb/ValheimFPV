# Valheim FPV Drone Mod

> **Work In Progress** — Expect bugs, crashes, and missing features. Report issues on the GitHub repo.

Fly around the Valheim world as an FPV racing drone in **acro/rate mode**. Plug in your RadioMaster (or any USB RC transmitter) and rip through the Black Forest, buzz Viking villages, and dive the mountains — all with realistic drone physics.

![Valheim FPV](https://img.shields.io/badge/Valheim-FPV%20Drone-blue)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-green)
![WIP](https://img.shields.io/badge/status-work%20in%20progress-orange)

---

## Features

- **True acro mode flight** — stick inputs command angular velocity, not angle. No auto-leveling. Release the sticks and the drone holds its current attitude, just like a real quad.
- **Betaflight rate curves** — RC Rate, Rate, and RC Expo per axis, matching Betaflight Configurator exactly. Copy your rates directly from BF Configurator.
- **RadioMaster & USB controller support** — any RC transmitter that exposes a USB HID joystick. Reads axes via WinMM directly, bypassing Unity's Input Manager limitations. Configurable axis mapping and inversion.
- **Axis calibration wizard** — press F7 to open the live input monitor. Move each stick on command and the wizard auto-detects axis mapping and saves immediately. No manual config editing needed.
- **Realistic physics** — thrust along local UP, gravity, quadratic aerodynamic drag, motor spin-up delay, terrain ground collision.
- **Solid obstacle collision** — collides with rocks, boulders, tree trunks, and player-built structures. Foliage and canopy excluded. Toggle-able in config.
- **Full-quality FPV rendering** — the drone view uses the game's existing camera, preserving all post-processing, fog, grass, particles, and ambient occlusion.
- **Map reveal** — flying the drone reveals the fog of war on the minimap, same as the player walking.
- **Player ghosting** — while flying, the player character is hidden, invincible, and clips through everything. World chunks stream under the drone.
- **FPV camera** — configurable uptilt angle and FOV.
- **OSD-style HUD** — speed, altitude, heading, throttle bar, stick position indicators, motor output, and max rates.
- **Keyboard fallback** — WASD + QE + Space/Shift for flying without a controller.

---

## Installation

### Prerequisites
- **Valheim** (Steam)
- **BepInEx 5.4.x** for Valheim — install via [Thunderstore](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) or r2modman

### Install the Mod
1. Build the project (see Building below) or download a release.
2. Copy `ValheimFPVDrone.dll` to `Valheim/BepInEx/plugins/ValheimFPVDrone/`.
3. Launch Valheim.

### RadioMaster Controller Setup
1. **Power on** your RadioMaster transmitter.
2. **Connect USB** to your PC. When prompted on the radio, select **USB Joystick (HID)**.
3. **Verify in Windows**: Open `joy.cpl` (Set up USB game controllers). You should see your radio listed and all axes responding.
4. **Launch Valheim** — the mod auto-detects the controller.
5. **Press F7** in-game to open the input monitor and run the calibration wizard.

#### EdgeTX USB Joystick Configuration
If your radio uses EdgeTX/OpenTX and axes aren't working:
1. On the radio, go to **MDL → USB Joystick** settings.
2. Ensure channels are mapped:
   - CH1 → Aileron (Roll) → Axis X
   - CH2 → Elevator (Pitch) → Axis Y
   - CH3 → Throttle → Axis Z
   - CH4 → Rudder (Yaw) → **Axis RX** (not RZ — WinMM exposes RX as the R axis)
3. Set mode to **Classic** or **Advanced** depending on your firmware.

---

## Controls

### Controller (RadioMaster / RC Transmitter)
| Stick          | Function                     |
|----------------|------------------------------|
| Right Stick X  | Roll (aileron)               |
| Right Stick Y  | Pitch (elevator)             |
| Left Stick Y   | Throttle                     |
| Left Stick X   | Yaw (rudder)                 |

### Keyboard Fallback
| Key            | Function                              |
|----------------|---------------------------------------|
| W / S          | Pitch forward / back                  |
| A / D          | Roll left / right                     |
| Q / E          | Yaw left / right                      |
| Space          | Increase throttle                     |
| Left Shift     | Decrease throttle                     |
| F7             | Open input monitor / calibration wizard |
| F8             | Toggle drone on/off                   |
| F9             | Reset drone to player position        |

---

## Configuration

All settings are in `BepInEx/config/com.fpvdrone.valheim.cfg`. Edit live with [Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/) (press F1 in-game).

### Rates (Betaflight-style)
These match the **BETAFLIGHT** rate type in Betaflight Configurator exactly. You can copy your rates from BF Configurator directly.

| Setting        | Default | Description                                               |
|----------------|---------|-----------------------------------------------------------|
| RollRCRate     | 1.7     | Center sensitivity for roll. "RC Rate" in BF Configurator.|
| RollRate       | 0.5     | Full-stick boost for roll. "Rate" in BF Configurator.     |
| RollRCExpo     | 0.0     | Center softening for roll. "RC Expo" in BF Configurator.  |
| PitchRCRate    | 1.7     | Center sensitivity for pitch.                             |
| PitchRate      | 0.5     | Full-stick boost for pitch.                               |
| PitchRCExpo    | 0.0     | Center softening for pitch.                               |
| YawRCRate      | 1.7     | Center sensitivity for yaw.                               |
| YawRate        | 0.5     | Full-stick boost for yaw.                                 |
| YawRCExpo      | 0.0     | Center softening for yaw.                                 |

### Physics
| Setting              | Default | Description                                     |
|----------------------|---------|-------------------------------------------------|
| Gravity              | 9.81    | m/s². Adjust for floaty/heavy feel.             |
| MaxThrust            | 75.0    | Newtons. Higher = more powerful motors.         |
| Mass                 | 0.99    | kg.                                             |
| DragCoefficient      | 0.02    | Air resistance. Lower = faster top speed. 0.02 gives ~120–140 km/h equilibrium. |
| AngularDragCoeff     | 8.0     | How snappy sticks feel. Higher = snappier.      |
| MotorSpinUpTime      | 0.03    | Seconds for motors to respond to throttle.      |
| ObstacleCollision    | true    | Collide with rocks, boulders, trunks, and player-built structures. Ground is always active. |

### Speed
| Setting              | Default | Description                              |
|----------------------|---------|------------------------------------------|
| MaxSpeed             | 100.0   | m/s hard cap (~360 km/h). Physics drag determines actual equilibrium speed. |
| IdleThrottlePercent  | 5.0     | Motor idle at zero throttle.             |

### Camera
| Setting              | Default | Description                                |
|----------------------|---------|--------------------------------------------|
| CameraTiltAngle      | 30.0    | Degrees of camera uptilt. 25–45 typical.   |
| CameraFOV            | 110.0   | Field of view. 100–140 for FPV.            |

### Controller Mapping
| Setting              | Default | Description                                                   |
|----------------------|---------|---------------------------------------------------------------|
| ThrottleAxis         | 2       | 0-based joystick axis index for throttle.                     |
| RollAxis             | 0       | Aileron / right stick X.                                      |
| PitchAxis            | 1       | Elevator / right stick Y.                                     |
| YawAxis              | 3       | Rudder / left stick X.                                        |
| InvertThrottle       | false   | Flip throttle direction.                                      |
| InvertRoll           | false   | Flip roll direction.                                          |
| InvertPitch          | false   | Flip pitch direction.                                         |
| InvertYaw            | false   | Flip yaw direction.                                           |
| StickDeadzone        | 0.02    | Deadzone for roll/pitch/yaw.                                  |
| ThrottleDeadzone     | 0.02    | Deadzone for throttle.                                        |
| ThrottleCenterZero   | false   | true = spring throttle (centers at 0). false = RC style.      |
| ThrottleRangeMin     | 0.0     | Raw throttle floor. `-1` = full-range axis. `0` = half-range (RadioMaster USB HID default). Auto-detected by calibration wizard. |

---

## Building from Source

### Requirements
- .NET SDK or Visual Studio with .NET 4.6.2 targeting pack
- Valheim + BepInEx installed

### Steps
1. Clone this repository.
2. Create a `libs/` folder inside `ValheimFPVDrone/ValheimFPVDrone/`.
3. Copy the required DLLs into `libs/`:
   ```
   From Valheim/BepInEx/core/:
     BepInEx.dll
     0Harmony.dll

   From Valheim/valheim_Data/Managed/:
     assembly_valheim.dll

   From Valheim/unstripped_corlib/:
     UnityEngine.dll
     UnityEngine.CoreModule.dll
     UnityEngine.PhysicsModule.dll
     UnityEngine.InputLegacyModule.dll
     UnityEngine.IMGUIModule.dll
     UnityEngine.TextRenderingModule.dll
     UnityEngine.AudioModule.dll
   ```
4. Build:
   ```bash
   cd ValheimFPVDrone/ValheimFPVDrone
   dotnet build -c Release
   ```
5. The output DLL will be in `bin/Release/net462/ValheimFPVDrone.dll`.
6. Copy to `Valheim/BepInEx/plugins/ValheimFPVDrone/`.

### Using Publicized Assemblies (Recommended)
For access to private members, use [AssemblyPublicizer](https://github.com/CabbageCrow/AssemblyPublicizer) on `assembly_valheim.dll` and reference the publicized version instead.

---

## How It Works

### Acro Mode Physics
In acro (rate) mode, the drone has no stabilization. Your stick inputs set the **angular velocity** — how fast the drone rotates — not the angle:

- **Stick at center** → drone holds its current orientation (no auto-level)
- **Stick deflected** → drone rotates at a rate determined by Betaflight rate curves
- **Throttle** → thrust along the drone's local UP axis
- **To move forward**: pitch the nose down so thrust has a forward component
- **To hover**: keep the drone level and throttle at hover power

### Betaflight Rate Formula
The mod implements the **BETAFLIGHT** rate type, matching Betaflight Configurator exactly:

```
1. Apply RC Expo (softens center):
   rcCmd = rcCmd * (|rcCmd|² * expo + (1 - expo))

2. Center sensitivity:
   angleRate = rcRate * 200 * rcCmd

3. Rate boost at full stick:
   angleRate = angleRate / (1.0 - |rcCmd| * rate)

Capped at ±1998 deg/sec.
```

You can copy RC Rate, Rate, and RC Expo values from Betaflight Configurator directly into the mod config.

### Camera System
The mod reuses the game's existing `Camera.main` rather than creating a new camera. This preserves all post-processing effects, fog, grass streaming, and depth of field. Each frame, after Valheim's `GameCamera.LateUpdate()` runs (updating all rendering systems), the mod re-asserts the camera position to the drone's location.

### Player Ghosting
While flying, the player character is fully hidden (renderers disabled), invincible (damage blocked), and clips through everything (CharacterController and colliders disabled). The player's world position follows the drone horizontally at the original ground elevation, keeping Valheim's zone system loading terrain under the drone.

### Motor Spin-Up
Real motors don't instantly change RPM. The `MotorSpinUpTime` parameter adds a small delay between throttle input and actual thrust output, simulating motor and ESC response latency.

---

## Tips for Flying

1. **Start with low rates** if you're new to acro: RC Rate 1.0, Rate 0.4, RC Expo 0.0.
2. **Camera tilt matters**: Higher tilt (40°+) for speed runs, lower (15–25°) for freestyle and slow flying.
3. **Throttle management**: Unlike angle mode, you need to actively manage throttle through all maneuvers.
4. **Use the keyboard first** to get a feel, then switch to your radio for the real experience.
5. **Reset with F9** if you crash into terrain — respawns the drone at your player.
6. **Run the F7 calibration wizard** once after connecting your radio. It auto-detects all axes and saves permanently.

---

## Troubleshooting

**Controller not detected:**
- Check `joy.cpl` — your radio should appear as a game controller.
- Make sure you selected "USB Joystick" mode on the radio, not "USB Storage."
- Try disconnecting and reconnecting after Valheim is running.

**Axes are wrong / drone spins on startup:**
- Press **F7** to open the input monitor and run the calibration wizard. Move each stick on command — axis mapping is auto-detected and saved immediately.
- Use the `Invert*` settings to flip any reversed axes.

**Yaw axis not showing in F7 monitor:**
- In EdgeTX, set CH4 (Rudder/Yaw) to **HID Rx** (not Rz). WinMM exposes the R axis (dwRpos) but not always Rz depending on caps flags.

**Throttle stuck at 50% / won't go to zero:**
- Run the F7 calibration wizard — it auto-sets `ThrottleRangeMin = 0` for half-range radios.
- Or set `ThrottleRangeMin = 0` manually in the config.

**Calibration not persisting after restart:**
- Check `BepInEx/LogOutput.log` for a line: `[FPVDrone] Calibration saved to: <path>` — this confirms the save happened and shows the file location.
- Also check the startup line: `[FPVDrone] Loaded axis mapping — T=Axis...` to confirm values loaded correctly.

**Drone falls through terrain:**
- The mod uses raycasting on the `terrain` layer for ground detection. Very complex terrain mods may cause issues.

**Getting stuck in trees:**
- Tree trunks are solid (`static_solid` layer). Tree foliage/canopy is excluded from collision intentionally. If collision is off, set `ObstacleCollision = true` in config.

**Player dies when exiting drone mode:**
- Fixed in current version — fall damage tracking is reset when exiting. If it still occurs, check the BepInEx log for `m_maxAirAltitude field not found` which would indicate a Valheim version mismatch.

**Game camera doesn't restore after exiting:**
- Press F8 twice to toggle drone off and on, which resets the camera override.

---

## License

MIT License. Use it, modify it, share it.

---

## Credits

- Physics model inspired by real Betaflight firmware and FPV drone dynamics.
- Built for [BepInEx](https://github.com/BepInEx/BepInEx) modding framework.
- Rate formula from [Betaflight](https://github.com/betaflight/betaflight) source code.
