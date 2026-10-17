using Core.Services;
using HarmonyLib;
using Odyssey.Content;
using Players.Interface.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Items.Model;
using UI.Inventory;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(StorageMenuActivator), nameof(StorageMenuActivator.ShowMenu))]
    internal static class ReactorMenuPatch
    {
        private static void Postfix(GameObject ____currentMenu)
        {
            if (____currentMenu != null && StorageMenuPatch.IsLocalPlayerUsing(Reactor.Is))
            {
                MelonLoader.MelonCoroutines.Start(Rename(____currentMenu));
            }
        }

        private static System.Collections.IEnumerator Rename(GameObject menu)
        {
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
                if (menu == null)
                {
                    yield break;
                }
                foreach (TMPro.TMP_Text text in menu.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    string original = text.text;
                    if (string.IsNullOrEmpty(original))
                    {
                        continue;
                    }
                    string renamed = System.Text.RegularExpressions.Regex.Replace(original, @"Wood[- ]?(Fired|Power(ed)?)?[- ]?Generator", "Nuclear Reactor",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    renamed = renamed.Replace("Generator", "Reactor").Replace("generator", "reactor").Replace("Wood", "Uranium").Replace("wood", "uranium");
                    if (renamed != original)
                    {
                        var localize = text.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                        if (localize != null)
                        {
                            localize.enabled = false;
                        }
                        text.text = renamed;
                    }
                }
            }
        }
    }
}
