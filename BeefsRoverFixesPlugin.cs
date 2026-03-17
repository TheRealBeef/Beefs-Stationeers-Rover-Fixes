using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BeefsRoverFixes
{
    public enum AdditionalGravityMode
    {
        Vanilla,
        AddPercentEarthGravity
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BeefsRoverFixesPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> MaxSpeed;
        public static ConfigEntry<float> MotorPower;
        public static ConfigEntry<float> BrakePower;
        public static ConfigEntry<float> TractionMultiplier;
        public static ConfigEntry<bool> SeatColliderFix;
        public static ConfigEntry<AdditionalGravityMode> AdditionalGravity;
        public static ConfigEntry<float> AdditionalGravityStrength;
        public static ConfigEntry<bool> StormImmunity;
        public static ConfigEntry<float> StormDamageScaling;
        public static ConfigEntry<float> StormWindScaling;
        public static ConfigEntry<bool> CameraOrbitEnabled;
        public static ConfigEntry<float> CameraOrbitSensitivity;
        public static ConfigEntry<float> CameraMinZoom;
        public static ConfigEntry<float> CameraMaxZoom;
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            MaxSpeed = Config.Bind("General",
                "MaxSpeed",
                40.0f,
                "Maximum rover speed. Vanilla value is 7.0");

            MotorPower = Config.Bind("General",
                "MotorPower",
                40.0f,
                "Rover motor power/torque. Vanilla value is 20.0");

            BrakePower = Config.Bind("General",
                "BrakePower",
                20.0f,
                "Rover brake power. Vanilla value is 5.0");

            TractionMultiplier = Config.Bind("General",
                "TractionMultiplier",
                3f,
                "Traction multiplier to reduce sliding. Higher values = more grip. 1.0 = vanilla");

            SeatColliderFix = Config.Bind("General",
                "SeatColliderFix",
                true,
                "Fix oversized seat interaction colliders that interfere with buttons. " +
                "Disable this once the game fixes it officially.");

            AdditionalGravity = Config.Bind("Physics",
                "AdditionalGravity",
                AdditionalGravityMode.AddPercentEarthGravity,
                "Additional gravity for rover stability.\n" +
                "Vanilla = no extra gravity (game default).\n" +
                "AddPercentEarthGravity = add a percentage of Earth gravity (below).");

            AdditionalGravityStrength = Config.Bind("Physics",
                "AdditionalGravityStrength",
                0.25f,
                new ConfigDescription(
                    "Percentage of Earth gravity (9.8 m/s²) to add. " +
                    "0.0 = none, 1.0 = 100%, 2.0 = 200%.",
                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            StormImmunity = Config.Bind("Storm",
                "StormImmunity",
                false,
                "Make rovers immune to storm wind forces and weather damage. " +
                "When enabled, storms will not push rovers or damage them.");

            StormDamageScaling = Config.Bind("Storm",
                "StormDamageScaling",
                1.0f,
                "Scale damage done to rovers during storm. " +
                "Percentage scale where 1 = 100% damage.");

            StormWindScaling = Config.Bind("Storm",
                "StormWindScaling",
                1.0f,
                "Scale storm wind force on rovers. " +
                "Percentage scale where 1 = 100% force.");

            CameraOrbitEnabled = Config.Bind("Camera",
                "CameraOrbitEnabled",
                true,
                "Enable free mouse-controlled orbit camera when in a rover in third person. " +
                "Vanilla locks the camera to a fixed point on the vehicle. " +
                "When enabled, you can orbit the camera around the rover with the mouse. " +
                "Middle-click resets the orbit to the default position behind the rover.");

            CameraOrbitSensitivity = Config.Bind("Camera",
                "CameraOrbitSensitivity",
                1.5f,
                new ConfigDescription(
                    "Mouse sensitivity for the orbit camera. Higher values = faster rotation.",
                    new AcceptableValueRange<float>(0.5f, 10.0f)));

            CameraMinZoom = Config.Bind("Camera",
                "CameraMinZoom",
                11.5f,
                new ConfigDescription(
                    "Minimum zoom level (smaller = closer to rover). " +
                    "Scrolling in past this switches to first person.",
                    new AcceptableValueRange<float>(0.2f, 5.0f)));

            CameraMaxZoom = Config.Bind("Camera",
                "CameraMaxZoom",
                5.0f,
                new ConfigDescription(
                    "Maximum zoom level (larger = further from rover). " +
                    "Scroll adjusts within the min/max range.",
                    new AcceptableValueRange<float>(1.0f, 10.0f)));

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
            // Log.LogInfo($"MaxSpeed: {MaxSpeed.Value}, MotorPower: {MotorPower.Value}, BrakePower: {BrakePower.Value}, TractionMultiplier: {TractionMultiplier.Value}");

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo("Patched with Harmony");
        }
    }
}