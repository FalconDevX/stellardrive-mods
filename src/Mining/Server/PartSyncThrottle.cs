using System;
using System.Collections.Generic;
using HarmonyLib;
using Managers.Server;
using Managers.Server.Network;
using MelonLoader;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Services;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(MasterSync), "ServerQueueStatefulPartUpdate")]
    internal static class PartSyncThrottle
    {
        private const float PruneSeconds = 10f;
        private static readonly Dictionary<(uint ship, ushort part), float> LastSent = new Dictionary<(uint, ushort), float>();
        private static readonly HashSet<(uint ship, ushort part)> Pending = new HashSet<(uint, ushort)>();
        private static readonly List<(uint ship, ushort part)> Due = new List<(uint, ushort)>();
        private static readonly AccessTools.FieldRef<MasterSync, ShipsServerTracker> Ships = AccessTools.FieldRefAccess<MasterSync, ShipsServerTracker>("_shipsServer");
        private static readonly Action<MasterSync, IShipChange> Queue = AccessTools.MethodDelegate<Action<MasterSync, IShipChange>>(AccessTools.Method(typeof(MasterSync), "QueueChange"));
        private static MasterSync _sync;
        private static float _nextPrune;
        private static bool _loggedError;

        private static float Interval => MiningMod.PartSyncPerSecond.Value > 0f ? 1f / MiningMod.PartSyncPerSecond.Value : 0f;

        private static bool Prefix(MasterSync __instance, uint shipId, StatefulPartStateChangeEvent partState)
        {
            float interval = Interval;
            if (interval <= 0f || partState.PreviousState == null || partState.PreviousState.Equals(partState.NewState))
            {
                return true;
            }
            var key = (shipId, partState.PartId);
            float now = Time.unscaledTime;
            if (!LastSent.TryGetValue(key, out float last) || now - last >= interval)
            {
                LastSent[key] = now;
                Pending.Remove(key);
                return true;
            }
            _sync = __instance;
            Pending.Add(key);
            return false;
        }

        internal static void Flush()
        {
            if (Pending.Count == 0 || _sync == null)
            {
                Prune();
                return;
            }
            try
            {
                float now = Time.unscaledTime, interval = Interval;
                foreach (var key in Pending)
                {
                    if (!LastSent.TryGetValue(key, out float last) || now - last >= interval)
                    {
                        Due.Add(key);
                    }
                }
                ShipsServerTracker ships = Ships(_sync);
                foreach (var key in Due)
                {
                    Pending.Remove(key);
                    LastSent[key] = now;
                    TrackedShipServer ship = ships?.GetShip(key.ship);
                    if (ship != null && ship.TryGetStatefulPart(key.part, out StatefulPart part))
                    {
                        Queue(_sync, new UpdateStatefulPart { ShipId = key.ship, PartId = key.part, State = part.State });
                    }
                }
            }
            catch (Exception e)
            {
                Pending.Clear();
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Part sync throttle: {e}");
                }
            }
            Due.Clear();
            Prune();
        }

        private static void Prune()
        {
            float now = Time.unscaledTime;
            if (now < _nextPrune)
            {
                return;
            }
            _nextPrune = now + PruneSeconds;
            foreach (var pair in LastSent)
            {
                if (now - pair.Value > PruneSeconds && !Pending.Contains(pair.Key)) Due.Add(pair.Key);
            }
            foreach (var key in Due) LastSent.Remove(key);
            Due.Clear();
        }
    }

    [HarmonyPatch(typeof(WorldPhysicsServerUpdater), "PostPhysics")]
    internal static class PartSyncFlushPatch
    {
        private static void Postfix() => PartSyncThrottle.Flush();
    }
}
