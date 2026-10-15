using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class Stairs
    {
        internal const ushort PartId = 235;
        private const ushort BedPartId = 43;
        private const int Steps = 8;
        internal const float Rise = 2f, Run = 2f;
        private const float Width = 1f, Tread = Run / Steps, StepHeight = Rise / Steps;
        private const float Side = Width / 2f - 0.04f, RailHeight = 0.9f;
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
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the stairs: {e}");
            }
            return _part;
        }

        private static float Front(int step) => -Run / 2f + step * Tread;

        private static Mesh BuildBody()
        {
            var b = new MeshBuilder();
            float tread = Width - 0.14f;
            for (int i = 0; i < Steps; i++)
            {
                float top = (i + 1) * StepHeight, front = Front(i), center = front + Tread / 2f;
                b.Box(new Vector3(0f, top - 0.025f, center + 0.01f), new Vector3(tread, 0.05f, Tread + 0.02f), "steel");
                b.Box(new Vector3(0f, top - 0.012f, front + 0.025f), new Vector3(tread, 0.026f, 0.05f), "yellow");
                for (int g = 1; g <= 3; g++)
                {
                    b.Box(new Vector3(0f, top + 0.002f, front + 0.05f + g * 0.045f), new Vector3(tread - 0.08f, 0.006f, 0.014f), "dark");
                }
                b.Box(new Vector3(0f, top - 0.09f, front + Tread - 0.03f), new Vector3(tread, 0.08f, 0.02f), "dark");
            }

            Quaternion slope = Quaternion.Euler(-45f, 0f, 0f);
            float length = Mathf.Sqrt(Rise * Rise + Run * Run) - 0.12f;
            foreach (float x in new[] { -Side, Side })
            {
                b.Box(new Vector3(x, Rise / 2f - 0.06f, 0.06f), new Vector3(0.06f, 0.22f, length), "grey", slope);
                b.Box(new Vector3(x, 0.02f, -Run / 2f + 0.12f), new Vector3(0.12f, 0.04f, 0.2f), "dark");
                b.Box(new Vector3(x, Rise - 0.06f, Run / 2f - 0.05f), new Vector3(0.1f, 0.12f, 0.1f), "dark");
                foreach (int step in new[] { 0, 3, 6 })
                {
                    Vector3 foot = new Vector3(x, (step + 1) * StepHeight, Front(step) + Tread / 2f);
                    b.Cylinder(foot, foot + Vector3.up * RailHeight, 0.022f, "steel", 14);
                    b.Cylinder(foot, foot + Vector3.up * 0.05f, 0.032f, "dark", 14);
                }
                Vector3 low = new Vector3(x, StepHeight + RailHeight, Front(0) + Tread / 2f);
                Vector3 high = new Vector3(x, 7 * StepHeight + RailHeight, Front(6) + Tread / 2f);
                Vector3 along = (high - low).normalized;
                b.Cylinder(low - along * 0.06f, high + along * 0.18f, 0.03f, "yellow", 16);
                b.Sphere(low - along * 0.06f, 0.03f, "yellow", 12);
                b.Sphere(high + along * 0.18f, 0.03f, "yellow", 12);
                Vector3 mid = Vector3.up * (RailHeight * 0.5f);
                b.Cylinder(low - mid, high - mid, 0.016f, "steel", 12);
            }
            return b.Build("MetalStairs");
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
}
