using HarmonyLib;
using TMPro;
using UI.Common;
using UI.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(InteractionTooltipsActivator), "InstantiateTooltip")]
    internal static class CustomTooltipPatch
    {
        internal static readonly System.Collections.Generic.List<(LocalizedString key, string text)> Labels = new System.Collections.Generic.List<(LocalizedString, string)>();

        private static bool Prefix(InteractionTooltipsActivator __instance, InputActionReference action, LocalizedString localizedString,
            GameObject ___interactionTipPrefab, InteractionCanvas ____instantiatedCanvas)
        {
            string text = Labels.Find(l => ReferenceEquals(l.key, localizedString)).text;
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
