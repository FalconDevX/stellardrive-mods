using HarmonyLib;
using Managers.Server.Services;
using Players;
using Players.Interface.Model;
using Players.Services;
using Ships.Interface.Model.Parts.Common;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(PlayersStateIntegrityRestorer))]
    internal static class CableNoclipFixPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnCablePlugCreated")]
        private static bool PlugCreated(PlayersServerTracker ____players, uint __0, CablePlugSocket __1)
        {
            if (__1.IsHeldByPlayer)
            {
                StartHolding(____players, __1.EntityId, __0);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("ProcessCablePlugUpdate")]
        private static bool PlugUpdate(PlayersServerTracker ____players, uint __0, CablePlugSocket __1, CablePlugSocket __2)
        {
            if (__1.IsHeldByPlayer && !__2.IsHeldByPlayer)
            {
                StopHolding(____players, __1.EntityId);
            }
            else if (!__1.IsHeldByPlayer && __2.IsHeldByPlayer)
            {
                StartHolding(____players, __2.EntityId, __0);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCablePlugDeleted")]
        private static bool PlugDeleted(PlayersServerTracker ____players, CablePlugSocket __0)
        {
            if (__0.IsHeldByPlayer)
            {
                StopHolding(____players, __0.EntityId);
            }
            return false;
        }

        private static bool IsCheatFlying(PlayerStateType type) =>
            type == PlayerStateType.CheatFlying || type == PlayerStateType.CheatFlyingHoldingCable;

        private static void StartHolding(PlayersServerTracker players, uint playerId, uint cableId)
        {
            if (players?.GetPlayer(playerId) is TrackedPlayerServer player)
            {
                player.SetState(new PlayerState
                {
                    Type = IsCheatFlying(player.StateVal) ? PlayerStateType.CheatFlyingHoldingCable : PlayerStateType.WalkingHoldingCable,
                    EntityId = cableId,
                });
            }
        }

        private static void StopHolding(PlayersServerTracker players, uint playerId)
        {
            if (players?.GetPlayer(playerId) is TrackedPlayerServer player)
            {
                player.SetState(new PlayerState
                {
                    Type = IsCheatFlying(player.StateVal) ? PlayerStateType.CheatFlying : PlayerStateType.Walking,
                });
            }
        }
    }
}
