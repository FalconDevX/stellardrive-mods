using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Cables.VisualObjects;
using Ships.Grid;
using Ships.Grid.Model;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts.Common;
using UnityEngine;

namespace Odyssey.Patches
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
            if (!TweaksMod.FastShipBounds.Value) return true;
            HullBoundsCache.Entry entry = HullBoundsCache.Cache.GetOrCreateValue(__instance);
            if (!entry.Valid) return true;
            __result = entry.Bounds;
            return false;
        }

        private static void Postfix(ShipGrid __instance, CoordBounds __result)
        {
            if (!TweaksMod.FastShipBounds.Value) return;
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
            FluidFlowState.OriginalSkipped = false;
            if (!TweaksMod.FastCableVisuals.Value) return true;
            _frame++;
            if (_frame % Every == 0 || loadedCables.Count != _cableCount || _flowRef == null)
            {
                _ranOriginal = true;
                return true;
            }
            _ranOriginal = false;
            FluidFlowState.OriginalSkipped = true;
            foreach (var (visual, flow) in Flows)
            {
                if (visual != null) visual.UpdateFlow(flow);
            }
            return false;
        }

        private static void Postfix(FluidCablesVisualUpdater __instance, IReadOnlyDictionary<uint, CableVisual> loadedCables)
        {
            if (!TweaksMod.FastCableVisuals.Value || !_ranOriginal) return;
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
            FluidFlowState.OriginalSkipped = false;
            if (!TweaksMod.FastCableVisuals.Value) return true;
            _frame++;
            return _frame % Every == 0;
        }
    }

    [HarmonyPatch(typeof(FluidCableVisual), nameof(FluidCableVisual.UpdateFlow))]
    internal static class HiddenFluidCableFlowPatch
    {
        private static readonly AccessTools.FieldRef<FluidCableVisual, MeshRenderer> Cord = AccessTools.FieldRefAccess<FluidCableVisual, MeshRenderer>("scrollingCord");

        private static bool Prefix(FluidCableVisual __instance)
        {
            if (!TweaksMod.FastCableVisuals.Value) return true;
            MeshRenderer cord = Cord(__instance);
            return cord == null || cord.isVisible;
        }
    }

    internal static class SignalInputsCache
    {
        internal sealed class Entry
        {
            public int Version;
            public float Time;
            public List<EntityPartSocket> Inputs;
        }

        private const float MaxAge = 1f;
        private static readonly ConditionalWeakTable<SignalConnectionGroupValues, Dictionary<uint, Entry>> Cache = new ConditionalWeakTable<SignalConnectionGroupValues, Dictionary<uint, Entry>>();
        internal static int Version;

        internal static List<EntityPartSocket> Get(SignalConnectionGroupValues values, uint groupId)
        {
            Dictionary<uint, Entry> groups = Cache.GetOrCreateValue(values);
            float now = Time.unscaledTime;
            if (groups.TryGetValue(groupId, out Entry entry) && entry.Version == Version && now - entry.Time < MaxAge) return entry.Inputs;
            values.GetGroupInputsAndOutputs(groupId, out List<EntityPartSocket> inputs, out _);
            if (entry == null) groups[groupId] = entry = new Entry();
            entry.Version = Version;
            entry.Time = now;
            entry.Inputs = inputs;
            return inputs;
        }
    }

    [HarmonyPatch(typeof(SignalConnectionGroupValues), nameof(SignalConnectionGroupValues.UpdateGroupSignalValue))]
    internal static class SignalGroupValuePatch
    {
        private static readonly AccessTools.FieldRef<SignalConnectionGroupValues, Dictionary<uint, double>> Values = AccessTools.FieldRefAccess<SignalConnectionGroupValues, Dictionary<uint, double>>("_groupSignalValues");
        private static readonly Func<SignalConnectionGroupValues, EntityPartSocket, double> Signal =
            AccessTools.MethodDelegate<Func<SignalConnectionGroupValues, EntityPartSocket, double>>(AccessTools.Method(typeof(SignalConnectionGroupValues), "GetSocketSignal"));

        private static bool Prefix(SignalConnectionGroupValues __instance, uint groupId)
        {
            if (!TweaksMod.FastSignals.Value) return true;
            List<EntityPartSocket> inputs = SignalInputsCache.Get(__instance, groupId);
            double value = 0.0;
            for (int i = 0; i < inputs.Count; i++)
            {
                value = SignalCablesUtils.ResolveConflictingSignal(value, Signal(__instance, inputs[i]));
            }
            Values(__instance)[groupId] = value;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class SignalInputsInvalidatePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ConnectionGroupMap), nameof(ConnectionGroupMap.OnConnectionCreate));
            yield return AccessTools.Method(typeof(ConnectionGroupMap), nameof(ConnectionGroupMap.OnConnectionUpdate));
            yield return AccessTools.Method(typeof(ConnectionGroupMap), nameof(ConnectionGroupMap.OnConnectionDelete));
            yield return AccessTools.Method(typeof(ShipState), nameof(ShipState.AddStatefulPart));
            yield return AccessTools.Method(typeof(ShipState), nameof(ShipState.AddStatefulPartWithoutInit));
            yield return AccessTools.Method(typeof(ShipState), nameof(ShipState.RemoveStatefulPart));
        }

        private static void Postfix() => SignalInputsCache.Version++;
    }

    [HarmonyPatch(typeof(PowerServerTracker), "EnsureStateStructureIsUpToDate")]
    internal static class PowerCableIdsResetPatch
    {
        private static readonly AccessTools.FieldRef<PowerServerTracker, HashSet<uint>> Reload = AccessTools.FieldRefAccess<PowerServerTracker, HashSet<uint>>("_groupsToReloadCache");
        private static readonly AccessTools.FieldRef<PowerServerTracker, Dictionary<uint, PowerGroupServerState>> State = AccessTools.FieldRefAccess<PowerServerTracker, Dictionary<uint, PowerGroupServerState>>("_powerState");

        private static void Prefix(PowerServerTracker __instance)
        {
            HashSet<uint> reload = Reload(__instance);
            if (reload.Count == 0) return;
            Dictionary<uint, PowerGroupServerState> state = State(__instance);
            foreach (uint id in reload)
            {
                if (state.TryGetValue(id, out PowerGroupServerState group)) group.CableIds.Clear();
            }
        }
    }
}
