using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Research.Interface.Model;
using Research.Interface.Values;
using Ships.Interface.Settings;
using TMPro;
using UI.Research;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mining.Blueprints
{
    internal static class WorkbenchUi
    {
        private const ushort FirstRecipeId = 64000;
        internal static bool Pending;
        private static readonly HashSet<ResearchCanvas> Canvases = new HashSet<ResearchCanvas>();
        private static readonly Dictionary<ResearchSettings, uint> Items = new Dictionary<ResearchSettings, uint>();
        private static List<ResearchSettings> _recipes;

        internal static bool IsWorkbench(ResearchCanvas canvas)
        {
            Canvases.RemoveWhere(c => c == null);
            return canvas != null && Canvases.Contains(canvas);
        }

        internal static void Claim(ResearchCanvas canvas)
        {
            if (Pending)
            {
                Pending = false;
                Canvases.Add(canvas);
            }
        }

        internal static bool IsRecipe(ResearchSettings research) => research != null && Items.ContainsKey(research);

        internal static uint ItemOf(ResearchSettings research) => Items[research];

        internal static IReadOnlyList<ResearchSettings> Recipes
        {
            get
            {
                if (_recipes != null && _recipes.All(r => r != null))
                {
                    return _recipes;
                }
                _recipes = new List<ResearchSettings>();
                Items.Clear();
                for (int i = 0; i < Workbench.Recipes.Length; i++)
                {
                    var recipe = Workbench.Recipes[i];
                    var r = ScriptableObject.CreateInstance<ResearchSettings>();
                    r.name = "WorkbenchRecipe" + recipe.item;
                    r.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    r.id = (ushort)(FirstRecipeId + i);
                    r.techName = ServerInventory.ItemName(recipe.item);
                    r.requirements = new ResearchSettings[0];
                    r.unlockedParts = new PartSettings[0];
                    r.unlockedFeatures = new ResearchUnlockableFeature[0];
                    r.unlockedGuideEntriesIds = new uint[0];
                    r.resourceCost = recipe.cost.Select(c => PartFactory.CreateItemInstance(c.item, c.count)).Where(c => c != null).ToArray();
                    r.availableInDemo = true;
                    r.positionInTechTree = new Vector2Int(i, 0);
                    _recipes.Add(r);
                    Items[r] = recipe.item;
                }
                return _recipes;
            }
        }
    }

    [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
    internal static class WorkbenchButtonsPatch
    {
        private static bool Prefix(ResearchCanvas __instance, ResearchSelectButton ___researchSelectButtonPrefab, RectTransform ___buttonsContainer, ref List<ResearchSelectButton> ____buttons)
        {
            WorkbenchUi.Claim(__instance);
            if (!WorkbenchUi.IsWorkbench(__instance))
            {
                return true;
            }
            try
            {
                ____buttons = new List<ResearchSelectButton>();
                foreach (ResearchSettings recipe in WorkbenchUi.Recipes)
                {
                    ResearchSelectButton button = Object.Instantiate(___researchSelectButtonPrefab, ___buttonsContainer);
                    button.research = recipe;
                    ____buttons.Add(button);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Workbench buttons: {e}");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ResearchSelectButton), "UpdateState")]
    internal static class WorkbenchButtonStatePatch
    {
        private static void Postfix(ResearchSelectButton __instance, ref ResearchSelectButton.ButtonState ____state)
        {
            if (WorkbenchUi.IsRecipe(__instance.research))
            {
                ____state = ResearchSelectButton.ButtonState.Available;
            }
        }
    }

    [HarmonyPatch(typeof(ResearchCanvas), nameof(ResearchCanvas.UnlockSelectedResearch))]
    internal static class WorkbenchCraftPatch
    {
        private static bool Prefix(ResearchCanvas __instance, ResearchSettings ____selectedResearch)
        {
            if (!WorkbenchUi.IsRecipe(____selectedResearch))
            {
                return true;
            }
            Workbench.Craft(WorkbenchUi.ItemOf(____selectedResearch), out string message);
            MiningMod.ShowMessage(message, 3f);
            Traverse.Create(__instance).Method("UpdateButtons").GetValue();
            Traverse.Create(__instance).Method("RefreshTechInfo").GetValue();
            return false;
        }
    }

    [HarmonyPatch(typeof(ResearchInfo), nameof(ResearchInfo.Show))]
    internal static class WorkbenchInfoPatch
    {
        private static readonly Dictionary<Button, string> OriginalLabels = new Dictionary<Button, string>();

        private static void Postfix(ResearchSettings researchSettings, Transform ___unlockLabel, Button ___researchButton)
        {
            bool recipe = WorkbenchUi.IsRecipe(researchSettings);
            if (___unlockLabel != null) ___unlockLabel.gameObject.SetActive(!recipe);
            TMP_Text label = ___researchButton != null ? ___researchButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (label == null)
            {
                return;
            }
            if (!OriginalLabels.ContainsKey(___researchButton)) OriginalLabels[___researchButton] = label.text;
            label.text = recipe ? "Craft" : OriginalLabels[___researchButton];
        }
    }
}
