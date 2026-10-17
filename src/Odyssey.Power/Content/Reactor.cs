using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class Reactor
    {
        internal const ushort PartId = 203;
        internal const ushort LargePartId = 231;
        internal const ushort LargeResearchId = 213;
        internal const float LargeScale = 5f;
        internal const byte PowerSocket = 0, WaterSocket = 2, SignalSocket = 3;
        internal const float LargeModelScale = 2f;
        internal static bool HasSignalSocket;
        internal static readonly Vector3 RotorPosition = new Vector3(0f, 1.44f, 0f);
        internal static readonly Vector3[] BeaconPositions =
        {
            new Vector3(0.86f, 1.612f, 0.86f), new Vector3(-0.86f, 1.612f, 0.86f), new Vector3(-0.86f, 1.612f, -0.86f), new Vector3(0.86f, 1.612f, -0.86f),
        };
        private static readonly Vector2[] SocketLayout = { new Vector2(-0.70f, 0.45f), new Vector2(-0.25f, 0.45f), new Vector2(0.25f, 0.45f), new Vector2(0.70f, 0.45f) };
        private const ushort ChestPartId = 30, GeneratorPartId = 60, DistillerPartId = 80;
        private static PartSettings _part, _large;
        private static bool _failed, _largeFailed;

        internal static PartSettings Part => _part;
        internal static PartSettings Large => _large;

        internal static bool Is(PartSettings settings) => settings != null && (settings.id == PartId || settings.id == LargePartId);

        internal static float Scale(PartSettings settings) => settings != null && settings.id == LargePartId ? LargeScale : 1f;

        internal static PartSettings GetOrCreateLarge(IEnumerable<PartSettings> gameParts)
        {
            if (_large != null || _largeFailed)
            {
                return _large;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId), distiller = PartFactory.Find(parts, DistillerPartId);
            if (chest?.part == null || generator?.part == null || distiller?.part == null)
            {
                return null;
            }
            try
            {
                _large = BuildLarge(chest, generator, distiller);
                Melon<PowerMod>.Logger.Msg($"Added part '{_large.fullLabel}' (id {LargePartId})");
            }
            catch (Exception e)
            {
                _largeFailed = true;
                Melon<PowerMod>.Logger.Error($"Could not build the large reactor: {e}");
            }
            return _large;
        }

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId), distiller = PartFactory.Find(parts, DistillerPartId);
            if (chest?.part == null || generator?.part == null || distiller?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chest, generator, distiller);
                Melon<PowerMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<PowerMod>.Logger.Error($"Could not build the reactor: {e}");
            }
            return _part;
        }

        private const float ShellGap = 0.01f, ShellDepth = 0.12f, SocketHole = 0.24f;

        private static void BuildInteractionShell(BoxCollider first, Bounds b, Vector2[] minusX, Vector2[] plusX, Vector2[] plusZ)
        {
            float x0 = b.min.x - ShellGap, x1 = b.max.x + ShellGap, z0 = b.min.z - ShellGap, z1 = b.max.z + ShellGap;
            float y0 = b.min.y, y1 = b.max.y + ShellGap;
            float ox0 = x0 - ShellDepth, ox1 = x1 + ShellDepth, oz0 = z0 - ShellDepth, oz1 = z1 + ShellDepth;
            var boxes = new List<(Vector3 min, Vector3 max)>
            {
                (new Vector3(ox0, y0, oz0), new Vector3(ox1, y1, z0)),
                (new Vector3(ox0, y1, oz0), new Vector3(ox1, y1 + ShellDepth, oz1)),
            };
            foreach (var (a0, a1, c0, c1) in Holes(z0, z1, y0, y1, minusX))
            {
                boxes.Add((new Vector3(ox0, c0, a0), new Vector3(x0, c1, a1)));
            }
            foreach (var (a0, a1, c0, c1) in Holes(z0, z1, y0, y1, plusX))
            {
                boxes.Add((new Vector3(x1, c0, a0), new Vector3(ox1, c1, a1)));
            }
            foreach (var (a0, a1, c0, c1) in Holes(ox0, ox1, y0, y1, plusZ))
            {
                boxes.Add((new Vector3(a0, c0, z1), new Vector3(a1, c1, oz1)));
            }
            for (int i = 0; i < boxes.Count; i++)
            {
                BoxCollider collider = i == 0 ? first : first.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = first.isTrigger;
                collider.center = (boxes[i].min + boxes[i].max) * 0.5f;
                collider.size = boxes[i].max - boxes[i].min;
            }
        }

        private static List<(float a0, float a1, float c0, float c1)> Holes(float a0, float a1, float c0, float c1, Vector2[] sockets)
        {
            var rects = new List<(float a0, float a1, float c0, float c1)> { (a0, a1, c0, c1) };
            foreach (Vector2 socket in sockets)
            {
                float h0 = socket.x - SocketHole, h1 = socket.x + SocketHole, v0 = socket.y - SocketHole, v1 = socket.y + SocketHole;
                var next = new List<(float a0, float a1, float c0, float c1)>();
                foreach (var r in rects)
                {
                    if (h1 <= r.a0 || h0 >= r.a1 || v1 <= r.c0 || v0 >= r.c1)
                    {
                        next.Add(r);
                        continue;
                    }
                    float ih0 = Mathf.Max(h0, r.a0), ih1 = Mathf.Min(h1, r.a1), iv0 = Mathf.Max(v0, r.c0), iv1 = Mathf.Min(v1, r.c1);
                    if (ih0 > r.a0) next.Add((r.a0, ih0, r.c0, r.c1));
                    if (ih1 < r.a1) next.Add((ih1, r.a1, r.c0, r.c1));
                    if (iv0 > r.c0) next.Add((ih0, ih1, r.c0, iv0));
                    if (iv1 < r.c1) next.Add((ih0, ih1, iv1, r.c1));
                }
                rects = next;
            }
            return rects;
        }

        private static GameObject BuildModel(PartSettings chest, PartSettings generator, PartSettings distiller, string name, float size, float detection)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = name;
            Mesh body = ModAssets.LoadMesh("reactor_body.bin");
            Mesh rotor = ModAssets.LoadMesh("reactor_rotor.bin");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("reactor_palette.png"), name);
            Material glowMaterial = new Material(material) { name = name + "Glow" };
            glowMaterial.SetColor("_EmissionColor", Color.black);
            glowMaterial.EnableKeyword("_EMISSION");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(visual.GetComponent<DefaultShipPartVisuals>());
            visual.gameObject.AddComponent<ReactorVisuals>();
            PartFactory.CreateRenderer("Body", visual, body, material, lookCopy).transform.localScale = Vector3.one * size;
            PartFactory.CreateRenderer("Glow", visual, ModAssets.LoadMesh("reactor_glow.bin"), glowMaterial, lookCopy).transform.localScale = Vector3.one * size;
            Transform spinner = PartFactory.CreateRenderer("Rotor", visual, rotor, material, lookCopy).transform;
            spinner.localPosition = RotorPosition * size;
            spinner.localScale = Vector3.one * size;
            Material beaconMaterial = new Material(material) { name = name + "Beacon" };
            beaconMaterial.SetTexture("_EmissionMap", ModAssets.LoadTexture("reactor_palette.png"));
            beaconMaterial.SetColor("_EmissionColor", Color.black);
            beaconMaterial.EnableKeyword("_EMISSION");
            Mesh beacon = ModAssets.LoadMesh("reactor_beacon.bin");
            for (int i = 0; i < BeaconPositions.Length; i++)
            {
                Transform lamp = PartFactory.CreateRenderer("Beacon" + i, visual, beacon, beaconMaterial, lookCopy).transform;
                lamp.localPosition = BeaconPositions[i] * size;
                lamp.localRotation = Quaternion.Euler(0f, 90f * i, 0f);
                lamp.localScale = Vector3.one * size * 1.5f;
            }
            Object.DestroyImmediate(lookCopy.gameObject);

            var bounds = new Bounds(body.bounds.center * size, body.bounds.size * size);
            PartFactory.SetBounds(root, bounds);

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            var interaction = box.GetComponent<PartInteraction>();
            if (interaction != null)
            {
                HarmonyLib.Traverse.Create(interaction).Field("detectionDistance").SetValue(detection);
            }
            Vector2[] sockets = SocketLayout.Select(p => p * size).ToArray();
            BuildInteractionShell(box, bounds, new Vector2[0], new Vector2[0], sockets);

            float face = bounds.max.z + 0.02f;
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject p0 = PartFactory.CloneSocket(power, root.transform, 0, new Vector3(sockets[0].x, sockets[0].y, face), 180f);
            SocketObject p1 = PartFactory.CloneSocket(power, root.transform, 1, new Vector3(sockets[1].x, sockets[1].y, face), 180f);
            HarmonyLib.Traverse.Create(p0).Field("relayedSockets").SetValue(new[] { p1 });
            HarmonyLib.Traverse.Create(p1).Field("relayedSockets").SetValue(new[] { p0 });
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, new Vector3(sockets[2].x, sockets[2].y, face), 180f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            HasSignalSocket = signalSource != null;
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(sockets[3].x, sockets[3].y, face), 180f);
            }
            else
            {
                Melon<PowerMod>.Logger.Warning("No signal socket found to copy - reactor without a signal input (runs without it)");
            }
            return root;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = BuildModel(chest, generator, distiller, "NuclearReactor", 1f, 3f);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningNuclearReactor";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Nuclear Reactor";
            settings.internalStateClass = generator.internalStateClass;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"Makes {PowerMod.ReactorPower.Value:0} W of power from uranium (put it in the slot) while cooled by water from a pipe " +
                                   "and switched on by signal 1 (e.g. from a lever). All four sockets are on the service panel on one side: two power sockets (yellow), water (blue) and signal (white). Without water or signal it stops.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 400f;
            settings.size = new Vector3(2f, 2f, 2f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 40), (112, 20), (Items.Model.ItemsConstants.GlassItemId, 10));
            return settings;
        }

        private static PartSettings BuildLarge(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = BuildModel(chest, generator, distiller, "LargeNuclearReactor", LargeModelScale, 4f);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningLargeNuclearReactor";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = LargePartId;
            settings.fullLabel = "Large Nuclear Reactor";
            settings.internalStateClass = generator.internalStateClass;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"4 x 4 x 4 reactor: {PowerMod.ReactorPower.Value * LargeScale:0} W from uranium, {LargeScale:0}x the uranium and cooling water of the small one. " +
                                   "All four sockets are on the service panel on one side: two power sockets (yellow), water (blue) and signal 1 (white, e.g. from a lever). Without water or signal it stops.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 2000f;
            settings.size = new Vector3(4f, 4f, 4f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 200), (112, 100), (Items.Model.ItemsConstants.GlassItemId, 50));
            return settings;
        }
    }
}
