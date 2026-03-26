# Changelog

All notable changes to the Valheim FPV Drone Mod will be documented in this file.

## [Unreleased]

## [0.3.4] - 2026-03-26

### Added
- **Visual drone models** — attach a Valheim model to the drone so other players can see it in multiplayer. Choose from: Karve, Deathsquito, Deer, Dragon, Player (your character), or None (invisible). Configurable via the `DroneModel` setting in the Visual section.
- **Hide model in FPV** — new `HideModelInFPV` config option (default: true). Uses Unity layer 31 to exclude the drone model from the local FPV camera's culling mask while keeping it visible to other players' cameras.
- **Third-person chase camera** — press V (configurable) to toggle between FPV and a third-person camera that follows the drone through rolls and flips. The camera offset is in the drone's local space so it rotates with the drone.
- **Third-person config options** — `ThirdPersonDistance` (default 5m) and `ThirdPersonHeight` (default 2m) to adjust the chase camera position.
- New source files: `DroneModelType.cs` (enum), `DroneModel.cs` (model spawning, stripping, layer management).

### Fixed
- **Drone HUD crosshair persisting after exiting drone mode** — added `IsFlying` guard directly in `DroneHUD.OnGUI` and explicit physics reference cleanup on exit.
- **Spawned prefab models activating game AI** — prefabs are now deactivated before `Object.Instantiate` so the clone starts inactive. All MonoBehaviours, ZNetView, Rigidbody, and Colliders are stripped via `DestroyImmediate` while the clone is still inactive, then only the visual shell is reactivated. Previously, instantiating creatures like Deathsquito or Dragon would spawn live hostile entities.
- **Model cleanup on exit** — `Detach` uses `DestroyImmediate` for instant cleanup. Camera culling mask is restored when exiting drone mode.

### Changed
- Drone models use their native scale (no scaling applied).
- Player model clones the character's visual hierarchy as-is, preserving world-space scale from the player's transform chain.

## [0.3.3] - 2026-03-25

### Added
- Initial public release with acro mode flight, Betaflight rate curves, RadioMaster/USB controller support, axis calibration wizard, custom physics simulation, obstacle collision, FPV camera, OSD HUD, and keyboard fallback.
