using System;
using MelonLoader;
using Odyssey;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MiningMod), "Odyssey Mining", "1.0.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
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

        private static readonly Action SitesUpdate = Sites.MiningSiteVisuals.Update;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyMining", "Odyssey Mining");
            SecondsPerItem = Settings.Entry(category, "SecondsPerOre", 1f, "Seconds per 1 ore mined by the excavator (at full power). Host only");
            PowerWatts = Settings.Entry(category, "PowerWatts", 1500f, "Excavator power draw (W). The wood generator gives 4000 W");
            SiteSpacing = Settings.Entry(category, "SiteSpacing", 300f, "Deposit grid cell size (m) - each cell can hold one deposit. Less = denser deposits. All players must use the same value");
            SiteChance = Settings.Entry(category, "SiteChance", 0.5f, "Chance of a deposit in a cell (0-1). All players must use the same value");
            SiteRadius = Settings.Entry(category, "SiteRadius", 5f, "Deposit radius (m). All players must use the same value");
            UraniumChance = Settings.Entry(category, "UraniumShare", 0.3f, "Share of Otrera b deposits that are uranium (0-1). All players must use the same value");
            ShowSites = Settings.Entry(category, "ShowSites", true, "Show deposits (fields of ore rocks)");
            DrillVolume = Settings.Entry(category, "DrillVolume", 0.8f, "Drill volume (0-1)");
            category.SaveToFile(false);
            Patches.Registration.Register();
            LoggerInstance.Msg("Press N: nearest ore deposit, U: nearest uranium deposit, K: nearest glass deposit.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Sites.MiningSites.Reset();
            Sites.MiningSiteVisuals.Clear();
            Server.ExcavatorServer.Reset();
        }

        public override void OnUpdate()
        {
            CoreMod.Run(LoggerInstance, "Deposits", SitesUpdate);
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }
            if (CoreMod.KeyPressed(kb.nKey))
            {
                CoreMod.ShowMessage(Sites.MiningSiteVisuals.DescribeNearest());
            }
            if (CoreMod.KeyPressed(kb.uKey))
            {
                CoreMod.ShowMessage(Sites.MiningSiteVisuals.DescribeNearest(uraniumOnly: true));
            }
            if (CoreMod.KeyPressed(kb.kKey))
            {
                CoreMod.ShowMessage(Sites.MiningSiteVisuals.DescribeNearest(Items.Model.ItemsConstants.GlassItemId, 5000f, "No glass deposits within 5 km."));
            }
        }
    }
}
