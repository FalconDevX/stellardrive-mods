using System;
using MelonLoader;
using UnityEngine;
using Water;

[assembly: MelonInfo(typeof(WaterMod), "Water", "0.1.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Water
{
    public class WaterMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> LakeDepth;
        internal static MelonPreferences_Entry<float> SurfaceRadius;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Water");
            Enabled = category.CreateEntry("Enabled", true, description: "Water in the lakes of Otrera b");
            LakeDepth = category.CreateEntry("LakeDepth", 10f,
                description: "How many meters to lower lake bottoms (0 = flat bottom like the game). All players must use the same value - it changes the terrain");
            SurfaceRadius = category.CreateEntry("SurfaceRadius", 3000f, description: "Radius of the water surface around the camera (m)");
            category.SaveToFile(false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Surface.WaterSurface.Clear();
            Diagnostics.WaterDevTest.OnSceneLoaded(sceneName);
        }

        public override void OnUpdate()
        {
            try
            {
                if (Enabled.Value)
                {
                    Surface.WaterSurface.Update();
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error($"Water error: {e}");
                Enabled.Value = false;
            }
        }
    }
}
