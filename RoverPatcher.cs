using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Util;
using Assets.Scripts.Vehicles;
using HarmonyLib;
using UnityEngine;

namespace BeefsRoverFixes
{
    [HarmonyPatch(typeof(Rover))]
    public static class RoverPatcher
    {
        private const float ReversalRPMThreshold = 10f;
        private static bool _gravityLoggedOnce = false;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(Rover __instance)
        {
            if (NetworkManager.NetworkRole == NetworkRole.Client)
                return;

            BeefsRoverFixesPlugin.Log.LogInfo($"Applying rover fixes to {__instance.name}");

            __instance.MaxSpeed = BeefsRoverFixesPlugin.MaxSpeed.Value;
            __instance.MotorPower = BeefsRoverFixesPlugin.MotorPower.Value;
            __instance.BrakePower = BeefsRoverFixesPlugin.BrakePower.Value;

            ApplyTractionFixes(__instance);

            if (BeefsRoverFixesPlugin.SeatColliderFix.Value)
                ApplySeatColliderFix(__instance);

            BeefsRoverFixesPlugin.Log.LogInfo(
                $"Rover modifications: MaxSpeed={__instance.MaxSpeed}, " +
                $"MotorPower={__instance.MotorPower}, BrakePower={__instance.BrakePower}");
        }

        private static void ApplyTractionFixes(Rover rover)
        {
            float tractionMultiplier = BeefsRoverFixesPlugin.TractionMultiplier.Value;
            BeefsRoverFixesPlugin.Log.LogInfo($"Applying traction fixes - multiplier {tractionMultiplier}x");

            if (rover.Wheels == null || rover.Wheels.Count == 0)
            {
                BeefsRoverFixesPlugin.Log.LogWarning("No wheels found");
                return;
            }

            BeefsRoverFixesPlugin.Log.LogInfo($"Found {rover.Wheels.Count} wheels");

            foreach (var wheel in rover.Wheels)
            {
                if (wheel.WheelCollider == null)
                    continue;

                var wc = wheel.WheelCollider;

                var fwd = wc.forwardFriction;
                var side = wc.sidewaysFriction;

                fwd.stiffness *= tractionMultiplier;
                side.stiffness *= tractionMultiplier;

                fwd.asymptoteSlip = Mathf.Max(0.05f, fwd.asymptoteSlip / tractionMultiplier);
                side.asymptoteSlip = Mathf.Max(0.05f, side.asymptoteSlip / tractionMultiplier);

                wc.forwardFriction = fwd;
                wc.sidewaysFriction = side;
            }
        }

        private static void ApplySeatColliderFix(Rover rover)
        {
            BeefsRoverFixesPlugin.Log.LogInfo("Applying seat collider fix...");

            int totalFixed = 0;
            foreach (var collider in rover.GetComponentsInChildren<Collider>())
            {
                string name = collider.name.ToLower();
                if (name.Contains("boxcollidertriggerslot") && name.Contains("entity")
                    && collider.isTrigger && collider is BoxCollider box)
                {
                    const float shrinkFactor = 0.3f;
                    var origSize = box.size;
                    var origCenter = box.center;
                    var newSize = origSize * shrinkFactor;
                    var heightDiff = origSize.y - newSize.y;

                    box.size = newSize;
                    box.center = new Vector3(origCenter.x, origCenter.y - heightDiff * 0.5f, origCenter.z);
                    totalFixed++;

                    BeefsRoverFixesPlugin.Log.LogInfo(
                        $"Shrunk seat collider {collider.name}: {origSize} → {newSize}");
                }
            }

            BeefsRoverFixesPlugin.Log.LogInfo($"Modified {totalFixed} seat colliders");
        }

