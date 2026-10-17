using System;
using System.Collections.Generic;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Odyssey.Content;
using Odyssey.Sites;
using Ships;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace Odyssey.Server
{
    [HarmonyPatch(typeof(TrackedShipServer))]
    internal static class ExcavatorServer
    {
        private const byte PowerSocketId = 0;
        private const int MaxStack = 100;

        private static readonly Dictionary<(uint ship, ushort part), float> Progress = new Dictionary<(uint, ushort), float>();
        private static readonly Dictionary<(uint ship, ushort part), MiningSite> SiteCache = new Dictionary<(uint, ushort), MiningSite>();
        private static float _nextSiteRefresh;
        private static bool _loggedError;

        private static readonly Dictionary<(uint ship, ushort part), string> Status = new Dictionary<(uint, ushort), string>();

        internal static void Reset()
        {
            Progress.Clear();
            SiteCache.Clear();
            Status.Clear();
        }

        private static void SetStatus((uint ship, ushort part) key, string status)
        {
            if (!Status.TryGetValue(key, out string previous) || previous != status)
            {
                Status[key] = status;
                Melon<MiningMod>.Logger.Msg($"Excavator (ship {key.ship}, part {key.part}): {status}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.PrepareConsumers))]
        private static void PrepareConsumers(TrackedShipServer __instance, float deltaTime)
        {
            List<ushort> ids = ShipPartIndex.Get(__instance, Excavator.PartId);
            if (ids.Count == 0)
            {
                return;
            }
            try
            {
                var power = ServiceCache.Get<PowerServerTracker>();
                foreach (ushort id in ids)
                {
                    if (!TryGetExcavator(__instance, id, out StatefulPart part))
                    {
                        continue;
                    }
                    if (CanWork(__instance, part, out _) && power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, PowerSocketId), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * MiningMod.PowerWatts.Value;
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance, float deltaTime)
        {
            List<ushort> ids = ShipPartIndex.Get(__instance, Excavator.PartId);
            if (ids.Count == 0)
            {
                return;
            }
            try
            {
                if (Time.time >= _nextSiteRefresh)
                {
                    _nextSiteRefresh = Time.time + 2f;
                    SiteCache.Clear();
                }
                var power = ServiceCache.Get<PowerServerTracker>();
                foreach (ushort id in ids)
                {
                    if (!TryGetExcavator(__instance, id, out StatefulPart part))
                    {
                        continue;
                    }
                    var key = (__instance.Id, part.Id);
                    MiningSite site = FindSite(__instance, part);
                    PowerGroupServerState group = null;
                    string status =
                        site == null ? "no ore deposit under the drill" :
                        !SignalAllows(__instance, part) ? "no signal 1 - connect a lever to the socket with the yellow cap" :
                        !HasSpace((StorageState)part.State, site.Kind.ItemId) ? "full - take the ore out" :
                        !power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, PowerSocketId), out group) ? "no power cable" :
                        group.NormalizedConsumerRate <= 0f ? "not enough power (generator empty or overloaded)" :
                        WorkingText(site.Kind.Name);
                    SetStatus(key, status);
                    bool working = site != null && ReferenceEquals(status, WorkingText(site.Kind.Name));
                    if (Excavator.HasWorkingMarker((StorageState)part.State) != working)
                    {
                        __instance.UpdatePartState(part.Id, (StorageState)((StorageState)part.State).WithUpdatedSlot(Excavator.WorkingMarkerSlot, Excavator.WorkingMarker(working)));
                    }
                    if (!working)
                    {
                        continue;
                    }
                    float progress = (Progress.TryGetValue(key, out float p) ? p : 0f) +
                                     deltaTime * group.NormalizedConsumerRate / Mathf.Max(0.5f, MiningMod.SecondsPerItem.Value);
                    if (progress >= 1f)
                    {
                        progress -= 1f;
                        if (TryAddItem((StorageState)part.State, site.Kind.ItemId, out StorageState updated))
                        {
                            __instance.UpdatePartState(part.Id, updated);
                        }
                    }
                    Progress[key] = progress;
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        private static readonly Dictionary<string, string> Working = new Dictionary<string, string>();

        private static string WorkingText(string kind)
        {
            if (!Working.TryGetValue(kind, out string text))
            {
                Working[kind] = text = "working - " + kind;
            }
            return text;
        }

        private static bool TryGetExcavator(TrackedShipServer ship, ushort id, out StatefulPart part) =>
            ship.TryGetStatefulPart(id, out part) && part.State is StorageState;

        private static bool CanWork(TrackedShipServer ship, StatefulPart part, out MiningSite site)
        {
            site = FindSite(ship, part);
            return site != null && SignalAllows(ship, part) && HasSpace((StorageState)part.State, site.Kind.ItemId);
        }

        private static bool SignalAllows(TrackedShipServer ship, StatefulPart part)
        {
            var connections = ServiceCache.Get<Ships.Cables.ConnectionsServerTracker>();
            var socket = new EntityPartSocket(ship.Id, part.Id, Excavator.SignalSocketId);
            return connections != null && connections.TryGetSignalConnectionGroupWithSocket(socket, out _) && connections.GetSignalValue(socket) >= 0.5;
        }

        private static MiningSite FindSite(TrackedShipServer ship, StatefulPart part)
        {
            var key = (ship.Id, part.Id);
            if (SiteCache.TryGetValue(key, out MiningSite cached))
            {
                return cached;
            }
            MiningSite site = null;
            var ships = ServiceCache.Get<ShipsServerTracker>();
            var spaces = ServiceCache.Get<ISpacesServerProvider>();
            var transpose = ServiceCache.Get<IWorldTransposeServer>();
            if (ships.TryGetRootShip(ship.Id, out IShipStateRead root) && root is TrackedShipServer rootShip &&
                spaces.GetSpace(rootShip.SpaceId) is PlanetSurfaceSpace surface)
            {
                Vector3 drillTip = part.LocalPosition + part.LocalRotation * (Excavator.DrillPivot + Vector3.down * Excavator.DrillLength);
                Vector3Double inSurface = transpose.Position(ship.RelativeSpaceId, surface, (Vector3Double)drillTip);
                Vector3 planetPos = (Vector3)(surface.Center + inSurface);
                site = MiningSites.FindAt(surface.PlanetId, planetPos, extraRadius: 1f, maxHeight: 3f);
            }
            SiteCache[key] = site;
            return site;
        }

        private static bool HasSpace(StorageState state, uint itemId)
        {
            for (byte i = 0; i < Excavator.SlotCount; i++)
            {
                InventorySlot slot = state.GetSlot(i);
                if (!slot.HasItem || (slot.ItemId == itemId && slot.Quantity < MaxStack))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryAddItem(StorageState state, uint itemId, out StorageState updated)
        {
            updated = state;
            int target = -1;
            for (byte i = 0; i < Excavator.SlotCount && target < 0; i++)
            {
                InventorySlot slot = state.GetSlot(i);
                if (slot.HasItem && slot.ItemId == itemId && slot.Quantity < MaxStack) target = i;
            }
            for (byte i = 0; i < Excavator.SlotCount && target < 0; i++)
            {
                if (!state.GetSlot(i).HasItem) target = i;
            }
            if (target < 0)
            {
                return false;
            }
            InventorySlot current = state.GetSlot((byte)target);
            var slotData = new InventorySlot
            {
                HasItem = true,
                ItemId = itemId,
                Quantity = (byte)((current.HasItem ? current.Quantity : 0) + 1),
            };
            updated = (StorageState)state.WithUpdatedSlot((byte)target, slotData);
            return true;
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Error($"Excavator error (server): {e}");
            }
        }
    }
}
