using UnityEngine;

namespace ValheimFPVDrone
{
    /// <summary>
    /// FPV Drone physics controller implementing acro/rate mode flight.
    ///
    /// In acro mode:
    /// - Stick input commands angular velocity (deg/sec), not angle
    /// - No auto-leveling — releasing sticks holds current attitude
    /// - Throttle commands thrust along the drone's local UP axis
    /// - Gravity always pulls down in world space
    /// - Aerodynamic drag opposes motion
    ///
    /// The drone's orientation determines where thrust is directed.
    /// To move forward, the pilot tilts forward (pitch down) so thrust
    /// has a forward component.
    /// </summary>
    public class DronePhysics : MonoBehaviour
    {
        // State
        private Vector3 _velocity = Vector3.zero;
        private Vector3 _angularVelocity = Vector3.zero; // deg/sec in local space
        private float _currentThrust = 0f;
        private float _motorResponse = 0f; // smoothed throttle for motor spinup sim

        // Cached
        private Transform _transform;

        // Properties for HUD
        public Vector3 Velocity => _velocity;
        public float CurrentThrust => _currentThrust;
        public float Speed => _velocity.magnitude;
        public float SpeedKmh => _velocity.magnitude * 3.6f;
        public float Altitude => _transform != null ? _transform.position.y : 0f;
        public float MotorOutput => _motorResponse;
        public Vector3 AngularVelocityDeg => _angularVelocity;
        public float VerticalSpeed => _velocity.y;

        private void Awake()
        {
            _transform = transform;
        }

        public void ResetState()
        {
            _velocity = Vector3.zero;
            _angularVelocity = Vector3.zero;
            _currentThrust = 0f;
            _motorResponse = 0f;
        }

        public void PhysicsUpdate(float throttle, float rollInput, float pitchInput, float yawInput, float dt)
        {
            if (dt <= 0f || dt > 0.1f) dt = 0.02f; // safety clamp

            // ── 1. Calculate desired angular rates from Betaflight rate curves ──
            float desiredRollRate = BetaflightRates.GetRollRate(rollInput);    // deg/sec
            float desiredPitchRate = BetaflightRates.GetPitchRate(pitchInput);
            float desiredYawRate = BetaflightRates.GetYawRate(yawInput);

            // ── 2. Angular velocity with drag (simulates PID response + prop wash) ──
            float angDrag = Plugin.AngularDragCoefficient.Value;

            // Exponential decay toward desired rate — feels like a real FC
            // Higher angular drag = snappier response (faster convergence)
            float angFactor = 1f - Mathf.Exp(-angDrag * dt);

            _angularVelocity.x = Mathf.Lerp(_angularVelocity.x, desiredPitchRate, angFactor);
            _angularVelocity.y = Mathf.Lerp(_angularVelocity.y, desiredYawRate, angFactor);
            _angularVelocity.z = Mathf.Lerp(_angularVelocity.z, -desiredRollRate, angFactor); // negative for correct roll direction

            // ── 3. Apply rotation ──
            // Convert angular velocity (deg/sec) to rotation delta
            Quaternion pitchDelta = Quaternion.AngleAxis(_angularVelocity.x * dt, Vector3.right);
            Quaternion yawDelta = Quaternion.AngleAxis(_angularVelocity.y * dt, Vector3.up);
            Quaternion rollDelta = Quaternion.AngleAxis(_angularVelocity.z * dt, Vector3.forward);

            // Apply in local space: yaw, then pitch, then roll (standard drone convention)
            _transform.localRotation = _transform.localRotation * yawDelta * pitchDelta * rollDelta;

            // ── 4. Motor response (spin-up/spin-down simulation) ──
            float targetThrust = throttle;
            float spinUpRate = 1f / Mathf.Max(Plugin.MotorSpinUpTime.Value, 0.001f);
            _motorResponse = Mathf.MoveTowards(_motorResponse, targetThrust, spinUpRate * dt);

            // ── 5. Calculate thrust force ──
            float idleFraction = Plugin.IdleThrottlePercent.Value / 100f;
            float effectiveThrottle = idleFraction + _motorResponse * (1f - idleFraction);
            _currentThrust = effectiveThrottle * Plugin.MaxThrust.Value;

            // Thrust is along the drone's local UP axis
            Vector3 thrustForce = _transform.up * _currentThrust;

            // ── 6. Gravity ──
            Vector3 gravityForce = Vector3.down * Plugin.Gravity.Value * Plugin.Mass.Value;

            // ── 7. Aerodynamic drag ──
            // Quadratic drag model: F_drag = -0.5 * Cd * v² * direction
            float speedSq = _velocity.sqrMagnitude;
            Vector3 dragForce = Vector3.zero;
            if (speedSq > 0.001f)
            {
                Vector3 velNorm = _velocity.normalized;
                dragForce = -velNorm * Plugin.DragCoefficient.Value * speedSq;
            }

            // ── 8. Integrate forces → acceleration → velocity ──
            float mass = Mathf.Max(Plugin.Mass.Value, 0.01f);
            Vector3 totalForce = thrustForce + gravityForce + dragForce;
            Vector3 acceleration = totalForce / mass;

            _velocity += acceleration * dt;

            // Clamp max speed
            float maxSpeed = Plugin.MaxSpeed.Value;
            if (_velocity.magnitude > maxSpeed)
            {
                _velocity = _velocity.normalized * maxSpeed;
            }

            // ── 9. Integrate velocity → position ──
            Vector3 newPos = _transform.position + _velocity * dt;

            // Ground collision — simple ground plane check
            // In Valheim, terrain height varies. We'll use a simple check and
            // the caller (DroneController) can do proper terrain raycasting.
            if (newPos.y < 0.5f)
            {
                newPos.y = 0.5f;
                if (_velocity.y < 0f)
                {
                    // Crash / ground contact
                    _velocity.y = 0f;
                    // Reduce horizontal speed on ground contact (friction)
                    _velocity.x *= 0.95f;
                    _velocity.z *= 0.95f;
                }
            }

            _transform.position = newPos;
        }

