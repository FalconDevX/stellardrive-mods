using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.Terrain;

namespace Water.Terrain
{
    // Teren Otrera b: noise -> kolor (poniżej 70 m piasek, poniżej 64,2 m "lód" = akwen) -> ucięcie do min 64 m.
    // Obniżamy ucięcie o LakeDepth, żeby akweny miały prawdziwą głębokość (dno łagodnie schodzi od brzegu);
    // wygląd dna zostaje jak w grze. Przed zbudowaniem terenu.
    [HarmonyPatch(typeof(PlanetTerrain), "Awake")]
    internal static class LakeTerrainPatch
    {
        internal const float SeaLevel = 64f;

        private static void Postfix(PlanetTerrain __instance)
        {
            var planet = __instance.GetComponent<Planet>();
            if (planet == null || planet.Id != 4 || !WaterMod.Enabled.Value || __instance.terrainConfig?.clampSteps == null)
            {
                return;
            }
            float depth = UnityEngine.Mathf.Clamp(WaterMod.LakeDepth.Value, 0f, 60f);
            foreach (var clamp in __instance.terrainConfig.clampSteps)
            {
                if (UnityEngine.Mathf.Abs(clamp.min - SeaLevel) < 1f)
                {
                    clamp.min = SeaLevel - depth;
                }
            }
            Melon<WaterMod>.Logger.Msg($"Akweny na {planet.name}: dno do {SeaLevel - depth:0} m (woda na {SeaLevel} m)");
        }
    }
}
