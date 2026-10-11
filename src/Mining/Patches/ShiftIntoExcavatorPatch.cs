using Core.Services;
using HarmonyLib;
using Mining.Content;
using Players.Interface.Model;
using Players.Inventory;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using Items.Model;

namespace Mining.Patches
{
    // Shift+klik z plecaka/paska wrzuca przedmiot do pierwszego wolnego slotu skrzyni - w koparce mógłby trafić
    // do ukrytych slotów 5-10. Dlatego przy otwartej koparce shift działa tylko w drugą stronę (z koparki do gracza).
    [HarmonyPatch(typeof(PlayerInventoryTransactions))]
    internal static class ShiftIntoExcavatorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.ShiftItem))]
        private static bool ShiftItem(PlayerInventoryTransactions __instance, ItemSlotId request) => Allowed(__instance, request);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.ShiftSplitItem))]
        private static bool ShiftSplitItem(PlayerInventoryTransactions __instance, ItemSlotId request) => Allowed(__instance, request);

        private static bool Allowed(PlayerInventoryTransactions transactions, ItemSlotId request)
        {
            if (request.Type == InventoryType.Chest)
            {
                return true;
            }
            PlayerState state = Traverse.Create(transactions).Field("_playerState").Property("ServerState").GetValue<PlayerState>();
            if (state.Type != PlayerStateType.InteractingWithChest)
            {
                return true;
            }
            var ships = ServiceLocator.GetService<IShipsServerProvider>();
            return ships == null ||
                   !ships.TryGetStatefulPart(new PartContext { ShipId = state.EntityId, PartId = state.PartId }, out StatefulPart part) ||
                   !Excavator.IsExcavator(part.Settings);
        }
    }
}
