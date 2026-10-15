using System.Collections.Generic;
using HarmonyLib;
using Mining.Content;
using Players.Physics;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PlayerPhysics), "IsPlayerFeetOnShip")]
    internal static class StairsBoardingPatch
    {
        private const float RescanSeconds = 1f;
        private const float SideMargin = 0.15f, EndMargin = 0.3f, Below = 0.3f, Above = 0.6f;

        private static readonly Dictionary<uint, (float time, List<StatefulPart> stairs)> Cache = new Dictionary<uint, (float, List<StatefulPart>)>();

        private static void Postfix(Vector3 playerRelativeFeetPos, Quaternion playerRelativeRot, IShipStateRead ship, ref bool __result)
        {
            if (__result || ship == null || Vector3.Dot(playerRelativeRot * Vector3.up, Vector3.up) <= 0.3f)
            {
                return;
            }
            foreach (StatefulPart part in StairsOf(ship))
            {
                Vector3 local = Quaternion.Inverse(part.LocalRotation) * (playerRelativeFeetPos - part.LocalPosition);
                if (part.Settings.id == RetractableStairs.PartId)
                {
                    float run = RetractableStairs.Run(ship.Id, part.Id);
                    float ramp = RetractableStairs.Surface(local.z, run);
                    if (run > 0.1f && Mathf.Abs(local.x) < RetractableStairs.Width / 2f + SideMargin &&
                        local.z > RetractableStairs.Front - EndMargin && local.z < RetractableStairs.Front + run + EndMargin &&
                        local.y > ramp - Below && local.y < ramp + Above)
                    {
                        __result = true;
                        return;
                    }
                    continue;
                }
                float surface = Mathf.Clamp(local.z + Stairs.Run / 2f, 0f, Stairs.Run) * (Stairs.Rise / Stairs.Run);
                if (Mathf.Abs(local.x) < 0.5f + SideMargin && Mathf.Abs(local.z) < Stairs.Run / 2f + EndMargin &&
                    local.y > surface - Below && local.y < surface + Above)
                {
                    __result = true;
                    return;
                }
            }
        }

        private static List<StatefulPart> StairsOf(IShipStateRead ship)
        {
            if (Cache.TryGetValue(ship.Id, out var entry) && Time.time - entry.time < RescanSeconds && entry.time <= Time.time)
            {
                return entry.stairs;
            }
            var stairs = new List<StatefulPart>();
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part.Settings != null && (part.Settings.id == Stairs.PartId || part.Settings.id == RetractableStairs.PartId))
                {
                    stairs.Add(part);
                }
            }
            Cache[ship.Id] = (Time.time, stairs);
            return stairs;
        }
    }
}
