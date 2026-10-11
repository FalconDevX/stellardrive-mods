using System;
using System.Collections.Generic;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Mining.Sites;
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

namespace Mining.Server
{
    // Praca koparki po stronie hosta, w tym samym cyklu co inne urządzenia statku:
    //  PrepareConsumers - zgłasza zapotrzebowanie na prąd (jak elektrolizer),
    //  UpdateParts      - postęp wydobycia zależny od dostarczonego prądu; co pełny cykl 1 sztuka rudy do slotów 1-4.
    // Kopie tylko, gdy stoi na złożu (MiningSites), na planecie, i ma miejsce w slotach.
    [HarmonyPatch(typeof(TrackedShipServer))]
    internal static class ExcavatorServer
    {
        private const byte PowerSocketId = 0;
        private const int MaxStack = 100;

        private static readonly Dictionary<(uint ship, ushort part), float> Progress = new Dictionary<(uint, ushort), float>();
        private static readonly Dictionary<(uint ship, ushort part), MiningSite> SiteCache = new Dictionary<(uint, ushort), MiningSite>();
        private static float _nextSiteRefresh;
        private static bool _loggedError;

        internal static void Reset()
        {
            Progress.Clear();
            SiteCache.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.PrepareConsumers))]
        private static void PrepareConsumers(TrackedShipServer __instance, float deltaTime)
        {
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in Excavators(__instance))
                {
                    if (CanWork(__instance, part, out _) && power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, PowerSocketId), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * MiningMod.PowerWatts.Value;
                    }
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance, float deltaTime)
        {
            Run(() =>
            {
                if (Time.time >= _nextSiteRefresh)
                {
                    _nextSiteRefresh = Time.time + 2f;
                    SiteCache.Clear();
                }
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in Excavators(__instance))
                {
                    var key = (__instance.Id, part.Id);
                    if (!CanWork(__instance, part, out MiningSite site) ||
                        !power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, PowerSocketId), out PowerGroupServerState group) ||
                        group.NormalizedConsumerRate <= 0f)
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
            });
        }

        private static IEnumerable<StatefulPart> Excavators(TrackedShipServer ship)
        {
            foreach (StatefulPart part in ship.GetAllStatefulParts<StorageState>())
            {
                if (Excavator.IsExcavator(part.Settings))
                {
                    yield return part;
                }
            }
        }

        private static bool CanWork(TrackedShipServer ship, StatefulPart part, out MiningSite site)
        {
            site = FindSite(ship, part);
            return site != null && SignalAllows(ship, part) && HasSpace((StorageState)part.State, site.Kind.ItemId);
        }

        // Wejście sygnału: bez kabla koparka pracuje, z kablem tylko przy sygnale >= 0,5 (czyli 1).
        private static bool SignalAllows(TrackedShipServer ship, StatefulPart part)
        {
            var connections = ServiceLocator.GetService<Ships.Cables.ConnectionsServerTracker>();
            var socket = new EntityPartSocket(ship.Id, part.Id, Excavator.SignalSocketId);
            return connections == null || !connections.TryGetSignalConnectionGroupWithSocket(socket, out _) || connections.GetSignalValue(socket) >= 0.5;
        }

        private static MiningSite FindSite(TrackedShipServer ship, StatefulPart part)
        {
            var key = (ship.Id, part.Id);
            if (SiteCache.TryGetValue(key, out MiningSite cached))
            {
                return cached;
            }
            MiningSite site = null;
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            if (ships.TryGetRootShip(ship.Id, out IShipStateRead root) && root is TrackedShipServer rootShip &&
                spaces.GetSpace(rootShip.SpaceId) is PlanetSurfaceSpace surface)
            {
                // Czubek wiertła (pod koparką) w układzie planety.
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

        // Najpierw dokłada do niepełnego stosu tej rudy, potem do pustego slotu.
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

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Błąd koparki (serwer): {e}");
                }
            }
        }
    }
}
