using MelonLoader;
using Mining;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MiningMod), "Mining", "0.14.15", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Mining
{
    public class MiningMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> SecondsPerItem;
        internal static MelonPreferences_Entry<float> PowerWatts;
        internal static MelonPreferences_Entry<float> SiteSpacing;
        internal static MelonPreferences_Entry<float> SiteChance;
        internal static MelonPreferences_Entry<float> SiteRadius;
        internal static MelonPreferences_Entry<float> UraniumChance;
        internal static MelonPreferences_Entry<bool> ShowSites;
        internal static MelonPreferences_Entry<float> DrillVolume;
        internal static MelonPreferences_Entry<float> HydrogenThrustMultiplier;
        internal static MelonPreferences_Entry<float> RotorSpeed;
        internal static MelonPreferences_Entry<float> RotorLeverAngle;
        internal static MelonPreferences_Entry<bool> RotorCollision;
        internal static MelonPreferences_Entry<bool> KeepNearbyShipsLoaded;
        internal static MelonPreferences_Entry<float> JetpackSecondsPerWood;
        internal static MelonPreferences_Entry<float> JetpackAcceleration;
        internal static MelonPreferences_Entry<float> JetpackMaxSpeed;
        internal static MelonPreferences_Entry<float> JetpackVolume;
        internal static MelonPreferences_Entry<bool> FastRooms;
        internal static MelonPreferences_Entry<float> ReactorPower;
        internal static MelonPreferences_Entry<float> ReactorSecondsPerUranium;
        internal static MelonPreferences_Entry<float> PumpRate;
        internal static MelonPreferences_Entry<float> PumpHoseLength;
        internal static MelonPreferences_Entry<float> PontoonLeakPerMinute;
        internal static MelonPreferences_Entry<float> LaserVolume;
        internal static MelonPreferences_Entry<float> PumpVolume;
        internal static MelonPreferences_Entry<float> ReactorVolume;
        internal static MelonPreferences_Entry<float> ElectrolyzerVolume;
        internal static MelonPreferences_Entry<float> DoorVolume;
        internal static MelonPreferences_Entry<float> DetailDistance;
        internal static MelonPreferences_Entry<bool> DetailShadows;
        internal static MelonPreferences_Entry<bool> FastShipBounds;
        internal static MelonPreferences_Entry<bool> FastCableVisuals;
        internal static MelonPreferences_Entry<string> DevAutoLoadWorld;
        internal static MelonPreferences_Entry<bool> DevDump;
        internal static MelonPreferences_Entry<bool> DevQuitAfterDump;
        internal static MelonPreferences_Entry<bool> DevTest;
        internal static MelonPreferences_Entry<bool> DevPerf;
        internal static MelonPreferences_Entry<bool> DevJetpack;

        private static string _message;
        private static float _messageUntil;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Mining");
            category.DeleteEntry("SecondsPerItem");
            SecondsPerItem = category.CreateEntry("SecondsPerOre", 1f, description: "Seconds per 1 ore mined by the excavator (at full power). Host only");
            PowerWatts = category.CreateEntry("PowerWatts", 1500f, description: "Excavator power draw (W). The wood generator gives 4000 W");
            SiteSpacing = category.CreateEntry("SiteSpacing", 300f,
                description: "Deposit grid cell size (m) - each cell can hold one deposit. Less = denser deposits. All players must use the same value");
            SiteChance = category.CreateEntry("SiteChance", 0.5f, description: "Chance of a deposit in a cell (0-1). All players must use the same value");
            SiteRadius = category.CreateEntry("SiteRadius", 5f, description: "Deposit radius (m). All players must use the same value");
            category.DeleteEntry("UraniumChance");
            UraniumChance = category.CreateEntry("UraniumShare", 0.3f, description: "Share of Otrera b deposits that are uranium (0-1). All players must use the same value");
            ShowSites = category.CreateEntry("ShowSites", true, description: "Show deposits (fields of ore rocks)");
            DrillVolume = category.CreateEntry("DrillVolume", 0.8f, description: "Drill volume (0-1)");
            ReactorPower = category.CreateEntry("ReactorPower", 20000f, description: "Reactor power (W). The wood generator gives 4000 W. Host only");
            ReactorSecondsPerUranium = category.CreateEntry("ReactorSecondsPerUranium", 120f, description: "Seconds of full-power operation per 1 uranium. Host only");
            PumpRate = category.CreateEntry("PumpRate", 1f, description: "Water per second pumped at full power. Host only");
            PumpVolume = category.CreateEntry("PumpVolume", 0.7f, description: "Water pump volume (0-1)");
            ReactorVolume = category.CreateEntry("ReactorVolume", 0.6f, description: "Running reactor volume (0-1)");
            ElectrolyzerVolume = category.CreateEntry("ElectrolyzerVolume", 0.25f, description: "Electrolyzer volume while working (0-1)");
            DoorVolume = category.CreateEntry("DoorVolume", 0.8f, description: "Large door volume (0-1)");
            DetailDistance = category.CreateEntry("DetailDistance", 30f, description: "Small ship details (cable plugs and cords, socket covers, small bits) are not drawn beyond this distance (m). 0 = always drawn");
            DetailShadows = category.CreateEntry("DetailShadows", false, description: "Small ship details cast shadows (false = faster)");
            FastShipBounds = category.CreateEntry("FastShipBounds", true, description: "Remember ship hull bounds until the hull changes instead of scanning every hull voxel twice per frame");
            FastCableVisuals = category.CreateEntry("FastCableVisuals", true, description: "Rebuild the pipe flow graph for cable animations every 4th frame and signal cable glow every 3rd frame");
            PontoonLeakPerMinute = category.CreateEntry("PontoonLeakPerMinute", 4f, description: "Oxygen leaking out of an inflated pontoon per minute (it holds 2); without an oxygen supply it deflates when empty");
            PumpHoseLength = category.CreateEntry("PumpHoseLength", 30f, description: "Pump hose length (m). All players should use the same value");
            if (Mathf.Approximately(PumpHoseLength.Value, 20f)) PumpHoseLength.Value = 30f;
            LaserVolume = category.CreateEntry("LaserVolume", 0.7f, description: "Laser cannon volume (0-1)");
            JetpackSecondsPerWood = category.CreateEntry("JetpackSecondsPerWood", 20f, description: "Seconds of jetpack flight per 1 wood. Host only");
            JetpackAcceleration = category.CreateEntry("JetpackAcceleration", 6f, description: "Jetpack upward acceleration above gravity (m/s^2). All players must use the same value");
            JetpackMaxSpeed = category.CreateEntry("JetpackMaxSpeed", 7f, description: "Maximum jetpack climb speed (m/s). All players must use the same value");
            category.DeleteEntry("JetpackVolume");
            JetpackVolume = category.CreateEntry("JetpackFireVolume", 0.45f, description: "Jetpack flame volume (0-1)");
            FastRooms = category.CreateEntry("FastRooms", true, description: "Fast room/oxygen recalculation after a ship change (less lag while building). false = like the game. Computed by the host");
            RotorLeverAngle = category.CreateEntry("RotorLeverAngle", 90f, description: "Rotor angle for a signal of 1, e.g. from a lever (degrees). Signals between -1 and 1 are multiplied by it, bigger signals are degrees. Host only");
            RotorCollision = category.CreateEntry("RotorCollision", false, description: "When true, a turning rotor stops before its moving part hits something. When false, it turns through everything. Host only");
            KeepNearbyShipsLoaded = category.CreateEntry("KeepNearbyShipsLoaded", true, description: "Keep the physics of ships within 3 km loaded even when they are in another space, so walking between areas does not rebuild their colliders (removes hitches). Host only");
            RotorSpeed = category.CreateEntry("RotorSpeed", 90f, description: "Rotor turning speed (degrees per second). Host only");
            HydrogenThrustMultiplier = category.CreateEntry("HydrogenThrustMultiplier", 1.8f, description: "How many times stronger the hydrogen thruster is than the chemical one. Host only");
            DevAutoLoadWorld = category.CreateEntry("DevAutoLoadWorld", "",
                description: "Mod developer: full path to a world.json loaded automatically from the menu (empty = off)");
            DevDump = category.CreateEntry("DevDump", false, description: "Mod developer: dump parts and materials to UserData/Mining/dump.txt");
            DevQuitAfterDump = category.CreateEntry("DevQuitAfterDump", false, description: "Mod developer: quit the game after the dump");
            DevTest = category.CreateEntry("DevTest", false, description: "Mod developer: preview the excavator and a deposit in front of the camera after the world loads");
            DevJetpack = category.CreateEntry("DevJetpack", false, description: "Mod developer: jetpack flight test");
            DevPerf = category.CreateEntry("DevPerf", false, description: "Mod developer: measure lag while quickly placing and removing parts");
            category.SaveToFile(false);
            LoggerInstance.Msg("Press N: nearest ore deposit, U: nearest uranium deposit, J: jetpack on/off.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Sites.MiningSites.Reset();
            Sites.MiningSiteVisuals.Clear();
            Server.ExcavatorServer.Reset();
            Server.WaterPumpServer.Reset();
            Server.ShipPartIndex.Reset();
            Blueprints.BlueprintGhosts.Clear();
            Diagnostics.DevTools.OnSceneLoaded(sceneName);
        }

        public override void OnUpdate()
        {
            try
            {
                Sites.MiningSiteVisuals.Update();
                Blueprints.BlueprintGhosts.Update();
                Diagnostics.DetailCuller.Update();
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.nKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping())
                {
                    ShowMessage(Sites.MiningSiteVisuals.DescribeNearest());
                }
                if (kb != null && kb.uKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping())
                {
                    ShowMessage(Sites.MiningSiteVisuals.DescribeNearest(uraniumOnly: true));
                }
                if (kb != null && kb.kKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping())
                {
                    ShowMessage(Sites.MiningSiteVisuals.DescribeNearest(Items.Model.ItemsConstants.GlassItemId, 5000f, "No glass deposits within 5 km."));
                }
                if (kb != null && kb.bKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping() && !Blueprints.BlueprintCatalog.IsTyping
                    && (Blueprints.BlueprintCatalog.IsOpen || Cursor.lockState == CursorLockMode.Locked))
                {
                    Blueprints.BlueprintCatalog.Toggle();
                }
                if (kb != null && kb.f11Key.wasPressedThisFrame)
                {
                    Diagnostics.FrameProfiler.Start();
                }
                if (kb != null && kb.lKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping() && !Blueprints.BlueprintCatalog.IsTyping && Content.Jetpack.IsUnlocked()
                    && Cursor.lockState == CursorLockMode.Locked)
                {
                    Content.Jetpack.Hold = !Content.Jetpack.Hold;
                    Content.Jetpack.HoldAirborne = false;
                    ShowMessage(Content.Jetpack.Hold ? "Jetpack hold ON - altitude locked, Space up / Crouch down" : "Jetpack hold OFF", 1.5f);
                }
                if (kb != null && kb.jKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping() && Content.Jetpack.IsUnlocked())
                {
                    Content.Jetpack.Enabled = !Content.Jetpack.Enabled;
                    ShowMessage(Content.Jetpack.Enabled ? "Jetpack ON" : "Jetpack OFF", 1.5f);
                }
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error($"Deposit error: {e}");
                ShowSites.Value = false;
            }
        }

        internal static void ShowMessage(string text, float seconds = 6f)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + seconds;
            Melon<MiningMod>.Logger.Msg(text);
        }

        public override void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (_message == null || Time.unscaledTime > _messageUntil)
            {
                return;
            }
            DrawLabel(_message, Screen.height * 0.18f);
        }

        private static GUIStyle _labelStyle, _shadowStyle;

        private static void DrawLabel(string text, float y)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                _labelStyle.normal.textColor = Color.white;
                _shadowStyle = new GUIStyle(_labelStyle);
                _shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
            }
            var rect = new Rect(0, y, Screen.width, 40);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, _shadowStyle);
            GUI.Label(rect, text, _labelStyle);
        }
    }
}
