using HarmonyLib;
using Mining.Content;
using TMPro;
using UI.Common;
using UI.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(InteractionTooltipsActivator), "InstantiateTooltip")]
    internal static class WaterPumpLabelPatch
    {
        private static bool Prefix(InteractionTooltipsActivator __instance, InputActionReference action, LocalizedString localizedString,
            GameObject ___interactionTipPrefab, InteractionCanvas ____instantiatedCanvas)
        {
            string text = ReferenceEquals(localizedString, WaterPumpInteraction.LowerLabel) ? "Lower hose" :
                          ReferenceEquals(localizedString, WaterPumpInteraction.RaiseLabel) ? "Raise hose" :
                          ReferenceEquals(localizedString, InfoPanelInteraction.EditLabel) ? "Edit text" : null;
            if (text == null || ___interactionTipPrefab == null || ____instantiatedCanvas == null)
            {
                return true;
            }
            GameObject tip = Object.Instantiate(___interactionTipPrefab, ____instantiatedCanvas.tipsWrapper);
            LocalizeStringEvent localize = tip.GetComponentInChildren<LocalizeStringEvent>();
            if (localize != null)
            {
                localize.enabled = false;
            }
            TMP_Text label = localize != null ? localize.GetComponent<TMP_Text>() : null;
            if (label == null)
            {
                label = tip.GetComponentInChildren<TMP_Text>();
            }
            if (label != null)
            {
                label.text = text;
            }
            KeyTipIcon icon = tip.GetComponentInChildren<KeyTipIcon>();
            if (icon != null)
            {
                icon.Init(action);
            }
            return false;
        }
    }
}
