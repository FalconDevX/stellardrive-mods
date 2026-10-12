using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.Terrain;

namespace Water.Terrain
{
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
            Melon<WaterMod>.Logger.Msg($"Lakes on {planet.name}: bottom down to {SeaLevel - depth:0} m (water at {SeaLevel} m)");
        }
    }
}
