using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Services;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(FluidCablesVisualUpdater), "RecurseBuildNode")]
    internal static class PipeFlowVisuals
    {
        private const float ReactorWaterPerSecond = 0.05f;
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
                    ships = ships ?? ServiceCache.Get<ShipsClientTracker>();
                    if (ships == null || !ships.TryGetStatefulPart(entry.Key, out StatefulPart part) || part.Settings == null)
                    {
                        continue;
                    }
                    if (WaterPump.Is(part.Settings))
                    {
                        if (part.State is IDoorState door && door.IsOpen)
                        {
                            int outlets = (entry.Value.Contains(WaterPump.OutSocketA) ? 1 : 0) + (entry.Value.Contains(WaterPump.OutSocketB) ? 1 : 0);
                            _flowRef(__result) -= Math.Max(0f, PowerMod.PumpRate.Value) * ScaledVariants.Rate(part.Settings) * outlets / 2f / 50f;
                        }
                    }
                    else if (Reactor.Is(part.Settings) && entry.Value.Contains(Reactor.WaterSocket) && part.State is WoodPowerGeneratorState generator)
                    {
                        _flowRef(__result) += generator.NormalizedProductionLevel * ReactorWaterPerSecond * Reactor.Scale(part.Settings) / 50f;
                    }
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<PowerMod>.Logger.Error($"Pipe flow visuals: {e}");
                }
            }
        }
    }
}