        [HarmonyPatch("HandleRoverMovement")]
        [HarmonyPostfix]
        public static void HandleRoverMovement_Postfix(Rover __instance)
        {
            bool wantFwd = KeyManager.GetButton(KeyMap.Forward);
            bool wantBwd = KeyManager.GetButton(KeyMap.Backward);

            float avgRPM = 0f;
            int count = 0;
            if (__instance.Wheels != null)
            {
                foreach (var wheel in __instance.Wheels)
                {
                    if (wheel.WheelCollider != null)
                    {
                        avgRPM += wheel.WheelCollider.rpm;
                        count++;
                    }
                }
                if (count > 0) avgRPM /= count;
            }

            var t = Traverse.Create(__instance);

            float targetMotor = t.Property("TargetMotorPower").GetValue<float>();
            float currentMotor = t.Field("CurrentMotorPower").GetValue<float>();
            bool inputOpposesTarget = (wantBwd && targetMotor > 0.01f)
                                   || (wantFwd && targetMotor < -0.01f);

            if (inputOpposesTarget)
            {
                t.Property("TargetMotorPower").SetValue(0f);
                t.Field("CurrentMotorPower").SetValue(0f);
            }

            if (!inputOpposesTarget)
            {
                bool signMismatch = (targetMotor > 0f && currentMotor < 0f)
                                 || (targetMotor < 0f && currentMotor > 0f);

                if (signMismatch || Mathf.Abs(targetMotor) < 0.01f)
                {
                    t.Field("CurrentMotorPower").SetValue(0f);
                }
            }

            bool reversingFwd = avgRPM > ReversalRPMThreshold && wantBwd;
            bool reversingBwd = avgRPM < -ReversalRPMThreshold && wantFwd;

            if (reversingFwd || reversingBwd)
            {
                t.Property("TargetBrakePower").SetValue(__instance.BrakePower);
                t.Field("CurrentBrakePower").SetValue(__instance.BrakePower);
            }
        }

        [HarmonyPatch("PhysicsUpdate")]
        [HarmonyPostfix]
        public static void PhysicsUpdate_Postfix(Rover __instance)
        {
            AdditionalGravityMode mode = BeefsRoverFixesPlugin.AdditionalGravity.Value;
            if (mode == AdditionalGravityMode.Vanilla)
                return;

            if (__instance.RigidBody == null || __instance.RigidBody.isKinematic)
                return;

            float strength = BeefsRoverFixesPlugin.AdditionalGravityStrength.Value;
            if (strength < 0.01f)
                return;

            float worldGrav = Mathf.Abs(WorldManager.WorldGravity);
            const float EarthGravity = 9.8f;

            float targetGrav = (strength * EarthGravity * 2f + worldGrav) / 3f;
            float extraGrav = targetGrav - worldGrav;
            if (extraGrav > 0.01f)
            {
                __instance.RigidBody.AddForce(Vector3.down * extraGrav, ForceMode.Acceleration);

                if (!_gravityLoggedOnce)
                {
                    BeefsRoverFixesPlugin.Log.LogInfo(
                        $"Additional gravity active: mode={mode}, worldGrav={worldGrav:F2}, " +
                        $"targetGrav={targetGrav:F2}, extra={extraGrav:F2}");
                    _gravityLoggedOnce = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(DynamicThing))]
    public static class StormPatcher
    {
        [HarmonyPatch("CanBeExposedToStorm")]
        [HarmonyPrefix]
        public static bool CanBeExposedToStorm_Prefix(DynamicThing __instance, ref bool __result)
        {
            if (BeefsRoverFixesPlugin.StormImmunity.Value && __instance is Rover)
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch("CanBeWeathered")]
        [HarmonyPrefix]
        public static bool CanBeWeathered_Prefix(DynamicThing __instance, ref bool __result)
        {
            if (BeefsRoverFixesPlugin.StormImmunity.Value && __instance is Rover)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CameraController))]
    public static class CameraPatcher
    {
        private static float _orbitYaw;
        private static float _orbitPitch = DefaultPitch;
        private static float _lastRoverYaw;
        private static Rover _lastRover;
        private static float _currentZoom = 1.0f;
        private static float _collisionDist;
        private static float _noInputTimer;

        private static readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        private const float DefaultPitch = 25f;
        private const float MinPitch = -10f;
        private const float MaxPitch = 80f;
        private const float OrbitCenterHeight = 1.5f;
        private const float CollisionSphereRadius = 0.25f;
        private const float CollisionPadding = 0.3f;
        private const float MinCameraDistance = 0.5f;
        private const float CollisionSnapSpeed = 25f;
        private const float CollisionEaseSpeed = 8f;
        private const float AutoReturnLerpSpeed = 2.0f;
        private const float AutoReturnDelay = 6.0f;
        private const float AutoReturnSpeedThreshold = 0.3f;

