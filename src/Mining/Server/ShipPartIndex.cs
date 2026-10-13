using System.Collections.Generic;
using Mining.Content;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using UnityEngine;

namespace Mining.Server
{
    internal static class ShipPartIndex
    {
        private const float RescanSeconds = 3f;
        private const int UnusedFrames = 600;

        private sealed class Entry
        {
            public int PartCount = -1;
            public float NextScan;
            public int Used;
            public readonly Dictionary<ushort, List<ushort>> Parts = new Dictionary<ushort, List<ushort>>();
        }

        private static readonly HashSet<ushort> Indexed = new HashSet<ushort>();
        private static readonly Dictionary<uint, Entry> Entries = new Dictionary<uint, Entry>();
        private static readonly List<ushort> None = new List<ushort>();
        private static readonly List<uint> Stale = new List<uint>();

        static ShipPartIndex()
        {
            Indexed.Add(Pontoon.PartId);
            Indexed.Add(LaserCannon.PartId);
            Indexed.Add(Excavator.PartId);
            Indexed.Add(WaterPump.PartId);
            foreach (InfoPanels.Variant variant in InfoPanels.Variants)
            {
                Indexed.Add(variant.Id);
            }
        }

        internal static void Reset() => Entries.Clear();

        internal static List<ushort> Get(TrackedShipServer ship, ushort settingsId)
        {
            Entry entry = Refresh(ship);
            return entry.Parts.TryGetValue(settingsId, out List<ushort> list) ? list : None;
        }

        private static Entry Refresh(TrackedShipServer ship)
        {
            if (!Entries.TryGetValue(ship.Id, out Entry entry))
            {
                Prune();
                Entries[ship.Id] = entry = new Entry();
            }
            entry.Used = Time.frameCount;
            if (entry.PartCount == ship.PartCount && Time.time < entry.NextScan)
            {
                return entry;
            }
            entry.PartCount = ship.PartCount;
            entry.NextScan = Time.time + RescanSeconds;
            foreach (List<ushort> list in entry.Parts.Values)
            {
                list.Clear();
            }
            foreach (StatefulPart part in ship.StatefulParts)
            {
                PartSettings settings = part?.Settings;
                if (settings == null || !Indexed.Contains(settings.id))
                {
                    continue;
                }
                if (!entry.Parts.TryGetValue(settings.id, out List<ushort> list))
                {
                    entry.Parts[settings.id] = list = new List<ushort>();
                }
                list.Add(part.Id);
            }
            return entry;
        }

        private static void Prune()
        {
            int frame = Time.frameCount;
            foreach (KeyValuePair<uint, Entry> pair in Entries)
            {
                if (frame - pair.Value.Used > UnusedFrames) Stale.Add(pair.Key);
            }
            foreach (uint id in Stale)
            {
                Entries.Remove(id);
            }
            Stale.Clear();
        }
    }
}
