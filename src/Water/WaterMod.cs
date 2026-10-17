using System;
using MelonLoader;
using UnityEngine;
using Water;

[assembly: MelonInfo(typeof(WaterMod), "Water", "0.4.6", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Water
{
    public class WaterMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> LakeDepth;
        internal static MelonPreferences_Entry<float> SurfaceRadius;
        internal static MelonPreferences_Entry<bool> ShipPhysics;
        internal static MelonPreferences_Entry<float> ShipBuoyancy;
        internal static MelonPreferences_Entry<float> WaterDrag;
        internal static MelonPreferences_Entry<float> PontoonLift;
        internal static MelonPreferences_Entry<float> UnderwaterCrashSpeed;
        internal static MelonPreferences_Entry<bool> Splashes;
        internal static MelonPreferences_Entry<float> SplashVolume;
        internal static MelonPreferences_Entry<bool> Swimming;
        internal static MelonPreferences_Entry<float> UnderwaterAirSeconds;
        internal static MelonPreferences_Entry<bool> GlassDroplets;
        internal static MelonPreferences_Entry<string> LakeBedTexture;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Water");
            Enabled = category.CreateEntry("Enabled", true, description: "Water in the lakes of Otrera b");
            LakeDepth = category.CreateEntry("LakeDepth", 10f,
                description: "How many meters to lower lake bottoms (0 = flat bottom like the game). All players must use the same value - it changes the terrain");
            SurfaceRadius = category.CreateEntry("SurfaceRadius", 3000f, description: "Radius of the water surface around the camera (m)");
            ShipPhysics = category.CreateEntry("ShipPhysics", true,
                description: "Ships hitting a lake brake hard, bounce off the surface and slowly sink instead of falling to the bottom. Host only");
            ShipBuoyancy = category.CreateEntry("Buoyancy", 0.8f,
                description: "Water lift as a share of ship weight when fully submerged (below 1 = ships sink, above 1 = they float). Host only");
            WaterDrag = category.CreateEntry("WaterDrag", 1f, description: "Water drag multiplier for ships. Host only");
            PontoonLift = category.CreateEntry("PontoonLift", 2000f,
                description: "How much ship mass (kg) one Pontoon (Mining mod part) holds up on the water surface. Host only");
            UnderwaterCrashSpeed = category.CreateEntry("UnderwaterCrashSpeed", 30f,
                description: "Ships in a lake take no collision damage below this speed (m/s), e.g. when settling on the bottom. Host only");
            Splashes = category.CreateEntry("Splashes", true, description: "Splashes, spray and sound when ships hit or move through water");
            SplashVolume = category.CreateEntry("SplashVolume", 0.8f, description: "Splash and wake volume (0-1)");
            LakeBedTexture = category.CreateEntry("LakeBedTexture", "auto",
                description: "Lake bottom texture: auto (wet sand of the planet), off (game texture), or part of a texture name from the log");
            GlassDroplets = category.CreateEntry("GlassDroplets", true, description: "Water droplets on ship windows after a nearby splash or when the glass comes out of the water; they slide down the glass");
            UnderwaterAirSeconds = category.CreateEntry("UnderwaterAirSeconds", 30f, description: "Seconds a full oxygen tank lasts with the head under water; then the player starts drowning (0 = off). Host only");
            Swimming = category.CreateEntry("Swimming", true,
                description: "Players swim in lakes: WASD swims where you look, Space up, Shift faster. All players should use the same value");
            category.SaveToFile(false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ServiceCache.Reset();
            Surface.WaterSurface.Clear();
            Buoyancy.WaterLevel.Clear();
            Buoyancy.ShipWaterPhysics.Clear();
            Effects.Splashes.Clear();
            Effects.GlassDroplets.Clear();
            Diagnostics.WaterDevTest.OnSceneLoaded(sceneName);
        }

        private static bool _surfaceFailed;

        public override void OnUpdate()
        {
            try
            {
                if (Enabled.Value && !_surfaceFailed)
                {
                    Surface.WaterSurface.Update();
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error($"Water surface disabled for this session: {e}");
                _surfaceFailed = true;
            }
            if (Enabled.Value && Splashes.Value)
            {
                Effects.Splashes.Update();
            }
        }
    }
}
