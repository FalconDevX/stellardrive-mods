using System;
using MelonLoader;
using UnityEngine;
using Water;

[assembly: MelonInfo(typeof(WaterMod), "Water", "0.1.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Water
{
    // Prawdziwa woda w akwenach Otrera b. W grze "jeziora" to tylko teren pomalowany na niebiesko poniżej 64,2 m
    // i ucięty płasko na 64 m. Mod obniża dno akwenów (Terrain/LakeTerrainPatch) i kładzie na poziomie 64 m taflę
    // wody, która podąża za kamerą (Surface/WaterSurface).
    public class WaterMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> LakeDepth;
        internal static MelonPreferences_Entry<float> SurfaceRadius;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Water");
            Enabled = category.CreateEntry("Enabled", true, description: "Woda w akwenach Otrera b");
            LakeDepth = category.CreateEntry("LakeDepth", 10f,
                description: "O ile metrów obniżyć dno akwenów (0 = płaskie dno jak w grze). Wszyscy gracze muszą mieć to samo - zmienia teren");
            SurfaceRadius = category.CreateEntry("SurfaceRadius", 3000f, description: "Promień tafli wody wokół kamery (m)");
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
                LoggerInstance.Error($"Błąd wody: {e}");
                Enabled.Value = false;
            }
        }
    }
}
