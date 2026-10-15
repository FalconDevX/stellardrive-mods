using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Thruster;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class HydrogenEngines
    {
        internal const ushort ResearchId = 212;
        internal const string Steel = "steel", Dark = "dark", White = "white", Orange = "orange", Black = "black", Light = "light", Cyan = "cyan";

        internal sealed class Variant
        {
            public ushort Id;
            public string Label;
            public int Width, Height;
            public float Scale, Mass;
            public int Iron, Copper, Glass;
            public Func<MeshBuilder, List<Nozzle>> Model;
        }

        internal struct Nozzle
        {
            public Vector3 Exit;
            public float Radius;
        }

        internal static readonly Variant[] Variants =
        {
            new Variant { Id = 228, Label = "Hydrogen Thruster M", Width = 2, Height = 3, Scale = 4f, Mass = 160f, Iron = 60, Copper = 25, Glass = 10, Model = Medium },
            new Variant { Id = 229, Label = "Hydrogen Thruster L", Width = 3, Height = 4, Scale = 9f, Mass = 420f, Iron = 140, Copper = 60, Glass = 20, Model = Large },
            new Variant { Id = 230, Label = "Hydrogen Thruster XL", Width = 4, Height = 6, Scale = 20f, Mass = 950f, Iron = 300, Copper = 120, Glass = 40, Model = Huge },
        };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static PartSettings[] All => Variants.Select(v => Get(v.Id)).Where(p => p != null).ToArray();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static Variant Find(ushort id) => Variants.FirstOrDefault(v => v.Id == id);

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            Variant variant = Find(id);
            List<PartSettings> list = gameParts.ToList();
            var chemical = PartFactory.Find(list, HydrogenThruster.ChemicalThrusterId) as ThrusterSettings;
            PartSettings generator = PartFactory.Find(list, HydrogenThruster.GeneratorPartId);
            if (variant == null || chemical?.part == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                PartSettings part = Build(variant, chemical, generator);
                Parts[id] = part;
                Melon<MiningMod>.Logger.Msg($"Added part '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build {variant.Label}: {e}");
                return null;
            }
        }

        private static ThrusterSettings Build(Variant v, ThrusterSettings chemical, PartSettings generator)
        {
            GameObject root = Object.Instantiate(chemical.part, PartFactory.Holder);
            root.name = "HydrogenThruster" + v.Width;
            ThrusterVisual visual = root.GetComponentInChildren<ThrusterVisual>(true);
            ThrusterFlameVFX flame = root.GetComponentInChildren<ThrusterFlameVFX>(true);
            ThrusterSFX sound = root.GetComponentInChildren<ThrusterSFX>(true);
            MeshRenderer look = null;
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                if ((flame != null && r.transform.IsChildOf(flame.transform)) || (sound != null && r.transform.IsChildOf(sound.transform)))
                {
                    continue;
                }
                if (look == null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Albedo"))
                {
                    look = r;
                }
                r.enabled = false;
            }
            if (look == null)
            {
                throw new InvalidOperationException("No chemical thruster material");
            }

            var builder = new MeshBuilder();
            List<Nozzle> nozzles = v.Model(builder);
            Mesh mesh = builder.Build("HydrogenThruster" + v.Width);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "HydrogenThruster" + v.Width);
            PartFactory.CreateRenderer("Body", visual.transform, mesh, material, look);
            PartFactory.SetBounds(root, mesh.bounds);

            Nozzle main = nozzles.OrderByDescending(n => n.Radius).First();
            if (flame != null)
            {
                flame.transform.position = root.transform.TransformPoint(main.Exit);
                HydrogenThruster.TintFlame(flame.gameObject);
                float scale = main.Radius / BaseNozzleRadius;
                var fields = Traverse.Create(flame);
                foreach (string field in new[] { "highSpeed", "lowSpeed", "highSize", "lowSize" })
                {
                    fields.Field(field).SetValue(fields.Field<float>(field).Value * (field.EndsWith("Size") ? scale : Mathf.Sqrt(scale)));
                }
                foreach (Nozzle extra in nozzles.Where(n => !n.Equals(main)))
                {
                    GameObject copy = Object.Instantiate(flame.gameObject, flame.transform.parent);
                    Object.DestroyImmediate(copy.GetComponent<ThrusterFlameVFX>());
                    copy.transform.position = root.transform.TransformPoint(extra.Exit);
                    copy.AddComponent<FlameMirror>().Setup(flame, extra.Radius / main.Radius);
                }
            }
            if (sound != null)
            {
                sound.transform.position = root.transform.TransformPoint(main.Exit);
            }

            Transform interactions = root.transform.Find("Interactions");
            BoxCollider box = interactions != null ? interactions.GetComponentInChildren<BoxCollider>(true) : null;
            if (box != null)
            {
                Vector3 center = root.transform.TransformPoint(mesh.bounds.center);
                box.center = box.transform.InverseTransformPoint(center);
                box.size = mesh.bounds.size;
            }

            foreach (SocketObject extra in root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Fluid && s.Id > 1).ToArray())
            {
                Object.DestroyImmediate(extra.gameObject);
            }
            SocketObject[] fluids = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Fluid).OrderBy(s => s.Id).ToArray();
            float half = v.Width * 0.5f;
            for (int i = 0; i < fluids.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Traverse.Create(fluids[i]).Field("relayedSockets").SetValue(new SocketObject[0]);
                fluids[i].transform.localPosition = new Vector3(side * (half - 0.05f), 0.32f, 0f);
                fluids[i].transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : 270f, 0f);
            }
            PartFactory.CloneSocket(PartFactory.FindSocket(generator, CableType.Power), root.transform, HydrogenThruster.PowerSocket, new Vector3(0f, 0.32f, half - 0.05f), 180f);

            var settings = ScriptableObject.CreateInstance<ThrusterSettings>();
            settings.name = "MiningHydrogenThruster" + v.Width;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.Id;
            settings.fullLabel = v.Label;
            settings.internalStateClass = chemical.internalStateClass;
            settings.snappingStyle = chemical.snappingStyle;
            settings.description = $"{v.Width} x {v.Width} x {v.Height} hydrogen thruster: {v.Scale:0}x the thrust of the small one, and {v.Scale:0}x its hydrogen, oxygen and power " +
                                   $"({HydrogenThruster.PowerWatts * v.Scale:0} W). Hydrogen and oxygen pipes go into the side sockets, power into the front one; it waits until all three are there.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = chemical.thumbnailRotation;
            settings.mass = v.Mass;
            settings.size = new Vector3(v.Width, v.Height, v.Width);
            settings.thrustStrength = chemical.thrustStrength;
            settings.maxFuel = chemical.maxFuel;
            settings.fuelDurationPerUnit = chemical.fuelDurationPerUnit;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.Iron), (112, v.Copper), (Items.Model.ItemsConstants.GlassItemId, v.Glass));
            return settings;
        }

        private const float BaseNozzleRadius = 0.28f;

        private static void MountPlate(MeshBuilder b, float width, int bolts)
        {
            float w = width - 0.06f;
            b.Box(new Vector3(0f, 0.07f, 0f), new Vector3(w, 0.14f, w), Dark);
            b.Box(new Vector3(0f, 0.15f, 0f), new Vector3(w - 0.2f, 0.03f, w - 0.2f), Light);
            float edge = w * 0.5f - 0.1f;
            for (int i = 0; i < bolts; i++)
            {
                float t = bolts == 1 ? 0f : -edge + 2f * edge * i / (bolts - 1);
                foreach (var (x, z) in new[] { (t, -edge), (t, edge), (-edge, t), (edge, t) })
                {
                    b.Cylinder(new Vector3(x, 0.14f, z), new Vector3(x, 0.18f, z), 0.035f, Steel, 8);
                }
            }
        }

        private static void Struts(MeshBuilder b, float width, float top, float reach)
        {
            float edge = width * 0.5f - 0.2f;
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                b.Cylinder(new Vector3(x * edge, 0.16f, z * edge), new Vector3(x * reach, top, z * reach), 0.05f, Steel, 10);
                b.Sphere(new Vector3(x * edge, 0.17f, z * edge), 0.08f, Dark, 10);
            }
        }

        private static Nozzle Engine(MeshBuilder b, Vector3 basePos, float chamberRadius, float chamberLength, float throatRadius, float exitRadius, float exitY, int segments)
        {
            float y0 = basePos.y;
            float yChamber = y0 + chamberLength;
            float yThroat = yChamber + chamberRadius * 0.45f;
            var chamber = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(chamberRadius * 0.75f, 0f),
                new Vector2(chamberRadius, chamberRadius * 0.25f),
                new Vector2(chamberRadius, chamberLength - chamberRadius * 0.15f),
                new Vector2(chamberRadius * 0.9f, chamberLength),
            };
            b.Lathe(new Vector3(basePos.x, y0, basePos.z), Vector3.up, chamber, White, segments);
            foreach (float t in new[] { 0.32f, 0.62f })
            {
                float y = chamberLength * t;
                b.Lathe(new Vector3(basePos.x, y0, basePos.z), Vector3.up, new List<Vector2>
                {
                    new Vector2(chamberRadius + 0.004f, y), new Vector2(chamberRadius + 0.02f, y + 0.02f),
                    new Vector2(chamberRadius + 0.02f, y + chamberLength * 0.08f), new Vector2(chamberRadius + 0.004f, y + chamberLength * 0.08f + 0.02f),
                }, t < 0.5f ? Orange : Dark, segments);
            }
            b.Lathe(new Vector3(basePos.x, 0f, basePos.z), Vector3.up, new List<Vector2>
            {
                new Vector2(chamberRadius * 0.9f, yChamber), new Vector2(throatRadius * 1.2f, yChamber + (yThroat - yChamber) * 0.6f), new Vector2(throatRadius, yThroat),
            }, Steel, segments);

            const int steps = 14;
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            float thickness = Mathf.Max(0.02f, exitRadius * 0.04f);
            for (int k = 0; k <= steps; k++)
            {
                float t = (float)k / steps;
                float r = throatRadius + (exitRadius - throatRadius) * (1f - Mathf.Pow(1f - t, 2.2f));
                float y = Mathf.Lerp(yThroat, exitY, t);
                outer.Add(new Vector2(r + thickness, y));
                inner.Add(new Vector2(r, y));
            }
            outer.Add(new Vector2(exitRadius + thickness * 1.8f, exitY));
            outer.Add(new Vector2(exitRadius, exitY + 0.005f));
            b.Lathe(new Vector3(basePos.x, 0f, basePos.z), Vector3.up, outer, Dark, segments);
            b.Lathe(new Vector3(basePos.x, 0f, basePos.z), Vector3.up, inner, Black, segments, true);
            int ribs = Mathf.Clamp(Mathf.RoundToInt((exitY - yThroat) / 0.35f), 2, 9);
            for (int k = 1; k <= ribs; k++)
            {
                float t = (float)k / (ribs + 1);
                float r = throatRadius + (exitRadius - throatRadius) * (1f - Mathf.Pow(1f - t, 2.2f)) + thickness;
                float y = Mathf.Lerp(yThroat, exitY, t);
                float h = Mathf.Max(0.015f, exitRadius * 0.035f);
                b.Lathe(new Vector3(basePos.x, 0f, basePos.z), Vector3.up, new List<Vector2>
                {
                    new Vector2(r, y - h), new Vector2(r + h * 0.8f, y - h * 0.4f), new Vector2(r + h * 0.8f, y + h * 0.4f), new Vector2(r, y + h),
                }, k == ribs ? Orange : Steel, segments);
            }
            return new Nozzle { Exit = new Vector3(basePos.x, exitY, basePos.z), Radius = exitRadius };
        }

        private static void Pump(MeshBuilder b, Vector3 bottom, float radius, float height, Vector3 target)
        {
            b.Lathe(bottom, Vector3.up, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(radius * 0.8f, 0f), new Vector2(radius, radius * 0.25f),
                new Vector2(radius, height - radius * 0.25f), new Vector2(radius * 0.8f, height), new Vector2(0f, height),
            }, Dark, 16);
            b.Cylinder(bottom + Vector3.up * height * 0.45f, bottom + Vector3.up * height * 0.6f, radius * 1.08f, Orange, 16);
            b.Cylinder(bottom + Vector3.up * height, bottom + Vector3.up * (height + radius * 0.35f), radius * 0.45f, Steel, 12);
            Vector3 outlet = bottom + Vector3.up * height * 0.8f;
            b.Pipe(new[] { outlet, new Vector3(outlet.x, target.y, outlet.z), target }, radius * 0.28f, Steel, 10);
        }

        private static void FeedPipes(MeshBuilder b, float width, Vector3 hydrogenTarget, Vector3 oxygenTarget)
        {
            float half = width * 0.5f - 0.12f;
            b.Pipe(new[] { new Vector3(-half, 0.32f, 0f), new Vector3(-half + 0.25f, 0.32f, 0f), new Vector3(-half + 0.25f, hydrogenTarget.y, 0f), hydrogenTarget }, 0.05f, Orange, 10);
            b.Pipe(new[] { new Vector3(half, 0.32f, 0f), new Vector3(half - 0.25f, 0.32f, 0f), new Vector3(half - 0.25f, oxygenTarget.y, 0f), oxygenTarget }, 0.05f, Cyan, 10);
            b.Box(new Vector3(0f, 0.32f, width * 0.5f - 0.2f), new Vector3(0.26f, 0.22f, 0.22f), Dark);
            b.Box(new Vector3(0f, 0.45f, width * 0.5f - 0.2f), new Vector3(0.27f, 0.03f, 0.23f), Orange);
        }

        private static List<Nozzle> Medium(MeshBuilder b)
        {
            MountPlate(b, 2f, 4);
            Struts(b, 2f, 0.95f, 0.36f);
            Nozzle main = Engine(b, new Vector3(0f, 0.22f, 0f), 0.42f, 0.8f, 0.2f, 0.82f, 2.94f, 32);
            Pump(b, new Vector3(0.62f, 0.2f, -0.55f), 0.15f, 0.55f, new Vector3(0.3f, 0.95f, -0.3f));
            FeedPipes(b, 2f, new Vector3(-0.38f, 0.75f, 0f), new Vector3(0.38f, 0.65f, 0f));
            return new List<Nozzle> { main };
        }

        private static List<Nozzle> Large(MeshBuilder b)
        {
            MountPlate(b, 3f, 5);
            Struts(b, 3f, 1.2f, 0.55f);
            b.Lathe(new Vector3(0f, 0.18f, 0f), Vector3.up, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.55f, 0.1f), new Vector2(0.55f, 1.4f), new Vector2(0.42f, 1.75f),
                new Vector2(0.3f, 2.3f), new Vector2(0.12f, 2.55f), new Vector2(0f, 2.6f),
            }, White, 32);
            b.Cylinder(new Vector3(0f, 0.7f, 0f), new Vector3(0f, 0.9f, 0f), 0.575f, Orange, 32);
            b.Cylinder(new Vector3(0f, 1.25f, 0f), new Vector3(0f, 1.35f, 0f), 0.575f, Dark, 32);
            var nozzles = new List<Nozzle>();
            foreach (var (x, z) in new[] { (-0.78f, -0.78f), (0.78f, -0.78f), (-0.78f, 0.78f), (0.78f, 0.78f) })
            {
                nozzles.Add(Engine(b, new Vector3(x, 0.24f, z), 0.28f, 0.75f, 0.14f, 0.6f, 3.92f, 24));
                b.Pipe(new[] { new Vector3(x * 0.55f, 0.9f, z * 0.55f), new Vector3(x * 0.8f, 0.9f, z * 0.8f) }, 0.06f, Steel, 10);
            }
            Pump(b, new Vector3(-1.1f, 0.2f, 0f), 0.17f, 0.7f, new Vector3(-0.56f, 1.1f, 0f));
            Pump(b, new Vector3(1.1f, 0.2f, 0f), 0.17f, 0.7f, new Vector3(0.56f, 1.1f, 0f));
            FeedPipes(b, 3f, new Vector3(-0.56f, 0.6f, 0.2f), new Vector3(0.56f, 0.6f, 0.2f));
            return nozzles;
        }

        private static List<Nozzle> Huge(MeshBuilder b)
        {
            MountPlate(b, 4f, 6);
            Struts(b, 4f, 1.7f, 0.7f);
            Nozzle main = Engine(b, new Vector3(0f, 0.24f, 0f), 0.78f, 1.5f, 0.38f, 1.24f, 5.92f, 40);
            foreach (float angle in new[] { 45f, 135f, 225f, 315f })
            {
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                b.Box(rot * new Vector3(0f, 0.95f, 0.98f), new Vector3(0.05f, 1.25f, 0.42f), Steel, rot);
                b.Box(rot * new Vector3(0f, 0.95f, 1.2f), new Vector3(0.08f, 1.3f, 0.04f), Orange, rot);
            }
            foreach (var (x, color) in new[] { (-1.38f, Orange), (1.38f, Cyan) })
            {
                b.Sphere(new Vector3(x, 0.85f, 0f), 0.55f, White, 24);
                b.Cylinder(new Vector3(x, 0.82f, 0f), new Vector3(x, 0.92f, 0f), 0.56f, color, 24);
                b.Cylinder(new Vector3(x, 0.16f, 0f), new Vector3(x, 0.32f, 0f), 0.28f, Dark, 16);
            }
            var nozzles = new List<Nozzle> { main };
            for (int k = 0; k < 6; k++)
            {
                float a = (30f + 60f * k) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * 1.46f, 1.6f, Mathf.Sin(a) * 1.46f);
                nozzles.Add(Engine(b, pos, 0.22f, 0.55f, 0.11f, 0.42f, 3.6f, 20));
                b.Pipe(new[] { new Vector3(pos.x * 0.6f, 1.55f, pos.z * 0.6f), new Vector3(pos.x, 1.55f, pos.z) }, 0.05f, Steel, 10);
            }
            Pump(b, new Vector3(0f, 0.2f, -1.35f), 0.2f, 0.9f, new Vector3(0f, 1.3f, -0.8f));
            Pump(b, new Vector3(0f, 0.2f, 1.1f), 0.18f, 0.6f, new Vector3(0f, 1.0f, 0.8f));
            FeedPipes(b, 4f, new Vector3(-1.38f, 1.2f, 0f), new Vector3(1.38f, 1.2f, 0f));
            b.Pipe(new[] { new Vector3(-1.05f, 0.85f, 0f), new Vector3(-0.8f, 0.85f, 0f) }, 0.08f, Orange, 12);
            b.Pipe(new[] { new Vector3(1.05f, 0.85f, 0f), new Vector3(0.8f, 0.85f, 0f) }, 0.08f, Cyan, 12);
            return nozzles;
        }
    }

    internal sealed class FlameMirror : MonoBehaviour
    {
        private const string Prefix = "FlameMirror|";
        private ParticleSystem _master, _own;
        private float _scale = 1f;

        internal void Setup(ThrusterFlameVFX master, float scale)
        {
            name = Prefix + scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool Resolve()
        {
            if (_master != null && _own != null)
            {
                return true;
            }
            ThrusterFlameVFX master = transform.parent != null ? transform.parent.GetComponentInChildren<ThrusterFlameVFX>(true) : null;
            _master = master != null ? master.GetComponentInChildren<ParticleSystem>(true) : null;
            _own = GetComponentInChildren<ParticleSystem>(true);
            if (name.StartsWith(Prefix) && float.TryParse(name.Substring(Prefix.Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale))
            {
                _scale = scale;
            }
            return _master != null && _own != null;
        }

        private void LateUpdate()
        {
            if (!Resolve())
            {
                return;
            }
            ParticleSystem.EmissionModule source = _master.emission, target = _own.emission;
            target.rateOverTimeMultiplier = source.rateOverTimeMultiplier;
            ParticleSystem.MainModule from = _master.main, to = _own.main;
            to.startSpeedMultiplier = from.startSpeedMultiplier * Mathf.Sqrt(_scale);
            to.startSizeMultiplier = from.startSizeMultiplier * _scale;
            if (_master.isPlaying && !_own.isPlaying) _own.Play(true);
            else if (!_master.isPlaying && _own.isPlaying) _own.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
