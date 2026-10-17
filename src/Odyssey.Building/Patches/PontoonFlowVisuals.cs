using System;
using System.Collections;
using System.Reflection;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using Ships.Cables.Fluids;
using Ships.Cables.VisualObjects;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Services;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(FluidCablesVisualUpdater), nameof(FluidCablesVisualUpdater.UpdateFluidCables))]
    internal static class PontoonFlowVisuals
    {
        private static void Postfix(FluidCablesVisualUpdater __instance)
        {
            if (!FluidFlowState.OriginalSkipped) Apply(__instance);
        }

        private const float IntakePerSecond = 2f;
        private static readonly FieldInfo NodesField = AccessTools.Field(typeof(FluidCablesVisualUpdater), "_socketsToNodeMap");
        private static readonly FieldInfo ConnectionsField = AccessTools.Field(typeof(FluidCablesVisualUpdater), "_visibleNodeConnections");
        private static readonly FieldInfo VisitedField = AccessTools.Field(typeof(FluidCablesVisualUpdater), "_visitedIds");
        private static readonly MethodInfo Balance = AccessTools.Method(typeof(FluidCablesVisualUpdater), "RecurseBalanceIncomingFlow");
        private static readonly object[] Args = new object[2];
        private static AccessTools.FieldRef<object, FluidCableVisual> _visualRef;
        private static AccessTools.FieldRef<object, float> _flowRef;
        private static bool _loggedError;

        internal static void Apply(FluidCablesVisualUpdater updater)
        {
            if (Balance == null || !(NodesField?.GetValue(updater) is IDictionary nodes) || nodes.Count == 0)
            {
                return;
            }
            try
            {
                ShipsClientTracker ships = null;
                bool changed = false;
                foreach (DictionaryEntry entry in nodes)
                {
                    var socket = (EntityPartSocket)entry.Key;
                    if (socket.Socket.SocketId != Pontoon.OxygenSocket || entry.Value == null)
                    {
                        continue;
                    }
                    ships = ships ?? ServiceCache.Get<ShipsClientTracker>();
                    if (ships == null || !ships.TryGetStatefulPart(socket.PartContext, out StatefulPart part) || part.Settings == null || !IsPontoon(part.Settings.id))
                    {
                        continue;
                    }
                    int n = Pontoon.Scale(part.Settings.id);
                    float volume = n * n * n;
                    bool inflated = part.State is IDoorState door && door.IsOpen;
                    float demand = (inflated ? BuildingMod.PontoonLeakPerMinute.Value / 60f : IntakePerSecond) * volume / 50f;
                    if (demand <= 0f)
                    {
                        continue;
                    }
                    (VisitedField?.GetValue(updater) as System.Collections.Generic.HashSet<uint>)?.Clear();
                    Args[0] = entry.Value;
                    Args[1] = demand;
                    Balance.Invoke(updater, Args);
                    changed |= (float)Args[1] < demand;
                }
                if (changed && ConnectionsField?.GetValue(updater) is IDictionary connections)
                {
                    foreach (object connection in connections.Values)
                    {
                        if (connection == null) continue;
                        if (_flowRef == null)
                        {
                            Type type = connection.GetType();
                            _visualRef = AccessTools.FieldRefAccess<FluidCableVisual>(type, "CableVisual");
                            _flowRef = AccessTools.FieldRefAccess<float>(type, "Flow");
                        }
                        _visualRef(connection)?.UpdateFlow(_flowRef(connection));
                    }
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
