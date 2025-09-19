using Assets.Scripts.Vehicles;
using Assets.Scripts.Networking;
using HarmonyLib;
using UnityEngine;

namespace BeefsRoverFixes
{
    [HarmonyPatch(typeof(Rover))]
    public static class RoverPatcher
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void RoverHandlingPatch(Rover __instance)
        {
            // Don't modify on clients for now
            if (NetworkManager.NetworkRole == NetworkRole.Client)
            {
                return;
            }

            BeefsRoverFixesPlugin.Log.LogInfo($"Applying rover stuff to {__instance.name}");

            __instance.MaxSpeed = BeefsRoverFixesPlugin.MaxSpeed.Value;
            __instance.MotorPower = BeefsRoverFixesPlugin.MotorPower.Value;
            __instance.BrakePower = BeefsRoverFixesPlugin.BrakePower.Value;

            ApplyTractionFixes(__instance);

            if (BeefsRoverFixesPlugin.SeatColliderFix.Value)
            {
                ApplySeatColliderFix(__instance);
            }

            BeefsRoverFixesPlugin.Log.LogInfo($"Rover modifications: MaxSpeed={__instance.MaxSpeed}, MotorPower={__instance.MotorPower}, BrakePower={__instance.BrakePower}");
        }

        private static void ApplyTractionFixes(Rover rover)
        {
            float tractionMultiplier = BeefsRoverFixesPlugin.TractionMultiplier.Value;
            BeefsRoverFixesPlugin.Log.LogInfo($"Applying traction fixes - multiplier {tractionMultiplier}x");

            if (rover.Wheels != null && rover.Wheels.Count > 0)
            {
                BeefsRoverFixesPlugin.Log.LogInfo($"Found {rover.Wheels.Count} wheels");

                foreach (var wheel in rover.Wheels)
                {
                    if (wheel.WheelCollider != null)
                    {
                        var wheelCollider = wheel.WheelCollider;

                        // Get current friction curves
                        var forwardFriction = wheelCollider.forwardFriction;
                        var sidewaysFriction = wheelCollider.sidewaysFriction;

                        BeefsRoverFixesPlugin.Log.LogInfo($"Original - Forward: stiffness={forwardFriction.stiffness}, slip={forwardFriction.asymptoteSlip}");
                        BeefsRoverFixesPlugin.Log.LogInfo($"Original - Sideways: stiffness={sidewaysFriction.stiffness}, slip={sidewaysFriction.asymptoteSlip}");

                        // Increase stiffness
                        forwardFriction.stiffness *= tractionMultiplier;
                        sidewaysFriction.stiffness *= tractionMultiplier;

                        // Reduce slip thresholds
                        forwardFriction.asymptoteSlip = Mathf.Max(0.05f, forwardFriction.asymptoteSlip / tractionMultiplier);
                        sidewaysFriction.asymptoteSlip = Mathf.Max(0.05f, sidewaysFriction.asymptoteSlip / tractionMultiplier);

                        // Apply
                        wheelCollider.forwardFriction = forwardFriction;
                        wheelCollider.sidewaysFriction = sidewaysFriction;

                        BeefsRoverFixesPlugin.Log.LogInfo($"Modified - Forward: stiffness={forwardFriction.stiffness}, slip={forwardFriction.asymptoteSlip}");
                        BeefsRoverFixesPlugin.Log.LogInfo($"Modified - Sideways: stiffness={sidewaysFriction.stiffness}, slip={sidewaysFriction.asymptoteSlip}");
                    }
                }
            }
            else
            {
                BeefsRoverFixesPlugin.Log.LogWarning("No wheels found");
            }

        }

        private static void ApplySeatColliderFix(Rover rover)
        {
            BeefsRoverFixesPlugin.Log.LogInfo("Applying seat collider fix...");

            int totalFixed = 0;
            var allColliders = rover.GetComponentsInChildren<Collider>();
            foreach (var collider in allColliders)
            {
                string name = collider.name.ToLower();
                if (name.Contains("boxcollidertriggerslot") && name.Contains("entity") && collider.isTrigger)
                {
                    if (ShrinkCollider(collider, $"Seat Collider ({collider.name})"))
                    {
                        totalFixed++;
                    }
                }
            }

            BeefsRoverFixesPlugin.Log.LogInfo($"modified {totalFixed} colliders total");
        }

        private static bool ShrinkCollider(Collider collider, string debugName)
        {
            const float shrinkFactor = 0.3f;

            if (collider is BoxCollider boxCollider)
            {
                var originalSize = boxCollider.size;
                var originalCenter = boxCollider.center;

                var newSize = originalSize * shrinkFactor;

                var heightDifference = originalSize.y - newSize.y;
                var newCenter = new Vector3(
                    originalCenter.x,
                    originalCenter.y - heightDifference * 0.5f,
                    originalCenter.z
                );

                boxCollider.size = newSize;
                boxCollider.center = newCenter;

                BeefsRoverFixesPlugin.Log.LogInfo($"Shrunk BoxCollider on {debugName}: size {originalSize} → {newSize}, center {originalCenter} → {newCenter}");
                return true;
            }

            return false;
        }
    }
}