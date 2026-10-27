using System;
using MelonLoader;
using Odyssey;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(GearMod), "Odyssey Gear", "1.0.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
{
    public class GearMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> JetpackSecondsPerWood;
        internal static MelonPreferences_Entry<float> JetpackAcceleration;
        internal static MelonPreferences_Entry<float> JetpackMaxSpeed;
        internal static MelonPreferences_Entry<float> JetpackVolume;
        internal static MelonPreferences_Entry<float> LaserVolume;

        private static readonly Action GhostsUpdate = Blueprints.BlueprintGhosts.Update;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyGear", "Odyssey Gear");
            JetpackSecondsPerWood = Settings.Entry(category, "JetpackSecondsPerWood", 20f, "Seconds of jetpack flight per 1 wood. Host only");
            JetpackAcceleration = Settings.Entry(category, "JetpackAcceleration", 6f, "Jetpack upward acceleration above gravity (m/s^2). All players must use the same value");
            JetpackMaxSpeed = Settings.Entry(category, "JetpackMaxSpeed", 7f, "Maximum jetpack climb speed (m/s). All players must use the same value");
            JetpackVolume = Settings.Entry(category, "JetpackFireVolume", 0.45f, "Jetpack flame volume (0-1)");
            LaserVolume = Settings.Entry(category, "LaserVolume", 0.7f, "Laser cannon volume (0-1)");
            category.SaveToFile(false);
            Patches.Registration.Register();
            LoggerInstance.Msg("Press J: jetpack on/off, L: jetpack altitude hold, B: blueprint catalog.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Blueprints.BlueprintGhosts.Clear();
        }

        public override void OnUpdate()
        {
            CoreMod.Run(LoggerInstance, "Blueprint ghosts", GhostsUpdate);
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }
            if (CoreMod.KeyPressed(kb.bKey) && !Blueprints.BlueprintCatalog.IsTyping && (Blueprints.BlueprintCatalog.IsOpen || Cursor.lockState == CursorLockMode.Locked))
            {
                Blueprints.BlueprintCatalog.Toggle();
            }
            if (CoreMod.KeyPressed(kb.lKey) && !Blueprints.BlueprintCatalog.IsTyping && Content.Jetpack.IsUnlocked() && Cursor.lockState == CursorLockMode.Locked)
            {
                Content.Jetpack.Hold = !Content.Jetpack.Hold;
                Content.Jetpack.HoldAirborne = false;
                CoreMod.ShowMessage(Content.Jetpack.Hold ? "Jetpack hold ON - altitude locked, Space up / Crouch down" : "Jetpack hold OFF", 1.5f);
            }
            if (CoreMod.KeyPressed(kb.jKey) && Content.Jetpack.IsUnlocked())
            {
                Content.Jetpack.Enabled = !Content.Jetpack.Enabled;
                CoreMod.ShowMessage(Content.Jetpack.Enabled ? "Jetpack ON" : "Jetpack OFF", 1.5f);
            }
        }
    }
}
