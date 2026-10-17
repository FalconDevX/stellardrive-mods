using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Door;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class BigDoors
    {
        internal const ushort ResearchId = 206;
        internal const ushort DoorPartId = 14;
        internal const byte SignalSocket = 0, BackSignalSocket = 1;

        internal static readonly (ushort id, string name, int width, int height, int iron)[] Variants =
        {
            (210, "Door 3x3", 3, 3, 18),
            (211, "Door 4x4", 4, 4, 32),
            (212, "Wide Door 6x3", 6, 3, 36),
            (213, "Hangar Door 10x5", 10, 5, 100),
            (216, "Hangar Door 16x8", 16, 8, 220),
            (217, "Wide Door 3x2", 3, 2, 12),
            (223, "Hangar Door 24x8", 24, 8, 330),
            (224, "Hangar Door 32x8", 32, 8, 440),
            (244, "Narrow Door 1x2", 1, 2, 6),
            (253, "Floor Hatch 2x2", 2, 2, 10),
            (254, "Floor Hatch 3x3", 3, 3, 20),
            (255, "Floor Hatch 4x4", 4, 4, 34),
            (256, "Floor Hatch 6x6", 6, 6, 72),
            (257, "Floor Hatch 8x8", 8, 8, 128),
            (258, "Floor Hatch 12x12", 12, 12, 290),
            (259, "Floor Hatch 16x16", 16, 16, 512),
        };

        private const string HatchPrefix = "Floor Hatch";

        internal static bool IsHatch(PartSettings part) => part != null && Variants.Any(v => v.id == part.id && v.name.StartsWith(HatchPrefix));

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static PartSettings[] All => Variants.Select(v => Get(v.id)).Where(p => p != null).ToArray();

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            List<PartSettings> list = gameParts.ToList();
            var door = PartFactory.Find(list, DoorPartId) as HullPartSettings;
            PartSettings chest = PartFactory.Find(list, 30);
            if (door?.part == null || chest?.part == null)
            {
                return null;
            }
            var variant = Variants.First(v => v.id == id);
            try
            {
                PartSettings part = Build(door, chest, variant);
                Parts[id] = part;
                Melon<MiningMod>.Logger.Msg($"Added part '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build door {variant.name}: {e}");
                return null;
            }
        }

        private static PartSettings Build(HullPartSettings door, PartSettings chest, (ushort id, string name, int width, int height, int iron) v)
        {
            GameObject root = Object.Instantiate(door.part, PartFactory.Holder);
            root.name = "MiningDoor" + v.width + "x" + v.height;
            Scale(root, v.width / door.size.x, v.height / door.size.y);
            RebuildColliders(root);
            CoverInteraction(root);
            ReplaceLook(root, chest);
            SetupLeafColliders(root);
            AddSignalSocket(root);

            HullPartSettings settings = Object.Instantiate(door);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.id;
            settings.fullLabel = v.name;
            settings.description = $"A {v.width} x {v.height} door. Opens and closes like the normal door and seals the room when closed. Signal 1 on a socket on the left post (one on each side) opens it, 0 closes it.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = new Vector3(v.width, v.height, door.size.z);
            if (v.name.StartsWith(HatchPrefix))
            {
                settings.snappingStyle = SnappingStyle.Floor;
                settings.defaultForward = PartOrientation.Up;
                settings.defaultUp = PartOrientation.Right;
                settings.size = new Vector3(v.height, 1f, v.width);
                settings.description = $"A {v.width} x {v.height} hatch built into the floor. Opens and closes like a door and seals the room below or above when closed. Signal 1 on its socket opens it, 0 closes it.";
            }
            settings.part = root;
            settings.pivotedSettings = null;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.iron), (112, Mathf.Max(2, v.iron / 8)));
            return settings;
        }

        private static void ReplaceLook(GameObject root, PartSettings chest)
        {
            MeshRenderer look = chest.part.transform.Find("Visual")?.GetComponentInChildren<MeshRenderer>(true) ?? chest.part.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "BigDoor");

            var visuals = root.GetComponentInChildren<DefaultShipPartVisuals>(true);
            Transform host = visuals != null ? visuals.transform : root.transform;
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            if (visuals != null) Object.DestroyImmediate(visuals);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(r);
            host.gameObject.AddComponent<BigDoorVisuals>();

            var bounds = root.GetComponent<ShipPartBounds>();
            Matrix4x4 hostToRoot = root.transform.worldToLocalMatrix * host.localToWorldMatrix;
            Transform model = BigDoorModel.Build(host, bounds.GetBounds(), material, look);
            Matrix4x4 inv = hostToRoot.inverse;
            model.localPosition = inv.GetColumn(3);
            model.localRotation = inv.rotation;
            model.localScale = inv.lossyScale;
        }

        private static void SetupLeafColliders(GameObject root)
        {
            DoorDynamicColliders dynamic = root.GetComponentInChildren<DoorDynamicColliders>(true);
            GameObject door = dynamic != null ? Traverse.Create(dynamic).Field<GameObject>("doorCollider").Value : null;
            BoxCollider closed = door != null ? door.GetComponent<BoxCollider>() : null;
            if (closed == null)
            {
                return;
            }
            Vector3 center = root.transform.InverseTransformPoint(door.transform.TransformPoint(closed.center));
            Vector3 size = root.transform.InverseTransformVector(door.transform.TransformVector(closed.size));
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            var leaves = new Dictionary<(string axis, int dir), (float lo, float hi, float distance, float clipLo, float clipHi)>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string[] p = t.name.Split('|');
                if (p.Length != 7 || p[0] != "P")
                {
                    continue;
                }
                int dir = int.Parse(p[2], CultureInfo.InvariantCulture);
                float distance = float.Parse(p[3], CultureInfo.InvariantCulture), clipLo = float.Parse(p[4], CultureInfo.InvariantCulture);
                float clipHi = float.Parse(p[5], CultureInfo.InvariantCulture), width = float.Parse(p[6], CultureInfo.InvariantCulture);
                float mid = p[1] == "x" ? t.localPosition.x : t.localPosition.y;
                var key = (p[1], dir);
                leaves[key] = leaves.TryGetValue(key, out var leaf)
                    ? (Mathf.Min(leaf.lo, mid - width / 2), Mathf.Max(leaf.hi, mid + width / 2), distance, clipLo, clipHi)
                    : (mid - width / 2, mid + width / 2, distance, clipLo, clipHi);
            }
            if (leaves.Count == 0)
            {
                return;
            }
            Object.DestroyImmediate(closed);
            float travel = 0f;
            foreach (var pair in leaves)
            {
                var (lo, hi, distance, clipLo, clipHi) = pair.Value;
                travel = Mathf.Max(travel, distance);
                Vector3 c = center, s = size;
                if (pair.Key.axis == "x") { c.x = (lo + hi) / 2; s.x = hi - lo; }
                else { c.y = (lo + hi) / 2; s.y = hi - lo; }
                string name = string.Format(CultureInfo.InvariantCulture, "{0}{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                    BigDoorLeafColliders.LeafPrefix, pair.Key.axis, pair.Key.dir, distance, lo, hi, clipLo, clipHi);
                AddBox(root, ColliderObject(name, door.transform, door.layer, door.tag), c, s);
            }
            door.name = BigDoorLeafColliders.DoorPrefix + (1.2f + 0.5f * travel).ToString(CultureInfo.InvariantCulture);
            dynamic.gameObject.AddComponent<BigDoorLeafColliders>();
        }

        private static void AddSignalSocket(GameObject root)
        {
            SocketObject source = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (source == null)
            {
                Melon<MiningMod>.Logger.Warning($"Door {root.name}: no signal socket found to copy");
                return;
            }
            if (root.GetComponentsInChildren<SocketObject>(true).Any(s => s.Id == SignalSocket || s.Id == BackSignalSocket))
            {
                Melon<MiningMod>.Logger.Warning($"Door {root.name}: socket {SignalSocket} or {BackSignalSocket} already used - no signal input");
                return;
            }
            Bounds b = root.GetComponent<ShipPartBounds>().GetBounds();
            float frame = BigDoorModel.SideFrame(b), depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f);
            float y = BigDoorModel.SocketY(b);
            float face = depth / 2 + BigDoorModel.PlateDepth + 0.002f;
            PartFactory.CloneSocket(source, root.transform, SignalSocket, new Vector3(b.min.x + frame / 2, y, b.center.z + face), 180f);
            PartFactory.CloneSocket(source, root.transform, BackSignalSocket, new Vector3(b.min.x + frame / 2, y, b.center.z - face), 0f);
        }

        private static void CoverInteraction(GameObject root)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Transform interactions = root.transform.Find("Interactions");
            if (interactions == null)
            {
                Melon<MiningMod>.Logger.Warning($"Door {root.name}: no Interactions object - F zone unchanged");
                return;
            }
            Bounds b = bounds.GetBounds();
            float frame = BigDoorModel.SideFrame(b), depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f);
            Vector3 size = new Vector3(bounds.bounds.x - frame, bounds.bounds.y, depth + 0.01f);
            Vector3 centre = bounds.center + new Vector3(frame / 2f, 0f, 0f);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                Transform t = box.transform;
                Vector3 lossy = t.lossyScale, rootScale = root.transform.lossyScale;
                box.center = t.InverseTransformPoint(root.transform.TransformPoint(centre));
                box.size = new Vector3(size.x * rootScale.x / lossy.x, size.y * rootScale.y / lossy.y, size.z * rootScale.z / lossy.z);
            }
        }

        private static void RebuildColliders(GameObject root)
        {
            DoorDynamicColliders dynamic = root.GetComponentInChildren<DoorDynamicColliders>(true);
            if (dynamic == null || dynamic.gameObject == root)
            {
                Melon<MiningMod>.Logger.Warning($"Door {root.name}: no separate dynamic colliders - collision unchanged");
                return;
            }
            Transform dt = dynamic.transform;
            Melon<MiningMod>.Logger.Msg($"Door {root.name}: dynamic colliders at {root.transform.InverseTransformPoint(dt.position)} rot={(Quaternion.Inverse(root.transform.rotation) * dt.rotation).eulerAngles} scale={dt.lossyScale}");
            dt.SetPositionAndRotation(root.transform.position, root.transform.rotation);
            dt.localScale = Vector3.Scale(dt.localScale, new Vector3(root.transform.lossyScale.x / dt.lossyScale.x, root.transform.lossyScale.y / dt.lossyScale.y, root.transform.lossyScale.z / dt.lossyScale.z));
            var fields = Traverse.Create(dynamic);
            GameObject oldDoor = fields.Field<GameObject>("doorCollider").Value;
            Collider sample = dynamic.GetComponentsInChildren<Collider>(true).FirstOrDefault();
            int layer = sample != null ? sample.gameObject.layer : dynamic.gameObject.layer;
            string tag = sample != null ? sample.tag : dynamic.tag;
            foreach (Collider c in dynamic.GetComponents<Collider>())
            {
                Object.DestroyImmediate(c);
            }
            foreach (Transform child in dynamic.transform.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }

            Bounds b = root.GetComponent<ShipPartBounds>().GetBounds();
            float frame = BigDoorModel.SideFrame(b), top = BigDoorModel.TopFrame(b), depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f), z = b.center.z;
            GameObject frameObject = ColliderObject("Frame", dynamic.transform, layer, tag);
            AddBox(root, frameObject, new Vector3(b.min.x + frame / 2, b.center.y, z), new Vector3(frame, b.size.y, depth));
            AddBox(root, frameObject, new Vector3(b.max.x - frame / 2, b.center.y, z), new Vector3(frame, b.size.y, depth));
            AddBox(root, frameObject, new Vector3(b.center.x, b.max.y - top / 2, z), new Vector3(b.size.x, top, depth));
            GameObject leaf = ColliderObject("Door", dynamic.transform, layer, tag);
            AddBox(root, leaf, new Vector3(b.center.x, (b.min.y + b.max.y - top) / 2, z), new Vector3(b.size.x - 2 * frame, b.size.y - top, depth));
            leaf.SetActive(oldDoor == null || oldDoor.activeSelf);
            fields.Field("doorCollider").SetValue(leaf);
            fields.Field("colliders").SetValue(new[] { frameObject, leaf });
        }

        private static GameObject ColliderObject(string name, Transform parent, int layer, string tag)
        {
            var go = new GameObject(name) { layer = layer, tag = tag };
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void AddBox(GameObject root, GameObject owner, Vector3 center, Vector3 size)
        {
            var box = owner.AddComponent<BoxCollider>();
            Transform t = owner.transform;
            box.center = t.InverseTransformPoint(root.transform.TransformPoint(center));
            Vector3 local = t.InverseTransformVector(root.transform.TransformVector(size));
            box.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        }

        private static void Scale(GameObject root, float sx, float sy)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c0 = bounds.center, b0 = bounds.bounds;
            var s = new Vector3(sx, sy, 1f);
            Vector3 min0 = c0 - b0 * 0.5f;
            Vector3 b1 = Vector3.Scale(b0, s);
            Vector3 c1 = min0 + b1 * 0.5f;
            bounds.center = c1;
            bounds.bounds = b1;

            DoorDynamicColliders dynamic = root.GetComponentInChildren<DoorDynamicColliders>(true);
            foreach (Transform unit in root.transform.Cast<Transform>().ToArray())
            {
                if (dynamic != null && dynamic.transform.IsChildOf(unit))
                {
                    continue;
                }
                Vector3 offset = c1 - Vector3.Scale(s, c0) + Vector3.Scale(s - Vector3.one, unit.localPosition);
                var scaled = new GameObject("Scaled").transform;
                scaled.SetParent(unit, false);
                foreach (Transform child in unit.Cast<Transform>().Where(t => t != scaled).ToArray())
                {
                    child.SetParent(scaled, false);
                }
                scaled.localPosition = offset;
                scaled.localScale = s;
                foreach (BoxCollider box in unit.GetComponents<BoxCollider>())
                {
                    box.center = offset + Vector3.Scale(s, box.center);
                    box.size = Vector3.Scale(s, box.size);
                }
                Melon<MiningMod>.Logger.Msg($"Door {root.name}: {unit.name} pos={unit.localPosition} rot={unit.localEulerAngles} scale={unit.localScale}");
            }
            Melon<MiningMod>.Logger.Msg($"Door {root.name}: bounds {c0}/{b0} -> {c1}/{b1}");
        }
    }
}
