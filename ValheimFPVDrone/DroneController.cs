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

            // Update FPV camera
            UpdateFPVCamera();

            // Keep player frozen
            FreezePlayer();
        }

        private void EnterDrone()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Plugin.Log.LogWarning("No local player found — cannot spawn drone.");
                return;
            }

            Plugin.Log.LogInfo("Entering FPV drone mode!");

            // Save player state
            _playerPosition = player.transform.position;
            _playerRotation = player.transform.rotation;

            // Create drone object
            _droneObject = new GameObject("FPVDrone");
            _droneObject.transform.position = player.transform.position + Vector3.up * 2f + player.transform.forward * 2f;
            _droneObject.transform.rotation = player.transform.rotation;

            // Add physics component
            _physics = _droneObject.AddComponent<DronePhysics>();

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

            // Disable player input
            IsFlying = true;

            // Freeze the game's time scale? No — we want the world to keep running.
            // Instead we disable player movement via the character controller.

            Plugin.Log.LogInfo("FPV Drone spawned! Use your controller or WASD to fly.");
        }

        private void ExitDrone()
        {
            Plugin.Log.LogInfo("Exiting FPV drone mode.");

            IsFlying = false;

            // Restore camera
            RestoreGameCamera();

            // Destroy drone
            if (_droneObject != null)
            {
                Destroy(_droneObject);
                _droneObject = null;
                _physics = null;
            }

            // _fpvCamera is the game camera — don't destroy it, just clear the reference
            _fpvCamera = null;

            // Hide HUD
            if (_hud != null)
            {
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

        private void UpdateFPVCamera()
        {
            if (_fpvCamera == null || _droneObject == null) return;

            // Camera is attached to drone with configurable uptilt
            _fpvCamera.transform.position = _droneObject.transform.position;

            // FPV camera looks forward with an uptilt angle
            // Uptilt means the camera is tilted up from the drone's forward axis
            // so when the drone pitches forward for speed, the camera still sees ahead
            float tilt = Plugin.CameraTiltAngle.Value;
            _fpvCamera.transform.rotation = _droneObject.transform.rotation *
                Quaternion.Euler(-tilt, 0f, 0f);

            // Update FOV in case config changed
            _fpvCamera.fieldOfView = Plugin.CameraFOV.Value;
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
            if (player == null) return;

            // Keep player at saved position to prevent falling etc.
            player.transform.position = _playerPosition;

            // Disable player character movement
            // We'll handle this more elegantly through Harmony patches,
            // but as a fallback, zero out the character velocity
            var character = player.GetComponent<Character>();
            if (character != null)
            {
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
        }

        private void UnfreezePlayer()
        {
            // Player will resume normal control once IsFlying = false
            // The Harmony patches check IsFlying to block input
        }

        private void OnDestroy()
        {
            if (IsFlying) ExitDrone();
        }
    }
}
