using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using Ships.Services;
using Ships.Interface.Services;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Odyssey.Blueprints
{
    internal static class GhostMaterials
    {
        private static Material _material;

        internal static Material Material
        {
            get
            {
                if (_material != null)
                {
                    return _material;
                }
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                _material = new Material(shader) { name = "BlueprintGhost", hideFlags = HideFlags.DontUnloadUnusedAsset };
                _material.SetColor("_BaseColor", Color.white);
                _material.SetColor("_Color", Color.white);
                _material.SetFloat("_Surface", 1f);
                _material.SetFloat("_Blend", 0f);
                _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _material.SetFloat("_ZWrite", 0f);
                _material.SetFloat("_Cull", 0f);
                _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _material.renderQueue = 3050;
                return _material;
            }
        }
    }

    internal sealed class GhostMeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Color32> _c = new List<Color32>();
        private readonly List<int> _t = new List<int>();

        private static readonly Vector3[] Corners =
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
        };

        private static readonly int[] Faces = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5 };

        internal void Box(Vector3 center, Vector3 size, Quaternion rotation, Color32 color)
        {
            int start = _v.Count;
            foreach (Vector3 corner in Corners)
            {
                _v.Add(center + rotation * Vector3.Scale(corner, size * 0.5f));
                _c.Add(color);
            }
            foreach (int i in Faces) _t.Add(start + i);
        }

        internal Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetTriangles(_t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    internal static class GhostMeshes
    {
        private static readonly Color32 HullColor = new Color32(70, 160, 255, 70);
        private static readonly Color32 PartColor = new Color32(120, 210, 255, 110);
        private static readonly Dictionary<BlueprintData, Mesh> Cache = new Dictionary<BlueprintData, Mesh>();

        internal static Mesh For(BlueprintData data)
        {
            if (Cache.TryGetValue(data, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            var b = new GhostMeshBuilder();
            var provider = new PartSettingsProvider();
            PartSettings Find(int id)
            {
                try { return provider.GetPartSettingById((ushort)id); }
                catch (ArgumentOutOfRangeException) { return null; }
            }
            foreach (BlueprintHull h in data.hull)
            {
                HullSlab(b, new Vector3Int(h.x, h.y, h.z), (VoxelPartType)h.t, HullColor);
            }
            foreach (BlueprintPart p in data.parts)
            {
                PartSettings settings = Find(p.s);
                if (p.onHull)
                {
                    var placement = new HullPartPlacement(new Vector3Int(p.hx, p.hy, p.hz), (VoxelPartType)p.ht, (PartOrientation)p.f, (PartOrientation)p.u);
                    IEnumerable<Vector3Int> coords = settings != null ? placement.CalculateAllCoords(settings.size) : new[] { placement.Coord };
                    foreach (Vector3Int c in coords) HullSlab(b, c, (VoxelPartType)p.ht, PartColor);
                    continue;
                }
                Quaternion rot = Quaternion.LookRotation(((PartOrientation)p.f).GetVector3(), ((PartOrientation)p.u).GetVector3());
                var bounds = settings?.part != null ? settings.part.GetComponent<ShipPartBounds>() : null;
                Vector3 center = bounds != null ? bounds.center : new Vector3(0f, 0.5f, 0f);
                Vector3 size = bounds != null ? bounds.bounds : Vector3.one * 0.8f;
                b.Box(new Vector3(p.x, p.y, p.z) + rot * center, size * 0.98f, rot, PartColor);
            }
            mesh = b.Build("BlueprintGhost_" + data.name);
            Cache[data] = mesh;
            return mesh;
        }

        private static void HullSlab(GhostMeshBuilder b, Vector3Int v, VoxelPartType type, Color32 color)
        {
            const float thin = 0.08f;
            switch (type)
            {
                case VoxelPartType.Floor: b.Box(v + new Vector3(0f, -0.5f, 0f), new Vector3(1f, thin, 1f), Quaternion.identity, color); break;
                case VoxelPartType.ForwardWall: b.Box(v + new Vector3(0f, 0f, 0.5f), new Vector3(1f, 1f, thin), Quaternion.identity, color); break;
                case VoxelPartType.RightWall: b.Box(v + new Vector3(0.5f, 0f, 0f), new Vector3(thin, 1f, 1f), Quaternion.identity, color); break;
                default: b.Box(v, Vector3.one * 0.9f, Quaternion.identity, color); break;
            }
        }

        internal static void Forget(BlueprintData data)
        {
            if (Cache.TryGetValue(data, out Mesh mesh) && mesh != null) Object.Destroy(mesh);
            Cache.Remove(data);
        }
    }

    internal sealed class BlueprintGhost
    {
        public BlueprintData Data;
        public BlueprintTarget Target;
        private GameObject _root;
        private Transform _body;

        internal Transform Body => _body;

        internal void Update()
        {
            if (_root == null)
            {
                _root = new GameObject("BlueprintGhost");
                Object.DontDestroyOnLoad(_root);
                var body = new GameObject("Body");
                body.transform.SetParent(_root.transform, false);
                body.AddComponent<MeshFilter>().sharedMesh = GhostMeshes.For(Data);
                var r = body.AddComponent<MeshRenderer>();
                r.sharedMaterial = GhostMaterials.Material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _body = body.transform;
            }
            bool visible = Place();
            if (_root.activeSelf != visible) _root.SetActive(visible);
        }

        private bool Place()
        {
            if (Target.NewShip)
            {
                if (_root.transform.parent != null) _root.transform.SetParent(null, false);
                try
                {
                    var transpose = ServiceCache.Get<IWorldTransposeClient>();
                    _root.transform.SetPositionAndRotation(
                        transpose.Position(Target.World.SpaceId, ClientOrigin.LocalVisualSpaceId, Target.World.Position),
                        transpose.Rotation(Target.World.SpaceId, ClientOrigin.LocalVisualSpaceId, Target.World.Rotation));
                }
                catch (Exception)
                {
                    return false;
                }
                _body.localPosition = Target.Transform.Offset;
                _body.localRotation = Quaternion.identity;
                return true;
            }
            var ships = ServiceCache.Get<ShipsClientTracker>();
            if (ships == null || !ships.TryGetTrackedShip(Target.ShipId, out TrackedShipClient ship) || !ship.IsVisualLoaded || ship.VisualObject == null)
            {
                return false;
            }
            Transform parent = ship.VisualObject.transform;
            if (_root.transform.parent != parent)
            {
                _root.transform.SetParent(parent, false);
                _root.transform.localPosition = Vector3.zero;
                _root.transform.localRotation = Quaternion.identity;
            }
            _body.localPosition = Target.Transform.Offset;
            _body.localRotation = Target.Transform.Rotation;
            return true;
        }

        internal bool Raycast(Ray ray, out float distance)
        {
            distance = 0f;
            if (_root == null || !_root.activeInHierarchy)
            {
                return false;
            }
            var local = new Ray(_body.InverseTransformPoint(ray.origin), _body.InverseTransformDirection(ray.direction));
            Bounds b = GhostMeshes.For(Data).bounds;
            b.Expand(0.2f);
            return b.IntersectRay(local, out distance);
        }

        internal void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }
    }

    [Serializable]
    internal class GhostRecord
    {
        public BlueprintData data;
        public bool newShip;
        public long shipId, spaceId;
        public int turns, ox, oy, oz;
        public float px, py, pz, rx, ry, rz, rw;
    }

    [Serializable]
    internal class GhostRecordList
    {
        public List<GhostRecord> ghosts = new List<GhostRecord>();
    }

    internal static class BlueprintGhosts
    {
        private static readonly List<BlueprintGhost> Ghosts = new List<BlueprintGhost>();
        private static string _loadedFor;
        private static Type _loadDataType;

        internal static IReadOnlyList<BlueprintGhost> All => Ghosts;

        internal static void Add(BlueprintData data, BlueprintTarget target)
        {
            Ghosts.Add(new BlueprintGhost { Data = data, Target = target });
            Persist();
        }

        internal static void Remove(BlueprintGhost ghost)
        {
            ghost.Destroy();
            Ghosts.Remove(ghost);
            Persist();
        }

        internal static void Clear()
        {
            foreach (BlueprintGhost g in Ghosts) g.Destroy();
            Ghosts.Clear();
            _loadedFor = null;
        }

        internal static void Update()
        {
            if (ServiceCache.Get<ShipsClientTracker>() == null)
            {
                return;
            }
            if (_loadedFor == null)
            {
                foreach (BlueprintGhost g in Ghosts) g.Destroy();
                Ghosts.Clear();
                _loadedFor = WorldKey();
                Load();
            }
            foreach (BlueprintGhost g in Ghosts) g.Update();
        }

        internal static BlueprintGhost Aimed(Ray ray, float maxDistance)
        {
            BlueprintGhost best = null;
            float bestDistance = maxDistance;
            foreach (BlueprintGhost g in Ghosts)
            {
                if (g.Raycast(ray, out float d) && d < bestDistance)
                {
                    bestDistance = d;
                    best = g;
                }
            }
            return best;
        }

        private static string WorldKey()
        {
            try
            {
                Type type = _loadDataType ?? (_loadDataType = AccessTools.TypeByName("MainGameSceneLoadData"));
                object value = type != null ? (AccessTools.Property(type, "SaveFileFullName")?.GetValue(null) ?? AccessTools.Field(type, "SaveFileFullName")?.GetValue(null)) : null;
                string name = value as string;
                if (!string.IsNullOrEmpty(name))
                {
                    string dir = Path.GetDirectoryName(name);
                    string world = string.IsNullOrEmpty(dir) ? Path.GetFileNameWithoutExtension(name) : Path.GetFileName(dir);
                    return new string(world.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                }
            }
            catch (Exception)
            {
            }
            return "world";
        }

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(BlueprintStore.Directory, "placed");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, (_loadedFor ?? "world") + ".json");
            }
        }

        private static void Persist()
        {
            try
            {
                var list = new GhostRecordList();
                foreach (BlueprintGhost g in Ghosts)
                {
                    BlueprintTarget t = g.Target;
                    list.ghosts.Add(new GhostRecord
                    {
                        data = g.Data, newShip = t.NewShip, shipId = t.ShipId, spaceId = t.World.SpaceId,
                        turns = t.Transform.Turns, ox = t.Transform.Offset.x, oy = t.Transform.Offset.y, oz = t.Transform.Offset.z,
                        px = t.World.Position.x, py = t.World.Position.y, pz = t.World.Position.z,
                        rx = t.World.Rotation.x, ry = t.World.Rotation.y, rz = t.World.Rotation.z, rw = t.World.Rotation.w,
                    });
                }
                File.WriteAllText(FilePath, Newtonsoft.Json.JsonConvert.SerializeObject(list));
            }
            catch (Exception e)
            {
                Melon<GearMod>.Logger.Warning($"Could not save placed blueprints: {e.Message}");
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var list = Newtonsoft.Json.JsonConvert.DeserializeObject<GhostRecordList>(File.ReadAllText(FilePath));
                foreach (GhostRecord r in list?.ghosts ?? new List<GhostRecord>())
                {
                    if (r.data == null) continue;
                    Ghosts.Add(new BlueprintGhost
                    {
                        Data = r.data,
                        Target = new BlueprintTarget
                        {
                            NewShip = r.newShip,
                            ShipId = (uint)r.shipId,
                            Transform = new BlueprintTransform(r.turns, new Vector3Int(r.ox, r.oy, r.oz)),
                            World = new WorldTransform { SpaceId = (uint)r.spaceId, Position = new Vector3(r.px, r.py, r.pz), Rotation = new Quaternion(r.rx, r.ry, r.rz, r.rw) },
                        },
                    });
                }
                Melon<GearMod>.Logger.Msg($"Placed blueprints for '{_loadedFor}': {Ghosts.Count}");
            }
            catch (Exception e)
            {
                Melon<GearMod>.Logger.Warning($"Could not load placed blueprints: {e.Message}");
            }
        }
    }
}
