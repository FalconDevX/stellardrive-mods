using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common.Gauge;
using Ships.Parts.FuelTank;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class BigFluidTank
    {
        internal const ushort ResearchId = 207;
        internal const ushort TankPartId = 23;
        internal const ushort LargeId = 215;

        internal static readonly (ushort id, string name, int scale, int iron, int glass)[] Variants =
        {
            (218, "Tank 2x2", 2, 25, 5),
            (LargeId, "Tank 4x4", 4, 60, 10),
        };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static PartSettings[] All => Variants.Select(v => Get(v.id)).Where(p => p != null).ToArray();

        internal static float Capacity(int scale) => 100f * scale * scale * scale;

        internal static bool TryGetCapacity(PartSettings settings, out float capacity)
        {
            capacity = 0f;
            if (settings == null) return false;
            foreach (var v in Variants)
            {
                if (v.id == settings.id)
                {
                    capacity = Capacity(v.scale);
                    return true;
                }
            }
            return false;
        }

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            PartSettings tank = PartFactory.Find(gameParts, TankPartId);
            if (tank?.part == null)
            {
                return null;
            }
            var variant = Variants.First(v => v.id == id);
            try
            {
                PartSettings part = Build(tank, variant);
                Parts[id] = part;
                Melon<MiningMod>.Logger.Msg($"Added part '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build tank {variant.name}: {e}");
                return null;
            }
        }

        private static PartSettings Build(PartSettings tank, (ushort id, string name, int scale, int iron, int glass) v)
        {
            GameObject root = Object.Instantiate(tank.part, PartFactory.Holder);
            root.name = "MiningTank" + v.scale;
            PartFactory.ScaleUniform(root, tank.size, v.scale, "Tank");
            AddMirroredSockets(root, v.scale);
            float capacity = Capacity(v.scale);
            foreach (FluidTankVisuals visuals in root.GetComponentsInChildren<FluidTankVisuals>(true))
            {
                visuals.gameObject.AddComponent<BigFluidTankMarker>().Capacity = capacity;
            }

            PartSettings settings = Object.Instantiate(tank);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.id;
            settings.fullLabel = v.name;
            settings.description = $"A {v.scale} x {v.scale} x {v.scale} fluid tank: holds {capacity:0} units ({v.scale * v.scale * v.scale} normal tanks). " +
                                   "Four pipe sockets, gauge and fill-level signal like the normal tank.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = tank.size * v.scale;
            settings.part = root;
            settings.mass = tank.mass * v.scale * v.scale / 2f;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.iron), (Items.Model.ItemsConstants.GlassItemId, v.glass));
            return settings;
        }

        private static void AddMirroredSockets(GameObject root, int scale)
        {
            SocketObject[] all = root.GetComponentsInChildren<SocketObject>(true);
            List<SocketObject> fluids = all.Where(x => x.Type == CableType.Fluid).OrderBy(x => x.Id).ToList();
            if (fluids.Count == 0)
            {
                throw new InvalidOperationException("The game tank has no pipe sockets");
            }
            bool relayed = fluids.Any(f => f.RelayedSockets != null && f.RelayedSockets.Any());
            float mirrorX = (scale - 1) * 0.5f;
            var ids = new HashSet<byte>(all.Select(x => x.Id));
            byte nextId = 0;
            foreach (SocketObject source in fluids.ToArray())
            {
                while (ids.Contains(nextId)) nextId++;
                Vector3 p = root.transform.InverseTransformPoint(source.transform.position);
                var pos = new Vector3(2f * mirrorX - p.x, p.y, p.z);
                SocketObject copy = PartFactory.CloneSocket(source, root.transform, nextId, pos, 0f);
                copy.transform.rotation = source.transform.rotation;
                copy.transform.localScale = source.transform.lossyScale;
                ids.Add(nextId);
                fluids.Add(copy);
            }
            if (relayed)
            {
                foreach (SocketObject f in fluids)
                {
                    Traverse.Create(f).Field("relayedSockets").SetValue(fluids.Where(o => o != f).ToArray());
                }
            }
            Melon<MiningMod>.Logger.Msg($"{root.name}: pipe sockets {string.Join(", ", fluids.Select(f => $"{f.Id}@{root.transform.InverseTransformPoint(f.transform.position)}"))}, relayed={relayed}");
        }
    }

    internal sealed class BigFluidTankMarker : MonoBehaviour
    {
        public float Capacity = 100f;
    }
}
