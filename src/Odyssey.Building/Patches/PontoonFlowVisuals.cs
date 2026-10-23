using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Services;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(FluidCablesVisualUpdater), "RecurseBuildNode")]
    internal static class PontoonFlowVisuals
    {
        private const float IntakePerSecond = 2f;
        private static AccessTools.FieldRef<object, float> _flowRef;
        private static AccessTools.FieldRef<object, Dictionary<PartContext, List<byte>>> _partsRef;
        private static bool _loggedError;

        private static void Postfix(object __result)
        {
            if (__result == null)
            {
                return;
            }
            try
            {
                if (_flowRef == null)
                {
                    Type type = __result.GetType();
                    _flowRef = AccessTools.FieldRefAccess<float>(type, "FluidFlow");
                    _partsRef = AccessTools.FieldRefAccess<Dictionary<PartContext, List<byte>>>(type, "ConnectedParts");
                }
                ShipsClientTracker ships = null;
                foreach (KeyValuePair<PartContext, List<byte>> entry in _partsRef(__result))
                {
                    if (!entry.Value.Contains(Pontoon.OxygenSocket))
                    {
                        continue;
                    }
                    ships = ships ?? ServiceCache.Get<ShipsClientTracker>();
                    if (ships == null || !ships.TryGetStatefulPart(entry.Key, out StatefulPart part) || part.Settings == null || !IsPontoon(part.Settings.id))
                    {
                        continue;
                    }
                    int n = Pontoon.Scale(part.Settings.id);
                    bool inflated = part.State is IDoorState door && door.IsOpen;
                    float perSecond = inflated ? Math.Max(0f, BuildingMod.PontoonLeakPerHour.Value) / 3600f : IntakePerSecond;
                    _flowRef(__result) += perSecond * n * n * n / 50f;
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<BuildingMod>.Logger.Error($"Pontoon pipe flow: {e}");
                }
            }
        }

        private static bool IsPontoon(ushort id)
        {
            foreach (ushort p in Pontoon.Ids)
            {
                if (p == id) return true;
            }
            return false;
        }
    }
}