        [HarmonyPatch("CacheCameraPosition")]
        [HarmonyPrefix]
        public static void CacheCameraPosition_Prefix()
        {
            if (!CameraController.IsThirdPerson)
                return;

            var entity = InventoryManager.Parent;
            if (entity == null)
                return;

            var parentSlot = entity.ParentSlot;
            if (parentSlot == null)
                return;

            if (!(parentSlot.Parent is Rover))
                return;

            if (_currentZoom <= BeefsRoverFixesPlugin.CameraMinZoom.Value)
                ThirdPersonOrbitCam.zoomOffset.z = 1.5f;
            else
                ThirdPersonOrbitCam.zoomOffset.z = 1.0f;
        }

        [HarmonyPatch("CacheCameraPosition")]
        [HarmonyPostfix]
        public static void CacheCameraPosition_Postfix(CameraController __instance)
        {
            if (!CameraController.IsThirdPerson)
                return;

            var entity = InventoryManager.Parent;
            if (entity == null)
                return;

            var parentSlot = entity.ParentSlot;
            if (parentSlot == null)
                return;

            var rover = parentSlot.Parent as Rover;
            if (rover == null)
                return;

            var cameraTransform = __instance.MainCameraTransform;
            if (cameraTransform == null)
                return;

            bool orbitEnabled = BeefsRoverFixesPlugin.CameraOrbitEnabled.Value;

            if (!orbitEnabled
                && Mathf.Approximately(_currentZoom, 1.0f)
                && Mathf.Abs(Input.mouseScrollDelta.y) < 0.01f)
                return;

            if (rover != _lastRover)
            {
                _orbitYaw = rover.transform.eulerAngles.y;
                _orbitPitch = DefaultPitch;
                _lastRoverYaw = rover.transform.eulerAngles.y;
                _lastRover = rover;
                _currentZoom = BeefsRoverFixesPlugin.CameraMinZoom.Value;
                _collisionDist = float.MaxValue;
                _noInputTimer = 0f;
            }

            bool inputSuppressed = Cursor.lockState != CursorLockMode.Locked;

            if (!inputSuppressed && KeyManager.GetButton(KeyMap.ThirdPersonControl))
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float speed = 1.0f;
                    _currentZoom -= scroll * speed * 0.15f;
                    _currentZoom = Mathf.Clamp(_currentZoom,
                        BeefsRoverFixesPlugin.CameraMinZoom.Value,
                        BeefsRoverFixesPlugin.CameraMaxZoom.Value);
                }
            }

            Vector3 roverPos = rover.transform.position;
            Vector3 vanillaCamPos = cameraTransform.position;
            Vector3 vanillaOffset = vanillaCamPos - roverPos;
            float baseDistance = vanillaOffset.magnitude;
            Vector3 orbitCenter = roverPos + Vector3.up * OrbitCenterHeight;

            if (baseDistance < 0.01f)
                return;

            float desiredDistance = baseDistance * _currentZoom;
            Vector3 camDirection;

