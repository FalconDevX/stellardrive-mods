using System;
using MelonLoader;
using Odyssey;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(TweaksMod), "Odyssey Tweaks", "1.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
{
    public class TweaksMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> KeepNearbyShipsLoaded;
        internal static MelonPreferences_Entry<float> KeepNearbyShipsDistance;
        internal static MelonPreferences_Entry<bool> FastRooms;
        internal static MelonPreferences_Entry<float> DetailDistance;
        internal static MelonPreferences_Entry<bool> DetailShadows;
        internal static MelonPreferences_Entry<bool> FastShipBounds;
        internal static MelonPreferences_Entry<float> PartCullScreenSize;
        internal static MelonPreferences_Entry<bool> GpuOcclusionCulling;
        internal static MelonPreferences_Entry<bool> HideSealedRooms;
        internal static MelonPreferences_Entry<bool> FastCableVisuals;
        internal static MelonPreferences_Entry<bool> FastSignals;

        private static readonly Action CullerUpdate = Diagnostics.DetailCuller.Update;
        private static readonly Action GpuCullingUpdate = Diagnostics.GpuCulling.Update;
        private static readonly Action RoomCullerUpdate = Diagnostics.RoomCuller.Update;
        private static readonly Action ChaseCameraUpdate = Patches.ChaseCameraPatch.Update;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyTweaks", "Odyssey Tweaks");
            DetailDistance = Settings.Entry(category, "DetailDistance", 30f, "Small ship details (cable plugs and cords, socket covers, small bits) are not drawn beyond this distance (m). 0 = always drawn");
            DetailShadows = Settings.Entry(category, "DetailShadows", false, "Small ship details cast shadows (false = faster)");
            HideSealedRooms = Settings.Entry(category, "HideSealedRooms", true, "Parts inside a closed room without windows are not drawn while the camera is outside that room. Opening a door or adding a window shows them again");
            GpuOcclusionCulling = Settings.Entry(category, "GpuOcclusionCulling", false, "Experimental: let the graphics card draw parts and skip what walls hide (Unity GPU resident drawer with occlusion culling). Moves work from the CPU to the GPU. If objects vanish or flicker, set it back to false");
            PartCullScreenSize = Settings.Entry(category, "PartCullScreenSize", 0.01f, "Ship parts (and their shadows) are not drawn when they are smaller than this part of the screen height, e.g. 0.01 = 1%. 0 = always draw like the game");
            FastShipBounds = Settings.Entry(category, "FastShipBounds", true, "Remember ship hull bounds until the hull changes instead of scanning every hull voxel twice per frame");
            FastCableVisuals = Settings.Entry(category, "FastCableVisuals", true, "Rebuild the pipe flow graph for cable animations every 4th frame, signal cable glow every 3rd frame, pipes off screen are not animated");
            FastSignals = Settings.Entry(category, "FastSignals", true, "Remember which parts feed each signal cable until cables or parts change instead of searching every connection of every signal cable each physics step");
            FastRooms = Settings.Entry(category, "FastRooms", true, "Fast room/oxygen recalculation after a ship change (less lag while building). false = like the game. Computed by the host");
            KeepNearbyShipsLoaded = Settings.Entry(category, "KeepNearbyShipsLoaded", true, "Keep the physics of ships within KeepNearbyShipsDistance loaded even when they are in another space, so walking between areas does not rebuild their colliders (removes hitches). Host only");
            KeepNearbyShipsDistance = Settings.Entry(category, "KeepNearbyShipsDistance", 3000f, "Distance (m) for KeepNearbyShipsLoaded. Every ship inside it is simulated all the time, so a smaller value means less CPU work with many ships. Host only");
            category.SaveToFile(false);
        }

        public override void OnUpdate()
        {
            CoreMod.Run(LoggerInstance, "Detail culler", CullerUpdate);
            CoreMod.Run(LoggerInstance, "GPU culling", GpuCullingUpdate);
            CoreMod.Run(LoggerInstance, "Room culler", RoomCullerUpdate);
            CoreMod.Run(LoggerInstance, "Chase camera", ChaseCameraUpdate);
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f11Key.wasPressedThisFrame)
            {
                Diagnostics.FrameProfiler.Start();
            }
        }
    }
}
