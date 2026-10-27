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
    internal static class Stairs
    {
        internal const ushort PartId = 235;
        private const ushort BedPartId = 43;
        private const int Steps = 8;
        internal const float Rise = 2f, Run = 2f;
        private const float Width = 1f, Tread = Run / Steps, StepHeight = Rise / Steps;
        private const float Side = Width / 2f - 0.04f;
        private static PartSettings _part;
        private static bool _failed;
        private static Mesh _collider;

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
                Melon<BuildingMod>.Logger.Error($"Could not build the stairs: {e}");
            }
            return _part;
        }

        internal const float PostHeight = 1f;
        internal static readonly Vector3[] Anchors =
        {
            new Vector3(-Width / 2f + 0.04f, 0f, -Run / 2f), new Vector3(Width / 2f - 0.04f, 0f, -Run / 2f),
            new Vector3(-Width / 2f + 0.04f, Rise, Run / 2f), new Vector3(Width / 2f - 0.04f, Rise, Run / 2f),
        };

        internal static bool Contains(Vector3 local) =>
            Mathf.Abs(local.x) < Width / 2f - 0.02f && local.y > 0.05f && local.y < Rise - 0.05f && Mathf.Abs(local.z) < Run / 2f - 0.02f;

        private static float Front(int step) => -Run / 2f + step * Tread;

        private static float RailY(float z) => z + Run / 2f + PostHeight - 0.05f;

        private static void Post(MeshBuilder b, Vector3 foot)
        {
            const float radius = 0.035f;
            b.Cylinder(foot + Vector3.up * 0.03f, foot + Vector3.up * (PostHeight - 0.02f), radius, "yellow", 20);
            b.Sphere(foot + Vector3.up * (PostHeight - 0.02f), radius * 1.15f, "yellow", 16);
            b.Cylinder(foot + Vector3.up * 0.03f, foot + Vector3.up * 0.09f, radius * 1.35f, "black", 20);
            b.Box(foot + Vector3.up * 0.015f, new Vector3(0.14f, 0.03f, 0.14f), "dark");
        }

        private const float WebIn = 0.44f, WebOut = 0.49f, StringerTop = 1.3f, StringerBottom = 0.84f;

        private static List<Vector2> Band(float lo, float hi)
        {
            var poly = new List<Vector2> { new Vector2(-3f, -3f + lo), new Vector2(3f, 3f + lo), new Vector2(3f, 3f + hi), new Vector2(-3f, -3f + hi) };
            poly = Clip(poly, p => p.x + Run / 2f);
            poly = Clip(poly, p => Run / 2f - p.x);
            poly = Clip(poly, p => p.y);
            return Clip(poly, p => Rise - p.y);
        }

        private static List<Vector2> Clip(List<Vector2> poly, Func<Vector2, float> inside)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = inside(a), db = inside(b);
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        private static Mesh BuildBody()
        {
            var b = new MeshBuilder();
            float tread = WebIn * 2f + 0.006f;
            for (int i = 0; i < Steps; i++)
            {
                float top = (i + 1) * StepHeight, front = Front(i), center = front + Tread / 2f;
                b.Box(new Vector3(0f, top - 0.025f, center + 0.01f), new Vector3(tread, 0.05f, Tread + 0.02f), "steel");
                b.Box(new Vector3(0f, top - 0.011f, front + 0.0235f), new Vector3(tread + 0.004f, 0.028f, 0.051f), "yellow");
                for (int g = 1; g <= 3; g++)
                {
                    b.Box(new Vector3(0f, top + 0.002f, front + 0.05f + g * 0.045f), new Vector3(tread - 0.08f, 0.006f, 0.014f), "dark");
                }
                b.Box(new Vector3(0f, top - 0.09f, front + Tread - 0.03f), new Vector3(tread, 0.08f, 0.02f), "dark");
                foreach (float sx in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(sx * (WebIn - 0.025f), top - 0.08f, front + 0.14f), new Vector3(0.05f, 0.06f, 0.16f), "dark");
                    foreach (float bz in new[] { front + 0.08f, front + 0.2f })
                    {
                        b.Cylinder(new Vector3(sx * (WebOut - 0.004f), top - 0.08f, bz), new Vector3(sx * (WebOut + 0.012f), top - 0.08f, bz), 0.013f, "steel", 8);
                    }
                }
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * Side;
                b.Prism(Band(StringerBottom, StringerTop), sx * WebIn, sx * WebOut, "grey");
                b.Prism(Band(StringerTop - 0.03f, StringerTop), sx * (WebIn - 0.012f), sx * (WebOut + 0.012f), "dark");
                b.Prism(Band(StringerBottom, StringerBottom + 0.03f), sx * (WebIn - 0.012f), sx * (WebOut + 0.012f), "dark");
                b.Box(new Vector3(sx * (WebIn + WebOut) / 2f, 0.01f, -Run / 2f + 0.08f), new Vector3(0.1f, 0.02f, 0.16f), "dark");
                b.Box(new Vector3(sx * (WebIn + WebOut) / 2f, Rise + 0.0075f, Run / 2f - 0.14f), new Vector3(0.1f, 0.015f, 0.28f), "dark");
                foreach (float z in new[] { -0.5f, 0f, 0.5f })
                {
                    float y = z + StringerTop;
                    b.Cylinder(new Vector3(x, y - 0.04f, z), new Vector3(x, RailY(z), z), 0.022f, "steel", 14);
                    b.Cylinder(new Vector3(x, y - 0.01f, z), new Vector3(x, y + 0.05f, z), 0.034f, "dark", 14);
                }
                foreach (Vector3 end in new[] { new Vector3(x, 0f, -Run / 2f), new Vector3(x, Rise, Run / 2f) })
                {
                    Post(b, end);
                }
                Vector3 low = new Vector3(x, RailY(-Run / 2f), -Run / 2f);
                Vector3 high = new Vector3(x, RailY(Run / 2f), Run / 2f);
                b.Cylinder(low, high, 0.03f, "yellow", 16);
                Vector3 mid = Vector3.up * 0.45f;
                b.Cylinder(low - mid, high - mid, 0.02f, "yellow", 12);
            }
            foreach (float z in new[] { -0.35f, 0.35f })
            {
                float y = z + StringerBottom + 0.05f;
                b.Cylinder(new Vector3(-WebIn - 0.004f, y, z), new Vector3(WebIn + 0.004f, y, z), 0.025f, "dark", 12);
            }
            return b.Build("MetalStairs");
        }

        private static readonly Mesh[] Rails = new Mesh[2];

        internal static Mesh RailColliderMesh(int side)
        {
            if (Rails[side] != null)
            {
                return Rails[side];
            }
            float outer = (side == 0 ? -1f : 1f) * Width / 2f, inner = outer - (side == 0 ? -1f : 1f) * 0.08f;
            var profile = new[] { new Vector2(-Run / 2f, 0f), new Vector2(Run / 2f, Rise), new Vector2(Run / 2f, Rise + PostHeight), new Vector2(-Run / 2f, PostHeight) };
            var vertices = new List<Vector3>();
            foreach (float x in new[] { outer, inner })
            {
                vertices.AddRange(profile.Select(p => new Vector3(x, p.y, p.x)));
            }
            var mesh = new Mesh { name = "MetalStairsRail" + side };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return Rails[side] = mesh;
        }

        internal static Mesh ColliderMesh()
        {
            if (_collider != null)
            {
                return _collider;
            }
            float h = Width / 2f, back = -Run / 2f - Tread / 2f, top = Run / 2f - Tread / 2f;
            var profile = new[] { new Vector2(back, 0f), new Vector2(Run / 2f, 0f), new Vector2(Run / 2f, Rise), new Vector2(top, Rise) };
            var vertices = new List<Vector3>();
            foreach (float x in new[] { -h, h })
            {
                vertices.AddRange(profile.Select(p => new Vector3(x, p.y, p.x)));
            }
            _collider = new Mesh { name = "MetalStairsCollider" };
            _collider.SetVertices(vertices);
            _collider.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 }, 0);
            _collider.RecalculateNormals();
            _collider.RecalculateBounds();
            _collider.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _collider;
        }

        private static PartSettings Build(PartSettings bed, PartSettings tank)
        {
            GameObject root = Object.Instantiate(bed.part, PartFactory.Holder);
            root.name = "MetalStairs";
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
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "MetalStairs");
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
            PartFactory.CreateRenderer("Body", visual, BuildBody(), material, lookCopy);
            visual.gameObject.AddComponent<StairsVisuals>();
            Object.DestroyImmediate(lookCopy.gameObject);
            var bounds = new Bounds(new Vector3(0f, Rise / 2f, 0f), new Vector3(Width * 0.98f, Rise * 0.98f, Run * 0.98f));
            PartFactory.SetBounds(root, bounds);
            Transform interactions = PartFactory.RemoveInteract(root);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                box.center = box.transform.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = bounds.size;
            }

            PartSettings settings = Object.Instantiate(bed);
            settings.name = "MiningMetalStairs";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Metal Stairs";
            settings.description = "Steel stairs at 45 degrees: 1 m wide, 2 m long, they climb exactly one deck (2 m) in 8 steps, with handrails on both sides. Cut a 1 x 2 hole in the floor above.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.size = new Vector3(Width, Rise, Run);
            settings.mass = 60f;
            settings.thumbnailRotation = Quaternion.Euler(0f, 60f, 0f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 12));
            return settings;
        }
    }

    internal sealed class StairsVisuals : MonoBehaviour
    {
        internal static readonly HashSet<StairsVisuals> Active = new HashSet<StairsVisuals>();

        private void OnEnable()
        {
            Active.Add(this);
            BarrierVisuals.Invalidate();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            BarrierVisuals.Invalidate();
        }
    }
}
