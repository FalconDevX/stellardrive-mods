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
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the barrier: {e}");
            }
            return _part;
        }

        private static Mesh BuildMesh()
        {
            var b = new MeshBuilder();
            float half = Width / 2f - Post / 2f;
            foreach (float x in new[] { -half, half })
            {
                b.Box(new Vector3(x, Height / 2f, 0f), new Vector3(Post, Height, Post), "yellow");
                b.Box(new Vector3(x, 0.01f, 0f), new Vector3(0.16f, 0.02f, 0.16f), "dark");
                b.Box(new Vector3(x, Height + 0.01f, 0f), new Vector3(Post + 0.02f, 0.02f, Post + 0.02f), "black");
            }
            Rail(b, Height - 0.05f, 0.07f, Depth);
            Rail(b, 0.55f, 0.06f, Depth * 0.8f);
            Rail(b, 0.1f, 0.12f, 0.02f);
            return b.Build("YellowBarrier");
        }

        private static void Rail(MeshBuilder b, float y, float height, float depth)
        {
            const int stripes = 8;
            float start = -Width / 2f + Post, length = Width - 2f * Post, step = length / stripes;
            for (int i = 0; i < stripes; i++)
            {
                b.Box(new Vector3(start + step * (i + 0.5f), y, 0f), new Vector3(step, height, depth), i % 2 == 0 ? "yellow" : "black");
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
            Mesh mesh = BuildMesh();
            PartFactory.CreateRenderer("Body", visual, mesh, material, lookCopy);
            Object.DestroyImmediate(lookCopy.gameObject);
            var bounds = new Bounds(new Vector3(0f, (Height + 0.02f) / 2f, 0f), new Vector3(Width, Height + 0.02f, 0.16f));
            PartFactory.SetBounds(root, bounds);
            Transform interactions = PartFactory.RemoveInteract(root);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                box.center = box.transform.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = bounds.size + Vector3.one * 0.02f;
            }

            PartSettings settings = Object.Instantiate(bed);
            settings.name = "MiningYellowBarrier";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Yellow Barrier";
            settings.description = "A 1 m yellow safety barrier with hazard stripes. It always stands upright on the floor; rotate it to line up railings along an edge.";
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
}
