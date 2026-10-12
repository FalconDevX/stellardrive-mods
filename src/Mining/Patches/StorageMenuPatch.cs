using Core.Services;
using HarmonyLib;
using Mining.Content;
using Players.Interface.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Items.Model;
using UI.Inventory;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(StorageMenuActivator), nameof(StorageMenuActivator.ShowMenu))]
    internal static class StorageMenuPatch
    {
        private static void Postfix(GameObject ____currentMenu)
        {
            if (____currentMenu == null || !IsLocalPlayerUsingExcavator())
            {
                return;
            }
            foreach (InventorySlotUI slot in ____currentMenu.GetComponentsInChildren<InventorySlotUI>(true))
            {
                if (slot.origin == InventoryType.Chest && slot.id >= Excavator.SlotCount)
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        internal static bool TryGetOpenExcavator(out uint shipId, out ushort partId)
        {
            shipId = 0;
            partId = 0;
            var players = ServiceLocator.GetService<IPlayersProviderClient>();
            if (players == null || !players.TryGetLocalPlayerState(out var state) || state.Type != Players.Interface.Model.PlayerStateType.InteractingWithChest || !IsLocalPlayerUsingExcavator())
            {
                return false;
            }
            shipId = state.EntityId;
            partId = state.PartId;
            return true;
        }

        internal static bool IsLocalPlayerUsingExcavator() => IsLocalPlayerUsing(Excavator.IsExcavator);

        internal static bool IsLocalPlayerUsing(System.Func<Ships.Interface.Settings.PartSettings, bool> isPart)
        {
            var players = ServiceLocator.GetService<IPlayersProviderClient>();
            var ships = ServiceLocator.GetService<IShipsClientProvider>();
            if (players == null || ships == null || !players.TryGetLocalPlayerState(out var state))
            {
                return false;
            }
            return ships.TryGetShip(state.EntityId, out IShipStateReadClient ship) &&
                   ship.TryGetStatefulPart(state.PartId, out StatefulPart part) &&
                   isPart(part.Settings);
        }
    }
}

namespace Mining.Patches
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
