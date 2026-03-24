# Valheim FPV Drone Mod

Fly around the Valheim world as an FPV racing drone in **acro/rate mode**. Plug in your RadioMaster (or any USB RC transmitter) and rip through the Black Forest, buzz Viking villages, and dive the mountains — all with realistic drone physics.

![Valheim FPV](https://img.shields.io/badge/Valheim-FPV%20Drone-blue)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-green)

---

## Features

- **True acro mode flight** — stick inputs command angular velocity, not angle. No auto-leveling. Release the sticks and the drone holds its current attitude, just like a real quad.
- **Betaflight rate curves** — configure RC Rate, Rate, and Super Rate for roll/pitch/yaw independently. Same math as actual Betaflight firmware.
- **RadioMaster & USB controller support** — any RC transmitter that exposes a USB HID joystick (RadioMaster TX16S, Boxer, Pocket, Zorro; FrSky, Jumper, etc.). Configurable axis mapping and inversion.
- **Realistic physics** — thrust along local UP, gravity, quadratic aerodynamic drag, motor spin-up delay, ground collision.
- **FPV camera** — configurable uptilt angle and FOV, just like mounting a GoPro or DJI camera on a real quad.
- **OSD-style HUD** — speed, altitude, heading, throttle bar, stick position indicators, motor output, max rates display.
- **Keyboard fallback** — WASD + QE + Space/Shift for flying without a controller.
- **Axis calibration wizard** — press F7 to open the live input monitor. Move each stick on command and the wizard auto-detects which physical axis maps to throttle, roll, pitch, and yaw. Saves directly to config.
- **Fully configurable** — every parameter is exposed in BepInEx config. Edit with F1 (Configuration Manager) or the config file.

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

#### EdgeTX USB Joystick Configuration
If your radio uses EdgeTX/OpenTX and axes aren't working:
1. On the radio, go to **MDL → USB Joystick** settings.
2. Ensure channels are mapped:
   - CH1 → Aileron (Roll) → Axis X
   - CH2 → Elevator (Pitch) → Axis Y
   - CH3 → Throttle → Axis Z
   - CH4 → Rudder (Yaw) → Axis RX
3. Set mode to **Classic** or **Advanced** depending on your firmware.
4. Axes in the mod config are 0-indexed, so the defaults (0,1,2,3) match this standard mapping. Adjust in config if your radio differs.

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
| Key            | Function                     |
|----------------|------------------------------|
| W / S          | Pitch forward / back         |
| A / D          | Roll left / right            |
| Q / E          | Yaw left / right             |
| Space          | Increase throttle            |
| Left Shift     | Decrease throttle            |
| F7             | Open input monitor / calibration wizard |
| F8             | Toggle drone on/off          |
| F9             | Reset drone to player        |

---

## Configuration

All settings are in `BepInEx/config/com.fpvdrone.valheim.cfg`. You can also edit them live with [Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/) (press F1 in-game).

### Rates (Betaflight-style)
| Setting        | Default | Description                                  |
|----------------|---------|----------------------------------------------|
| RollRCRate     | 1.0     | Center sensitivity for roll                  |
| RollRate       | 0.7     | Max rotation speed factor                    |
| RollSuperRate  | 0.75    | Endpoint acceleration (expo-like)            |
| PitchRCRate    | 1.0     | Center sensitivity for pitch                 |
| PitchRate      | 0.7     | Max rotation speed factor                    |
| PitchSuperRate | 0.75    | Endpoint acceleration                        |
| YawRCRate      | 1.0     | Center sensitivity for yaw                   |
| YawRate        | 0.65    | Max rotation speed factor                    |
| YawSuperRate   | 0.70    | Endpoint acceleration                        |

These use the **exact Betaflight formula**. You can copy your rates from Betaflight Configurator directly.

### Physics
| Setting              | Default | Description                              |
|----------------------|---------|------------------------------------------|
| Gravity              | 9.81    | m/s². Adjust for floaty/heavy feel       |
| MaxThrust            | 35.0    | Newtons. ~3.5x weight = racing quad      |
| Mass                 | 0.8     | kg. Typical 5" quad                      |
| DragCoefficient      | 0.4     | Air resistance. Higher = slower top speed|
| AngularDragCoeff     | 8.0     | How snappy sticks feel. Higher = snappier|
| MotorSpinUpTime      | 0.05    | Seconds for motors to respond            |

### Speed
| Setting              | Default | Description                              |
|----------------------|---------|------------------------------------------|
| MaxSpeed             | 50.0    | m/s (~180 km/h)                          |
| IdleThrottlePercent  | 5.0     | Motor idle when throttle is at zero      |

### Camera
| Setting              | Default | Description                              |
|----------------------|---------|------------------------------------------|
| CameraTiltAngle      | 30.0    | Degrees of camera uptilt. 25-45 typical  |
| CameraFOV            | 110.0   | Field of view. 100-140 for FPV           |

### Controller Mapping
| Setting              | Default | Description                              |
|----------------------|---------|------------------------------------------|
| ThrottleAxis         | 2       | 0-based joystick axis index              |
| RollAxis             | 0       | Typically aileron / right stick X        |
| PitchAxis            | 1       | Typically elevator / right stick Y       |
| YawAxis              | 3       | Typically rudder / left stick X          |
| InvertThrottle       | false   | Flip throttle direction                  |
| InvertRoll           | false   | Flip roll direction                      |
| InvertPitch          | false   | Flip pitch direction                     |
| InvertYaw            | false   | Flip yaw direction                       |
| StickDeadzone        | 0.02    | Deadzone for roll/pitch/yaw              |
| ThrottleDeadzone     | 0.02    | Deadzone for throttle                    |
| ThrottleCenterZero   | false   | true = spring throttle, false = RC style |
| ThrottleRangeMin     | 0.0     | -1 = full-range axis, 0 = half-range (RadioMaster USB HID default). Auto-detected by the calibration wizard. |

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
In acro (rate) mode, the drone has no stabilization. Your stick inputs set the **angular velocity** — how fast the drone rotates — not the angle. This means:

- **Stick at center** → drone holds its current orientation (no auto-level)
- **Stick deflected** → drone rotates at a rate determined by Betaflight rate curves
- **Throttle** → thrust along the drone's local UP axis
- **To move forward**: pitch the nose down so thrust has a forward component
- **To hover**: keep the drone level and throttle at ~hover power

### Betaflight Rate Formula
The mod uses the actual Betaflight rate calculation:
```
rcRateCalc = rcCommand × rcRate × 200
superFactor = 1 / (1 - |rcCommand| × superRate)
finalRate = rcRateCalc × superFactor
```
This means you can paste your Betaflight rates directly into the config and get identical stick feel.

### Motor Spin-Up
Real motors don't instantly change RPM. The `MotorSpinUpTime` parameter adds a small delay between throttle input and actual thrust output, simulating motor+ESC response time.

---

## Tips for Flying

1. **Start with low rates** if you're new to acro: Roll/Pitch Rate 0.5, SuperRate 0.5.
2. **Camera tilt matters**: Higher tilt (40°+) is better for speed runs, lower (20-25°) for freestyle.
3. **Throttle management**: Unlike angle mode, you need to actively manage throttle throughout maneuvers. A split-S or flip requires throttle adjustment.
4. **Use the keyboard first** to get a feel, then switch to your radio for the real experience.
5. **Reset with F9** if you crash into terrain — it respawns the drone at your player.

---

## Troubleshooting

**Controller not detected:**
- Check `joy.cpl` in Windows — your radio should appear as a game controller.
- Make sure you selected "USB Joystick" mode on the radio, not "USB Storage."
- Try disconnecting and reconnecting after Valheim is running.

**Axes are wrong / drone spins on startup:**
- Press **F7** to open the input monitor and run the calibration wizard (press ENTER from the monitor). Move each stick on command — axis mapping is auto-detected and saved.
- Use the `Invert*` settings to flip any reversed axes.
- Set `ThrottleCenterZero = true` if your throttle stick has a spring (centers when released).

**Throttle stuck at 50% / won't go to zero:**
- Your radio outputs throttle as a half-range axis (common with RadioMaster in USB HID mode). Run the F7 calibration wizard — it auto-sets `ThrottleRangeMin = 0` for half-range radios.
- Or set `ThrottleRangeMin = 0` manually in the config.

**Drone falls through terrain:**
- The mod uses raycasting for ground detection. Extremely complex terrain or mods that modify terrain collision may cause issues.

**Game camera doesn't restore:**
- Press F8 twice to toggle drone off and on, which should restore the camera.

---

## License

MIT License. Use it, modify it, share it.

---

## Credits

- Physics model inspired by [KestrelFPV](https://github.com/eleurent/KestrelFPV) and real Betaflight firmware.
- Built for [BepInEx](https://github.com/BepInEx/BepInEx) modding framework.
- Rate formula from [Betaflight](https://github.com/betaflight/betaflight) source code.
