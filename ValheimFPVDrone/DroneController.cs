using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// Top-level drone controller. Manages:
    /// - Spawning/despawning the drone object
    /// - Switching camera from player to drone FPV
    /// - Disabling player controls while flying
    /// - Calling DroneInput + DronePhysics each frame
    /// </summary>
    public class DroneController : MonoBehaviour
    {
        public static DroneController Instance { get; private set; }
        public bool IsFlying { get; private set; } = false;

        private GameObject _droneObject;
        private GameObject _visualModel;
        private DronePhysics _physics;
        private DroneHUD _hud;

        // Camera state — we reuse the GameCamera directly instead of creating a new one
        private Camera _fpvCamera;      // points to _originalCamera while flying; null otherwise
        private Camera _originalCamera;
        private bool _originalCameraState;
        private float _originalFOV;
        private float _originalNearClip;

        // Player state to restore
        private Vector3 _playerPosition;
        private Quaternion _playerRotation;

        // Player ghost state (hidden + no collision while flying)
        private Renderer[] _playerRenderers;
        private CharacterController _playerCC;
        private Collider[] _playerColliders;

        // Camera view mode
        private bool _thirdPerson = false;

        // Fixed timestep accumulator for physics
        private float _physicsAccumulator = 0f;
        private const float PHYSICS_DT = 0.005f; // 200Hz physics for smooth flight

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Toggle drone on/off
            if (Input.GetKeyDown(Plugin.ToggleDroneKey.Value))
            {
                if (IsFlying) ExitDrone();
                else EnterDrone();
            }

            // Reset drone position
            if (IsFlying && Input.GetKeyDown(Plugin.ResetDroneKey.Value))
            {
                ResetDronePosition();
            }

            // Toggle camera view
            if (IsFlying && Input.GetKeyDown(Plugin.ToggleCameraViewKey.Value))
            {
                _thirdPerson = !_thirdPerson;
                Plugin.Log.LogInfo($"Camera: {(_thirdPerson ? "Third Person" : "FPV")}");
            }

            if (!IsFlying) return;

            // Update input
            DroneInput.Update();

            // Fixed-timestep physics at high frequency for smooth flight
            _physicsAccumulator += Time.unscaledDeltaTime;
            int steps = 0;
            while (_physicsAccumulator >= PHYSICS_DT && steps < 20)
            {
                _physics.PhysicsUpdate(
                    DroneInput.Throttle,
                    DroneInput.Roll,
                    DroneInput.Pitch,
                    DroneInput.Yaw,
                    PHYSICS_DT
                );
                _physicsAccumulator -= PHYSICS_DT;
                steps++;
            }

            // Terrain collision at frame rate is fine
            _physics.HandleTerrainCollision();

            // Reveal map at drone position (mirrors what Player calls each frame)
            ExploreMap(_droneObject.transform.position);

            // Keep player frozen
            FreezePlayer();
        }

        private bool _warnShown = false;

        private void EnterDrone()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Plugin.Log.LogWarning("No local player found — cannot spawn drone.");
                return;
            }

            Plugin.Log.LogInfo("Entering FPV drone mode!");

            if (!_warnShown)
            {
                _warnShown = true;
                if (MessageHud.instance != null)
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.Center,
                        $"FPV Drone v{Plugin.PluginVersion} — Work In Progress. Expect bugs!");
            }

            // Save player state
            _playerPosition = player.transform.position;
            _playerRotation = player.transform.rotation;

            // Create drone object
            _droneObject = new GameObject("FPVDrone");
            _droneObject.transform.position = player.transform.position + Vector3.up * 2f + player.transform.forward * 2f;
            _droneObject.transform.rotation = player.transform.rotation;

            // Add physics component
            _physics = _droneObject.AddComponent<DronePhysics>();

            // Attach visual model
            _visualModel = DroneModel.Attach(_droneObject, Plugin.DroneModel.Value);

            // Ghost the player: invisible, invincible, no collision
            GhostPlayer(player);

            // Create FPV camera
            CreateFPVCamera();

            // Create HUD
            if (_hud == null)
            {
                GameObject hudObj = new GameObject("DroneHUD");
                _hud = hudObj.AddComponent<DroneHUD>();
            }
            _hud.SetDronePhysics(_physics);
            _hud.enabled = true;

            // Disable game camera, enable FPV
            SwitchToFPVCamera();

            // Start in FPV mode
            _thirdPerson = false;
            IsFlying = true;

            // Freeze the game's time scale? No — we want the world to keep running.
            // Instead we disable player movement via the character controller.

            Plugin.Log.LogInfo("FPV Drone spawned! Use your controller or WASD to fly.");
        }

        private void ExitDrone()
        {
            Plugin.Log.LogInfo("Exiting FPV drone mode.");

            IsFlying = false;

            // Restore camera (including culling mask for drone model layer)
            DroneModel.SetFPVCameraVisibility(_fpvCamera, false);
            RestoreGameCamera();

            // Destroy drone
            if (_droneObject != null)
            {
                DroneModel.Detach(ref _visualModel);
                Destroy(_droneObject);
                _droneObject = null;
                _physics = null;
            }

            // _fpvCamera is the game camera — don't destroy it, just clear the reference
            _fpvCamera = null;

            // Hide HUD
            if (_hud != null)
            {
                _hud.SetDronePhysics(null);
                _hud.enabled = false;
            }

            // Unfreeze player
            UnfreezePlayer();
        }

        private void ResetDronePosition()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _droneObject == null) return;

            _droneObject.transform.position = player.transform.position + Vector3.up * 2f + player.transform.forward * 2f;
            _droneObject.transform.rotation = player.transform.rotation;
            _physics.ResetState();

            Plugin.Log.LogInfo("Drone position reset.");
        }

        private void CreateFPVCamera()
        {
            // We reuse the existing GameCamera (see SwitchToFPVCamera) so no new
            // camera object is needed — all post-processing and render settings are
            // preserved automatically.
        }

        /// <summary>
        /// Called by GameCamera_LateUpdate_Patch Postfix — re-asserts drone position,
        /// rotation, and FOV after Valheim's LateUpdate resets them to the player.
        /// Running as Postfix (not Prefix skip) ensures all LateUpdate side-effects
        /// (grass streaming, fog, particles, DOF) still execute each frame.
        /// </summary>
        public void ApplyFPVCameraOverrides()
        {
            if (_fpvCamera == null || _droneObject == null) return;

            Transform drone = _droneObject.transform;

            if (_thirdPerson)
            {
                // Chase camera: offset in the drone's local space so it follows rolls and flips
                float dist = Plugin.ThirdPersonDistance.Value;
                float height = Plugin.ThirdPersonHeight.Value;
                Vector3 offset = drone.rotation * new Vector3(0f, height, -dist);
                _fpvCamera.transform.position = drone.position + offset;
                _fpvCamera.transform.rotation = Quaternion.LookRotation(
                    drone.position - _fpvCamera.transform.position, drone.up);
            }
            else
            {
                // FPV: camera at drone position with tilt
                float tilt = Plugin.CameraTiltAngle.Value;
                _fpvCamera.transform.position = drone.position;
                _fpvCamera.transform.rotation = drone.rotation * Quaternion.Euler(-tilt, 0f, 0f);
            }

            _fpvCamera.fieldOfView   = Plugin.CameraFOV.Value;
            _fpvCamera.nearClipPlane = 0.05f;

            // In FPV mode, optionally hide the drone model from the local camera
            // so it doesn't block the view. Other players still see it.
            bool hideModel = !_thirdPerson && Plugin.HideModelInFPV.Value;
            DroneModel.SetFPVCameraVisibility(_fpvCamera, hideModel);
        }

        // Cached reflection info for Minimap.Explore (not exposed in publicized DLL)
        private static System.Reflection.MethodInfo _minimapExplore;
        private static bool _minimapExploreSearched;

        private void ExploreMap(Vector3 pos)
        {
            if (Minimap.instance == null) return;

            if (!_minimapExploreSearched)
            {
                _minimapExploreSearched = true;
                _minimapExplore = typeof(Minimap).GetMethod("Explore",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Vector3), typeof(float) },
                    null);
                if (_minimapExplore == null)
                    Plugin.Log.LogWarning("[FPVDrone] Minimap.Explore not found — map reveal disabled.");
            }

            _minimapExplore?.Invoke(Minimap.instance, new object[] { pos, 100f });
        }

        private void SwitchToFPVCamera()
        {
            // Reuse the existing GameCamera — all post-processing, culling mask,
            // rendering path and other components stay intact.
            _originalCamera = Camera.main;
            if (_originalCamera == null) return;

            _originalCameraState = _originalCamera.enabled;
            _originalFOV         = _originalCamera.fieldOfView;
            _originalNearClip    = _originalCamera.nearClipPlane;

            // Apply FPV overrides directly to the game camera
            _originalCamera.fieldOfView   = Plugin.CameraFOV.Value;
            _originalCamera.nearClipPlane = 0.05f;

            // Use it as our FPV camera reference (GameCamera_LateUpdate_Patch
            // already blocks the game from overriding the transform while flying)
            _fpvCamera = _originalCamera;
        }

        private void RestoreGameCamera()
        {
            // Restore the overrides we applied to the game camera
            if (_originalCamera != null)
            {
                _originalCamera.fieldOfView   = _originalFOV;
                _originalCamera.nearClipPlane = _originalNearClip;
                _originalCamera.enabled       = _originalCameraState;
            }
            // GameCamera_LateUpdate_Patch will resume once IsFlying = false,
            // returning full control of the camera transform to the game.
        }

        private void FreezePlayer()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _droneObject == null) return;

            // Follow drone horizontally so Valheim's ZoneSystem loads chunks under
            // the drone, but keep the player at their original Y (ground level) to
            // avoid collision with the drone which is flying above them.
            Vector3 followPos = new Vector3(
                _droneObject.transform.position.x,
                _playerPosition.y,
                _droneObject.transform.position.z);
            player.transform.position = followPos;

            try
            {
                var body = player.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            catch { }
        }

        private void UnfreezePlayer()
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.transform.position = _playerPosition;

                // Reset Valheim's fall-damage tracker so the game doesn't see the
                // teleport as a fall from whatever altitude it last recorded.
                ResetFallState(player);

                // Zero out any residual velocity so the CharacterController doesn't
                // launch the player when it's re-enabled.
                try
                {
                    var body = player.GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
                catch { }
            }

            RestorePlayer(player);
        }

        // Cached reflection info for Character.m_maxAirAltitude
        private static System.Reflection.FieldInfo _maxAirAltitudeField;
        private static bool _maxAirAltitudeSearched;

        private static void ResetFallState(Character character)
        {
            if (!_maxAirAltitudeSearched)
            {
                _maxAirAltitudeSearched = true;
                _maxAirAltitudeField = typeof(Character).GetField("m_maxAirAltitude",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (_maxAirAltitudeField == null)
                    Plugin.Log.LogWarning("[FPVDrone] m_maxAirAltitude field not found — fall damage reset unavailable.");
            }

            if (_maxAirAltitudeField != null)
                _maxAirAltitudeField.SetValue(character, character.transform.position.y);
        }

        private void GhostPlayer(Player player)
        {
            // Hide all renderers
            _playerRenderers = player.GetComponentsInChildren<Renderer>(false);
            foreach (var r in _playerRenderers)
                r.enabled = false;

            // Disable CharacterController so the player clips through terrain
            _playerCC = player.GetComponent<CharacterController>();
            if (_playerCC != null) _playerCC.enabled = false;

            // Disable all colliders so nothing can push or block the player
            _playerColliders = player.GetComponentsInChildren<Collider>(false);
            foreach (var c in _playerColliders)
                c.enabled = false;
        }

        private void RestorePlayer(Player player)
        {
            if (player == null) return;

            if (_playerRenderers != null)
            {
                foreach (var r in _playerRenderers)
                    if (r != null) r.enabled = true;
                _playerRenderers = null;
            }

            if (_playerCC != null)
            {
                _playerCC.enabled = true;
                _playerCC = null;
            }

            if (_playerColliders != null)
            {
                foreach (var c in _playerColliders)
                    if (c != null) c.enabled = true;
                _playerColliders = null;
            }
        }

        private void OnDestroy()
        {
            if (IsFlying) ExitDrone();
        }
    }
}
