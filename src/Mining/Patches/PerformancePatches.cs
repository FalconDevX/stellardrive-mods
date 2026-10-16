using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.VisualObjects;
using Ships.Grid;
using Ships.Grid.Model;
using Ships.Interface.Model;
using UnityEngine;

namespace Mining.Patches
{
    internal static class HullBoundsCache
    {
        internal sealed class Entry
        {
            public bool Valid;
            public CoordBounds Bounds;
        }

        internal static readonly ConditionalWeakTable<ShipGrid, Entry> Cache = new ConditionalWeakTable<ShipGrid, Entry>();

        internal static void Invalidate(ShipGrid grid)
        {
            if (grid != null && Cache.TryGetValue(grid, out Entry entry)) entry.Valid = false;
        }
    }

    [HarmonyPatch(typeof(ShipGrid), nameof(ShipGrid.GetCoordBounds))]
    internal static class HullBoundsCachePatch
    {
        private static bool Prefix(ShipGrid __instance, ref CoordBounds __result)
        {
            if (!MiningMod.FastShipBounds.Value) return true;
            HullBoundsCache.Entry entry = HullBoundsCache.Cache.GetOrCreateValue(__instance);
            if (!entry.Valid) return true;
            __result = entry.Bounds;
            return false;
        }

        private static void Postfix(ShipGrid __instance, CoordBounds __result)
        {
            if (!MiningMod.FastShipBounds.Value) return;
            HullBoundsCache.Entry entry = HullBoundsCache.Cache.GetOrCreateValue(__instance);
            entry.Bounds = __result;
            entry.Valid = true;
        }
    }

    [HarmonyPatch]
    internal static class HullBoundsInvalidatePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ShipGrid), nameof(ShipGrid.AddHullPart));
            yield return AccessTools.Method(typeof(ShipGrid), nameof(ShipGrid.UpdateHullPart));
            yield return AccessTools.Method(typeof(ShipGrid), nameof(ShipGrid.RemoveHullPart));
            yield return AccessTools.Method(typeof(ShipGrid), nameof(ShipGrid.AddStatefulPart));
            yield return AccessTools.Method(typeof(ShipGrid), nameof(ShipGrid.RemoveStatefulPart));
        }

        private static void Prefix(ShipGrid __instance) => HullBoundsCache.Invalidate(__instance);

        private static void Postfix(ShipGrid __instance) => HullBoundsCache.Invalidate(__instance);
    }

    [HarmonyPatch(typeof(FluidCablesVisualUpdater), nameof(FluidCablesVisualUpdater.UpdateFluidCables))]
    internal static class FluidCableVisualsThrottlePatch
    {
        private const int Every = 4;
        private static readonly FieldInfo ConnectionsField = AccessTools.Field(typeof(FluidCablesVisualUpdater), "_visibleNodeConnections");
        private static readonly List<(FluidCableVisual visual, float flow)> Flows = new List<(FluidCableVisual, float)>();
        private static AccessTools.FieldRef<object, FluidCableVisual> _visualRef;
        private static AccessTools.FieldRef<object, float> _flowRef;
        private static int _frame;
        private static int _cableCount = -1;
        private static bool _ranOriginal;

        private static bool Prefix(IReadOnlyDictionary<uint, CableVisual> loadedCables)
        {
            if (!MiningMod.FastCableVisuals.Value) return true;
            _frame++;
            if (_frame % Every == 0 || loadedCables.Count != _cableCount || _flowRef == null)
            {
                _ranOriginal = true;
                return true;
            }
            _ranOriginal = false;
            foreach (var (visual, flow) in Flows)
            {
                if (visual != null) visual.UpdateFlow(flow);
            }
            return false;
        }

        private static void Postfix(FluidCablesVisualUpdater __instance, IReadOnlyDictionary<uint, CableVisual> loadedCables)
        {
            if (!MiningMod.FastCableVisuals.Value || !_ranOriginal) return;
            _cableCount = loadedCables.Count;
            Flows.Clear();
            if (!(ConnectionsField?.GetValue(__instance) is IDictionary connections)) return;
            foreach (object connection in connections.Values)
            {
                if (connection == null) continue;
                if (_flowRef == null)
                {
                    Type type = connection.GetType();
                    if (AccessTools.Field(type, "CableVisual") == null || AccessTools.Field(type, "Flow") == null) return;
                    _visualRef = AccessTools.FieldRefAccess<FluidCableVisual>(type, "CableVisual");
                    _flowRef = AccessTools.FieldRefAccess<float>(type, "Flow");
                }
                Flows.Add((_visualRef(connection), _flowRef(connection)));
            }
        }
    }

    [HarmonyPatch(typeof(SignalCablesVisualUpdater), nameof(SignalCablesVisualUpdater.UpdateSignalCables))]
    internal static class SignalCableVisualsThrottlePatch
    {
        private const int Every = 3;
        private static int _frame;

        private static bool Prefix()
        {
            if (!MiningMod.FastCableVisuals.Value) return true;
            _frame++;
            return _frame % Every == 0;
        }
    }
}
