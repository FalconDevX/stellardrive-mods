using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class Barrier
    {
        internal const ushort PartId = 234;
        internal const ushort BedPartId = 43;
        private const float Width = 1f, Height = 1f, Post = 0.07f, Depth = 0.06f;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings bed = PartFactory.Find(parts, BedPartId);
            PartSettings tank = PartFactory.Find(parts, 23);
            if (bed?.part == null || tank?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(bed, tank);
                Melon<BuildingMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<BuildingMod>.Logger.Error($"Could not build the barrier: {e}");
            }
            return _part;
        }

        internal const float HalfLength = Width / 2f;

        private static Mesh BuildRails()
        {
            var b = new MeshBuilder();
            Rail(b, Height - 0.05f, 0.03f);
            Rail(b, 0.55f, 0.025f);
            Rail(b, 0.15f, 0.02f);
            return b.Build("YellowBarrierRails");
        }

        private static Mesh BuildPost()
        {
            var b = new MeshBuilder();
            float postRadius = Post / 2f;
            b.Cylinder(new Vector3(0f, 0.03f, 0f), new Vector3(0f, Height - 0.02f, 0f), postRadius, "yellow", 20);
            b.Sphere(new Vector3(0f, Height - 0.02f, 0f), postRadius * 1.15f, "yellow", 16);
            b.Cylinder(new Vector3(0f, 0.03f, 0f), new Vector3(0f, 0.09f, 0f), postRadius * 1.35f, "black", 20);
            b.Box(new Vector3(0f, 0.015f, 0f), new Vector3(0.14f, 0.03f, 0.14f), "dark");
            foreach (float y in new[] { Height - 0.05f, 0.55f, 0.15f })
            {
                b.Cylinder(new Vector3(0f, y - 0.035f, 0f), new Vector3(0f, y + 0.035f, 0f), postRadius * 1.3f, "dark", 20);
            }
            return b.Build("YellowBarrierPost");
        }

        private static void Rail(MeshBuilder b, float y, float radius)
        {
            const int stripes = 8;
            float start = -Width / 2f + Post / 2f, length = Width - Post, step = length / stripes;
            for (int i = 0; i < stripes; i++)
            {
                b.Cylinder(new Vector3(start + step * i, y, 0f), new Vector3(start + step * (i + 1), y, 0f), radius, i % 2 == 0 ? "yellow" : "black", 16);
            }
        }

        private static PartSettings Build(PartSettings bed, PartSettings tank)
        {
            GameObject root = Object.Instantiate(bed.part, PartFactory.Holder);
            root.name = "YellowBarrier";
            MeshRenderer look = tank.part.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Albedo"))
                                ?? root.GetComponentInChildren<MeshRenderer>(true);
            DefaultShipPartVisuals visuals = root.GetComponentInChildren<DefaultShipPartVisuals>(true);
            Transform visual = visuals != null ? visuals.transform : root.transform.Find("Visuals") ?? root.transform.Find("Visual");
            if (visual == null || visual == root.transform)
            {
                throw new InvalidOperationException("The bed has no separate visuals object");
            }
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "YellowBarrier");
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (MeshRenderer r in visual.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
            foreach (MeshFilter f in visual.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
            if (visuals != null)
            {
                Object.DestroyImmediate(visuals);
                visual.gameObject.AddComponent<DefaultShipPartVisuals>();
            }
            PartFactory.CreateRenderer("Body", visual, BuildRails(), material, lookCopy);
            Mesh post = BuildPost();
            PartFactory.CreateRenderer(BarrierVisuals.LeftPost, visual, post, material, lookCopy).transform.localPosition = new Vector3(-HalfLength, 0f, 0f);
            PartFactory.CreateRenderer(BarrierVisuals.RightPost, visual, post, material, lookCopy).transform.localPosition = new Vector3(HalfLength, 0f, 0f);
            visual.gameObject.AddComponent<BarrierVisuals>();
            Object.DestroyImmediate(lookCopy.gameObject);
            var bounds = new Bounds(new Vector3(0f, Height / 2f, 0f), new Vector3(Width * 0.9f, Height, 0.12f));
            PartFactory.SetBounds(root, bounds);
            Transform interactions = PartFactory.RemoveInteract(root);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                box.center = box.transform.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = bounds.size;
            }

            PartSettings settings = Object.Instantiate(bed);
            settings.name = "MiningYellowBarrier";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Yellow Barrier";
            settings.description = "A 1 m yellow safety barrier with hazard stripes. It always stands upright on the floor; barriers placed side by side share their posts and form one railing.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.size = new Vector3(1f, 1f, 1f);
            settings.mass = 8f;
            settings.thumbnailRotation = Quaternion.Euler(0f, 30f, 0f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 3));
            return settings;
        }
    }

    internal sealed class BarrierVisuals : MonoBehaviour
    {
        internal const string LeftPost = "PostLeft", RightPost = "PostRight";
        private const float Snap = 0.08f, RefreshInterval = 5f;
        private static readonly HashSet<BarrierVisuals> Active = new HashSet<BarrierVisuals>();
        private static readonly Dictionary<Vector3Int, List<(Vector3 pos, int rank)>> Ends = new Dictionary<Vector3Int, List<(Vector3, int)>>();
        private static readonly Stack<List<(Vector3, int)>> Pool = new Stack<List<(Vector3, int)>>();
        private static int _version, _gridVersion = -1, _gridFrame = -1;
        private static float _nextRefresh;
        private Transform _left, _right;
        private int _evaluated = -1;

        internal static void Invalidate() => _version++;

        private void OnEnable()
        {
            Active.Add(this);
            _left = transform.Find(LeftPost);
            _right = transform.Find(RightPost);
            _evaluated = -1;
            Invalidate();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            Invalidate();
        }

        private Vector3 End(float side) => transform.TransformPoint(new Vector3(side * Barrier.HalfLength, 0f, 0f));

        private void LateUpdate()
        {
            if (_left == null)
            {
                return;
            }
            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshInterval;
                Invalidate();
            }
            if (_evaluated == _version)
            {
                return;
            }
            _evaluated = _version;
            EnsureGrid();
            UpdatePost(_left, -1f);
            UpdatePost(_right, 1f);
        }

        private int Rank(float side) => GetInstanceID() * 2 + (side < 0f ? 0 : 1);

        private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Snap), Mathf.FloorToInt(p.y / Snap), Mathf.FloorToInt(p.z / Snap));

        private static void Add(Vector3 pos, int rank)
        {
            Vector3Int cell = Cell(pos);
            if (!Ends.TryGetValue(cell, out List<(Vector3, int)> list))
            {
                list = Pool.Count > 0 ? Pool.Pop() : new List<(Vector3, int)>();
                Ends[cell] = list;
            }
            list.Add((pos, rank));
        }

        private static void EnsureGrid()
        {
            if (_gridVersion == _version && _gridFrame == Time.frameCount)
            {
                return;
            }
            _gridVersion = _version;
            _gridFrame = Time.frameCount;
            foreach (List<(Vector3, int)> list in Ends.Values)
            {
                list.Clear();
                Pool.Push(list);
            }
            Ends.Clear();
            foreach (BarrierVisuals barrier in Active)
            {
                if (barrier == null) continue;
                Add(barrier.End(-1f), barrier.Rank(-1f));
                Add(barrier.End(1f), barrier.Rank(1f));
            }
            foreach (StairsVisuals stairs in StairsVisuals.Active)
            {
                if (stairs == null) continue;
                foreach (Vector3 anchor in Stairs.Anchors)
                {
                    Add(stairs.transform.TransformPoint(anchor), int.MinValue);
                }
            }
        }

        private void UpdatePost(Transform post, float side)
        {
            if (post == null)
            {
                return;
            }
            Vector3 mine = End(side);
            int rank = Rank(side);
            bool hidden = false;
            Vector3Int cell = Cell(mine);
            for (int x = -1; x <= 1 && !hidden; x++)
            for (int y = -1; y <= 1 && !hidden; y++)
            for (int z = -1; z <= 1 && !hidden; z++)
            {
                if (!Ends.TryGetValue(new Vector3Int(cell.x + x, cell.y + y, cell.z + z), out List<(Vector3 pos, int rank)> list)) continue;
                foreach ((Vector3 pos, int other) in list)
                {
                    if (other < rank && (pos - mine).sqrMagnitude < Snap * Snap)
                    {
                        hidden = true;
                        break;
                    }
                }
            }
            if (post.gameObject.activeSelf == hidden) post.gameObject.SetActive(!hidden);
        }
    }
}
