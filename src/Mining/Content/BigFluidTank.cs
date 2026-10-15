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

        private static Material _housingMaterial;

        private static void AddHousing(GameObject root, Vector3 socket, Vector3 inward, int scale)
        {
            MeshRenderer look = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Model") ?? root.GetComponentInChildren<MeshRenderer>(true);
            if (look == null)
            {
                return;
            }
            if (_housingMaterial == null)
            {
                _housingMaterial = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "TankSocketHousing");
            }
            inward = new Vector3(0f, 0f, Mathf.Sign(inward.z == 0f ? 1f : inward.z));
            float width = 0.1f * scale, depth = Mathf.Clamp(DistanceToBody(root, look, socket, inward, width) + 0.05f * scale, 0.1f * scale, 0.6f * scale), height = socket.y + 0.05f * scale;
            float radius = width / 2f;
            Vector3 front = new Vector3(socket.x, 0f, socket.z) + inward * 0.01f;
            Vector3 center = front + inward * (depth / 2f);
            var b = new MeshBuilder();
            b.Box(center + Vector3.up * ((height - radius) / 2f), new Vector3(width, height - radius, depth), "light");
            b.Cylinder(center + Vector3.up * (height - radius) - inward * (depth / 2f), center + Vector3.up * (height - radius) + inward * (depth / 2f), radius, "light", 20);
            b.Box(new Vector3(socket.x, socket.y, front.z) + inward * 0.012f, new Vector3(width * 0.62f, width * 0.62f, 0.02f), "dark");
            b.Box(center + Vector3.up * 0.02f * scale, new Vector3(width * 1.1f, 0.04f * scale, depth * 1.05f), "grey");
            Transform visuals = root.transform.Find("Visuals") ?? root.transform;
            GameObject housing = PartFactory.CreateRenderer("SocketHousing", visuals, b.Build("TankSocketHousing"), _housingMaterial, look);
            housing.transform.SetParent(visuals, true);
            housing.transform.position = root.transform.position;
            housing.transform.rotation = root.transform.rotation;
            housing.transform.localScale = Vector3.one;
        }

        private static float DistanceToBody(GameObject root, MeshRenderer look, Vector3 socket, Vector3 inward, float width)
        {
            Mesh mesh = look.GetComponent<MeshFilter>()?.sharedMesh;
            float best = float.MaxValue;
            if (mesh != null && mesh.isReadable)
            {
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 p = root.transform.InverseTransformPoint(look.transform.TransformPoint(v));
                    float along = Vector3.Dot(p - socket, inward);
                    if (along > 0f && Mathf.Abs(p.x - socket.x) < width / 2f && p.y > 0.05f && along < best) best = along;
                }
            }
            if (best < float.MaxValue)
            {
                return best;
            }
            Bounds b = look.bounds;
            Vector3 center = root.transform.InverseTransformPoint(b.center);
            float radius = Mathf.Min(b.extents.x, b.extents.z);
            float dx = Mathf.Abs(socket.x - center.x) + width / 2f;
            float surface = dx < radius ? Mathf.Sqrt(radius * radius - dx * dx) : 0f;
            return Mathf.Max(0f, Mathf.Abs(socket.z - center.z) - surface);
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
                AddHousing(root, pos, new Vector3(0f, 0f, mirrorX - pos.z), scale);
                Quaternion r = Quaternion.Inverse(root.transform.rotation) * source.transform.rotation;
                copy.transform.localRotation = new Quaternion(r.x, -r.y, -r.z, r.w);
                Vector3 size = source.transform.lossyScale;
                copy.transform.localScale = new Vector3(-size.x, size.y, size.z);
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
