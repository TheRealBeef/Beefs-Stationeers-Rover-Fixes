using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BeefsRoverFixes
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BeefsRoverFixesPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> MaxSpeed;
        public static ConfigEntry<float> MotorPower;
        public static ConfigEntry<float> BrakePower;
        public static ConfigEntry<float> TractionMultiplier;
        public static ConfigEntry<bool> SeatColliderFix;
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
                "Fix oversized seat interaction colliders that interfere with buttons. Disable this once the game fixes it officially.");


            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
            Log.LogInfo($"MaxSpeed: {MaxSpeed.Value}, MotorPower: {MotorPower.Value}, BrakePower: {BrakePower.Value}, TractionMultiplier: {TractionMultiplier.Value}");

            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo("Patched with Harmony");
        }
    }
}