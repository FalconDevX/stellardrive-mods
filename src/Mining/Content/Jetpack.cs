using System.Collections.Generic;
using Core.Services;
using Items.Interface.Service;
using Items.Model;
using Players;
using Research.Interface.Services;
using UnityEngine;

namespace Mining.Content
{
    internal static class Jetpack
    {
        internal const ushort ResearchId = 205;
        internal const byte FuelSlot = 14;
        internal static uint FuelItem => ItemsConstants.WoodItemId;

        internal static TrackedPlayerServer CurrentServerPlayer;
        internal static bool TestForceUp;
        internal static bool CurrentIsLocalPrediction;

        internal static bool Enabled = true;
        internal static bool Hold;
        internal static bool HoldAirborne;
        internal static bool LocalHasGravity;

        private static readonly Dictionary<uint, float> Burned = new Dictionary<uint, float>();

        internal static bool IsUnlocked()
        {
            var research = ServiceLocator.GetService<IResearchProvider>();
            try
            {
                return research != null && research.IsResearched(ResearchId);
            }
            catch
            {
                return false;
            }
        }

        internal static bool HasFuel()
        {
            InventorySlot slot = default;
            if (CurrentServerPlayer != null)
            {
                var state = HarmonyLib.Traverse.Create(CurrentServerPlayer).Property("NetworkState").GetValue<Players.Network.PlayerNetworkState>();
                slot = state != null ? state.Inventory[FuelSlot] : default;
            }
            else if (CurrentIsLocalPrediction)
            {
                var inventory = ServiceLocator.GetService<Players.Interface.Services.IPlayerMainInventoryProvider>();
                slot = inventory != null ? inventory.GetSlot(FuelSlot) : default;
            }
            return slot.HasItem && slot.ItemId == FuelItem && slot.Quantity > 0;
        }

        internal static void Burn(float seconds)
        {
            TrackedPlayerServer player = CurrentServerPlayer;
            if (player == null)
            {
                return;
            }
            float burned = (Burned.TryGetValue(player.Id, out float b) ? b : 0f) + seconds / Mathf.Max(1f, MiningMod.JetpackSecondsPerWood.Value);
            if (burned >= 1f)
            {
                burned -= 1f;
                var state = HarmonyLib.Traverse.Create(player).Property("NetworkState").GetValue<Players.Network.PlayerNetworkState>();
                if (state != null)
                {
                    InventorySlot slot = state.Inventory[FuelSlot];
                    if (slot.HasItem && slot.ItemId == FuelItem)
                    {
                        slot.Quantity -= 1;
                        state.ServerSetInventorySlot(FuelSlot, slot.Quantity > 0 ? slot : default(InventorySlot));
                    }
                }
            }
            Burned[player.Id] = burned;
        }
    }
}