        /// <summary>
        /// Handles terrain collision using Valheim's terrain system.
        /// Call after PhysicsUpdate.
        /// </summary>
        public void HandleTerrainCollision()
        {
            if (_transform == null) return;

            Vector3 pos = _transform.position;

            // Raycast down to find ground
            float groundHeight = 0f;
            bool foundGround = false;

            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out hit, 500f, _terrainMask))
            {
                groundHeight = hit.point.y;
                foundGround = true;
            }

            float minHeight = foundGround ? groundHeight + 0.2f : 0.5f;

            if (pos.y < minHeight)
            {
                pos.y = minHeight;
                _transform.position = pos;

                if (_velocity.y < 0f)
                {
                    float impactSpeed = Mathf.Abs(_velocity.y);
                    _velocity.y = 0f;
                    if (impactSpeed > 5f)
                    {
                        _velocity *= 0.3f;
                        _angularVelocity *= 0.1f;
                    }
                    else
                    {
                        _velocity.x *= 0.9f;
                        _velocity.z *= 0.9f;
                    }
                }
            }

            // ── Obstacle collision (solid world geometry + player-built structures) ──
            if (!Plugin.ObstacleCollision.Value) return;

            // OverlapSphere + ClosestPoint: finds exact separation from each nearby collider.
            // _solidMask targets static_solid (rocks, boulders, tree trunks) and piece (player builds).
            // Foliage, canopy, characters, water and triggers are excluded by the mask.
            const float droneRadius = 0.3f;
            int nearbyCount = Physics.OverlapSphereNonAlloc(pos, droneRadius, _overlapBuffer, _solidMask);
            for (int i = 0; i < nearbyCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;

                Vector3 closest = col.ClosestPoint(pos);
                Vector3 away = pos - closest;
                float dist = away.magnitude;

                // dist == 0: deep inside a non-convex mesh — skip
                if (dist < 0.001f) continue;

                away /= dist;

                float overlap = droneRadius - dist;
                if (overlap > 0f)
                {
                    pos += away * (overlap + 0.02f);
                    _transform.position = pos;
                }

                float into = Vector3.Dot(_velocity, -away);
                if (into > 0f)
                {
                    _velocity += away * into;
                    _velocity *= 0.5f;
                    _angularVelocity *= 0.3f;
                }
            }
        }

        // Terrain-only mask for ground raycasting — only hits the terrain mesh, not rocks/trees/foliage.
        private static readonly int _terrainMask =
            LayerMask.GetMask("terrain");

        // Solid-world mask for obstacle collision — rocks, boulders, tree trunks (static_solid)
        // and player-built structures (piece). Excludes foliage, canopy, characters, triggers, etc.
        private static readonly int _solidMask =
            LayerMask.GetMask("static_solid", "piece");

        // Pre-allocated buffer — avoids GC allocation every frame
        private static readonly Collider[] _overlapBuffer = new Collider[16];

        /// <summary>
        /// Get the angle between drone's up vector and world up.
        /// Used for HUD artificial horizon.
        /// </summary>
        public float GetTiltAngle()
        {
            return Vector3.Angle(_transform.up, Vector3.up);
        }

        /// <summary>
        /// Get heading in degrees (0-360).
        /// </summary>
        public float GetHeading()
        {
            Vector3 forward = _transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return 0f;
            float heading = Quaternion.LookRotation(forward).eulerAngles.y;
            return heading;
        }
    }
}
