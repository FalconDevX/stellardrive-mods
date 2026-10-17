using HarmonyLib;
using Players.Network.Model;
using Players.Physics;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Model.State;
using UnityEngine;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(PlayerPhysics), "IsPlayerStillInsideRefShip")]
    internal static class AirborneInShipPatch
    {
        private const float MinShipSpeed = 2f;
        private const float Margin = 1f;

        private static void Postfix(PlayerPhysicsState physicsState, IShipStateRead ship, ref bool __result)
        {
            if (__result || !ShipSafetyMod.KeepJumpingPlayersInShip.Value || !(ship is TrackedShip tracked))
            {
                return;
            }
            if (tracked.PhysicsState.Velocity.sqrMagnitude < MinShipSpeed * MinShipSpeed)
            {
                return;
            }
            CoordBounds coords = tracked.GetHullCoordBounds();
            Vector3 min = (Vector3)coords.LowerCoord - Vector3.one * (0.5f + Margin);
            Vector3 max = (Vector3)coords.UpperCoord + Vector3.one * (0.5f + Margin);
            Vector3 p = physicsState.Position;
            __result = p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
        }
    }
}