            if (orbitEnabled)
            {
                if (!inputSuppressed && Input.GetMouseButtonDown(2))
                {
                    _orbitYaw = rover.transform.eulerAngles.y;
                    _orbitPitch = DefaultPitch;
                    _currentZoom = BeefsRoverFixesPlugin.CameraMinZoom.Value;
                    desiredDistance = baseDistance * _currentZoom;
                    _noInputTimer = 0f;
                }

                float roverYaw = rover.transform.eulerAngles.y;
                float roverYawDelta = Mathf.DeltaAngle(_lastRoverYaw, roverYaw);
                _orbitYaw += roverYawDelta;
                _lastRoverYaw = roverYaw;

                float lookX = 0f;
                float lookY = 0f;
                if (!inputSuppressed)
                {
                    float sensitivity = BeefsRoverFixesPlugin.CameraOrbitSensitivity.Value;
                    lookX = Singleton<InputManager>.Instance.GetAxis("LookX");
                    lookY = Singleton<InputManager>.Instance.GetAxis("LookY");
                    _orbitYaw += lookX * sensitivity;
                    _orbitPitch -= lookY * sensitivity;
                    _orbitPitch = Mathf.Clamp(_orbitPitch, MinPitch, MaxPitch);
                }

                {
                    bool hasInput = Mathf.Abs(lookX) > 0.2f || Mathf.Abs(lookY) > 0.2f;
                    float forwardSpeed = rover.RigidBody != null
                        ? Vector3.Dot(rover.RigidBody.velocity, rover.transform.forward)
                        : 0f;
                    bool movingForward = forwardSpeed
                        >= BeefsRoverFixesPlugin.MaxSpeed.Value * AutoReturnSpeedThreshold;

                    if (hasInput || !movingForward)
                        _noInputTimer = 0f;
                    else
                        _noInputTimer += Time.deltaTime;

                    if (_noInputTimer > AutoReturnDelay)
                    {
                        float t = Time.deltaTime * AutoReturnLerpSpeed;
                        _orbitYaw = Mathf.LerpAngle(_orbitYaw, rover.transform.eulerAngles.y, t);
                        _orbitPitch = Mathf.Lerp(_orbitPitch, DefaultPitch, t);
                    }
                }

                Quaternion rotation = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
                camDirection = rotation * Vector3.back;
            }
            else
            {
                Vector3 fixedDesiredPos = roverPos + vanillaOffset.normalized * desiredDistance;
                Vector3 toCamera = fixedDesiredPos - orbitCenter;
                float toCameraMag = toCamera.magnitude;

                if (toCameraMag < 0.01f)
                    return;

                camDirection = toCamera / toCameraMag;
                desiredDistance = toCameraMag;

                _lastRoverYaw = rover.transform.eulerAngles.y;
            }

            float finalDistance = desiredDistance;

            if (desiredDistance > MinCameraDistance)
            {
                float safeDist = GetSafeDistance(orbitCenter, camDirection, desiredDistance,
                    rover.transform, entity.transform);

                if (_collisionDist > desiredDistance)
                    _collisionDist = desiredDistance;

                if (safeDist < desiredDistance - 0.05f)
                {
                    float target = Mathf.Max(MinCameraDistance, safeDist);
                    _collisionDist = Mathf.Lerp(_collisionDist, target,
                        Time.deltaTime * CollisionSnapSpeed);
                }
                else if (_collisionDist < desiredDistance - 0.05f)
                {
                    _collisionDist = Mathf.Lerp(_collisionDist, desiredDistance,
                        Time.deltaTime * CollisionEaseSpeed);
                }
                else
                {
                    _collisionDist = desiredDistance;
                }

                finalDistance = Mathf.Min(desiredDistance, _collisionDist);
            }
            else
            {
                _collisionDist = desiredDistance;
            }

            Vector3 newCamPos = orbitCenter + camDirection * finalDistance;
            cameraTransform.position = newCamPos;

            if (orbitEnabled)
                cameraTransform.LookAt(orbitCenter);

            CameraController.CameraPosition = newCamPos;
            CameraController.EffectiveCameraPosition = newCamPos;
            Traverse.Create(__instance).Property("MainCameraPosition").SetValue(newCamPos);
        }

        private static float GetSafeDistance(Vector3 origin, Vector3 direction,
            float desiredDistance, Transform roverTransform, Transform playerTransform)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin, CollisionSphereRadius, direction, _hitBuffer,
                desiredDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float closestHit = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var col = _hitBuffer[i].collider;

                if (col.transform.IsChildOf(roverTransform))
                    continue;

                if (playerTransform != null && col.transform.IsChildOf(playerTransform))
                    continue;

                if (_hitBuffer[i].distance < closestHit)
                    closestHit = _hitBuffer[i].distance;
            }

            if (closestHit < float.MaxValue)
                return Mathf.Max(MinCameraDistance, closestHit - CollisionPadding);

            return desiredDistance;
        }
    }
}