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
    // Koparka ma stan skrzyni (10 slotów), ale pokazujemy tylko 4 - tyle zapełnia wiertło.
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

        internal static bool IsLocalPlayerUsingExcavator()
        {
            var players = ServiceLocator.GetService<IPlayersProviderClient>();
            var ships = ServiceLocator.GetService<IShipsClientProvider>();
            if (players == null || ships == null || !players.TryGetLocalPlayerState(out var state))
            {
                return false;
            }
            return ships.TryGetShip(state.EntityId, out IShipStateReadClient ship) &&
                   ship.TryGetStatefulPart(state.PartId, out StatefulPart part) &&
                   Excavator.IsExcavator(part.Settings);
        }
    }
}
