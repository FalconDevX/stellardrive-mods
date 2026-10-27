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

namespace Odyssey.Content
{
    internal static class HydrogenEngines
    {
        internal const ushort ResearchId = 212;
        internal const string Steel = "steel", Dark = "dark", White = "white", Orange = "orange", Black = "black", Light = "light", Cyan = "cyan", Grey = "grey", Red = "red";

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
                Melon<PowerMod>.Logger.Msg($"Added part '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<PowerMod>.Logger.Error($"Could not build {variant.Label}: {e}");
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
                if (v.Scale >= 20f)
                {
                    foreach (ParticleSystem ps in flame.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ParticleSystem.MainModule mainModule = ps.main;
                        mainModule.startLifetimeMultiplier *= 1.8f;
                    }
                }
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
                sound.gameObject.AddComponent<HydrogenThrusterSound>();
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

        private static Vector3 Ring(float angle, float radius, float y) =>
            new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, y, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

        private static void BoltCircle(MeshBuilder b, Vector3 center, float radius, int count, float boltRadius, float height)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + Ring(180f / count + 360f * i / count, radius, 0f);
                b.Cylinder(p - Vector3.up * 0.003f, p + Vector3.up * height, boltRadius, Dark, 6);
            }
        }

        private static float FlangeThickness(float pipeRadius) => Mathf.Max(0.012f, pipeRadius * 0.35f);

        private static void Flange(MeshBuilder b, Vector3 at, Vector3 dir, float pipeRadius)
        {
            float r = pipeRadius * 1.9f, t = FlangeThickness(pipeRadius), br = Mathf.Max(0.006f, pipeRadius * 0.18f);
            b.Cylinder(at - dir * (t * 0.5f), at + dir * (t * 0.5f), r, Steel, 16);
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized, side2 = Vector3.Cross(dir, side);
            for (int i = 0; i < 6; i++)
            {
                float a = (30f + 60f * i) * Mathf.Deg2Rad;
                Vector3 p = at + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * r * 0.74f;
                b.Cylinder(p - dir * (t * 0.5f + br), p + dir * (t * 0.5f + br), br, Dark, 6);
            }
        }

        private static void Port(MeshBuilder b, Vector3 at, Vector3 dir, float pipeRadius)
        {
            b.Cylinder(at - dir * 0.012f, at + dir * (pipeRadius + 0.03f), pipeRadius * 1.5f, Dark, 16);
            Flange(b, at - dir * (0.012f + FlangeThickness(pipeRadius) * 0.5f), dir, pipeRadius);
        }

        private static void Valve(MeshBuilder b, Vector3 at, Vector3 dir, float pipeRadius, Vector3 up)
        {
            b.Cylinder(at - dir * pipeRadius * 1.5f, at + dir * pipeRadius * 1.5f, pipeRadius * 1.35f, Dark, 14);
            b.Box(at + up * pipeRadius * 0.6f, new Vector3(pipeRadius * 2f, pipeRadius * 2.2f, pipeRadius * 2f), Dark, Quaternion.LookRotation(dir, up));
            b.Cylinder(at + up * pipeRadius * 1.5f, at + up * pipeRadius * 2.5f, pipeRadius * 0.45f, Steel, 8);
            b.Cylinder(at + up * pipeRadius * 2.45f, at + up * pipeRadius * 2.7f, pipeRadius * 1.4f, Red, 14);
        }

        private static void MountPlate(MeshBuilder b, float width, int bolts)
        {
            float w = width - 0.06f;
            b.Box(new Vector3(0f, 0.07f, 0f), new Vector3(w, 0.14f, w), Dark);
            b.Box(new Vector3(0f, 0.15f, 0f), new Vector3(w - 0.2f, 0.03f, w - 0.2f), Light);
            float edge = w * 0.5f - 0.05f;
            for (int i = 0; i < bolts; i++)
            {
                float t = -edge + 2f * edge * i / (bolts - 1);
                bool corner = i == 0 || i == bolts - 1;
                foreach (var (x, z, skip) in new[] { (t, -edge, false), (t, edge, false), (-edge, t, corner), (edge, t, corner) })
                {
                    if (!skip)
                    {
                        b.Cylinder(new Vector3(x, 0.136f, z), new Vector3(x, 0.158f, z), 0.02f, Steel, 6);
                    }
                }
            }
        }

        private static void CableTray(MeshBuilder b, float z0, float z1, float cableEnd)
        {
            float length = z0 - z1, mid = (z0 + z1) * 0.5f;
            b.Box(new Vector3(0f, 0.171f, mid), new Vector3(0.18f, 0.014f, length), Steel);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 0.084f, 0.2f, mid), new Vector3(0.012f, 0.07f, length), Steel);
                b.Cylinder(new Vector3(s * 0.035f, 0.196f, z0), new Vector3(s * 0.035f, 0.196f, cableEnd), 0.018f, s < 0f ? Orange : Black, 8);
            }
            int ties = Mathf.Max(2, Mathf.RoundToInt(length / 0.14f));
            for (int i = 0; i < ties; i++)
            {
                b.Box(new Vector3(0f, 0.233f, Mathf.Lerp(z0, z1, (i + 0.5f) / ties)), new Vector3(0.18f, 0.012f, 0.02f), Dark);
            }
        }

        private static void Gimbal(MeshBuilder b, Vector3 basePos, float chamberRadius)
        {
            const float top = 0.185f;
            float r = chamberRadius * 0.62f, block = chamberRadius * 0.5f, mid = (top + basePos.y) * 0.5f;
            b.Cylinder(new Vector3(basePos.x, 0.14f, basePos.z), new Vector3(basePos.x, top, basePos.z), r, Steel, 24);
            BoltCircle(b, new Vector3(basePos.x, top, basePos.z), r * 0.84f, 8, Mathf.Max(0.011f, r * 0.05f), 0.016f);
            b.Box(new Vector3(basePos.x, mid, basePos.z), new Vector3(block, basePos.y - top + 0.012f, block), Dark);
            float pin = Mathf.Min((basePos.y - top) * 0.45f, chamberRadius * 0.07f);
            b.Cylinder(new Vector3(basePos.x - block * 0.68f, mid, basePos.z), new Vector3(basePos.x + block * 0.68f, mid, basePos.z), pin, Steel, 12);
            b.Cylinder(new Vector3(basePos.x, mid, basePos.z - block * 0.68f), new Vector3(basePos.x, mid, basePos.z + block * 0.68f), pin, Steel, 12);
        }

        private static void Actuator(MeshBuilder b, Vector3 axis, float chamberRadius, float angle, float reach, float lugY)
        {
            Vector3 radial = Ring(angle, 1f, 0f), center = new Vector3(axis.x, 0f, axis.z);
            Quaternion rot = Quaternion.LookRotation(radial, Vector3.up);
            float a = Mathf.Max(0.03f, chamberRadius * 0.08f);
            Vector3 foot = center + radial * (chamberRadius + reach) + Vector3.up * 0.2f;
            Vector3 lug = center + radial * (chamberRadius + 0.045f) + Vector3.up * lugY;
            b.Box(foot - Vector3.up * 0.02f, new Vector3(a * 3f, 0.03f, a * 3f), Dark, rot);
            b.Box(lug - radial * 0.025f, new Vector3(a * 1.6f, a * 2.4f, 0.05f), Dark, rot);
            b.Sphere(foot, a * 1.1f, Steel, 10);
            b.Sphere(lug, a * 0.9f, Steel, 10);
            Vector3 along = (lug - foot).normalized, mid = Vector3.Lerp(foot, lug, 0.58f);
            b.Cylinder(foot, mid, a, Dark, 12);
            b.Cylinder(mid - along * 0.01f, mid + along * 0.015f, a * 1.18f, Orange, 12);
            b.Cylinder(mid, lug, a * 0.5f, Steel, 10);
        }

        private static void Struts(MeshBuilder b, float width, float top, float radial)
        {
            float edge = width * 0.5f - 0.2f, reach = radial / Mathf.Sqrt(2f);
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                var foot = new Vector3(x * edge, 0.19f, z * edge);
                var head = new Vector3(x * reach, top, z * reach);
                b.Box(new Vector3(foot.x, 0.175f, foot.z), new Vector3(0.14f, 0.02f, 0.14f), Dark);
                foreach (var (bx, bz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                {
                    b.Cylinder(new Vector3(foot.x + bx * 0.052f, 0.182f, foot.z + bz * 0.052f), new Vector3(foot.x + bx * 0.052f, 0.197f, foot.z + bz * 0.052f), 0.011f, Steel, 6);
                }
                b.Sphere(foot, 0.07f, Dark, 10);
                b.Cylinder(foot, head, 0.045f, Steel, 10);
                b.Sphere(head, 0.065f, Dark, 10);
            }
        }

        private static void Collar(MeshBuilder b, Vector3 axis, float radius, float y, float height, int segments)
        {
            b.Lathe(axis, Vector3.up, new List<Vector2>
            {
                new Vector2(radius - 0.004f, y - height * 0.5f), new Vector2(radius + 0.035f, y - height * 0.5f),
                new Vector2(radius + 0.035f, y + height * 0.5f), new Vector2(radius - 0.004f, y + height * 0.5f),
            }, Steel, segments);
        }

        private static Nozzle Engine(MeshBuilder b, Vector3 basePos, float chamberRadius, float chamberLength, float throatRadius, float exitRadius, float exitY, int segments, float collarY = -1f)
        {
            float y0 = basePos.y;
            float yChamber = y0 + chamberLength;
            float yThroat = yChamber + chamberRadius * 0.45f;
            var origin = new Vector3(basePos.x, 0f, basePos.z);
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
                    new Vector2(chamberRadius - 0.004f, y), new Vector2(chamberRadius + 0.02f, y + 0.02f),
                    new Vector2(chamberRadius + 0.02f, y + chamberLength * 0.08f), new Vector2(chamberRadius - 0.004f, y + chamberLength * 0.08f + 0.02f),
                }, t < 0.5f ? Orange : Dark, segments);
            }
            if (collarY > 0f)
            {
                Collar(b, origin, chamberRadius, collarY, chamberLength * 0.08f, segments);
            }
            b.Lathe(origin, Vector3.up, new List<Vector2>
            {
                new Vector2(chamberRadius * 0.9f, yChamber), new Vector2(throatRadius * 1.2f, yChamber + (yThroat - yChamber) * 0.6f), new Vector2(throatRadius, yThroat),
            }, Steel, segments);

            const int steps = 14;
            float thickness = Mathf.Max(0.02f, exitRadius * 0.04f);
            float Bell(float t) => throatRadius + (exitRadius - throatRadius) * (1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2.2f));
            float Surface(float t) => Bell(t) + thickness;
            float Y(float t) => Mathf.Lerp(yThroat, exitY, t);
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            for (int k = 0; k <= steps; k++)
            {
                float t = (float)k / steps;
                outer.Add(new Vector2(Bell(t) + thickness, Y(t)));
                inner.Add(new Vector2(Bell(t), Y(t)));
            }
            outer.Add(new Vector2(exitRadius + thickness * 1.8f, exitY));
            outer.Add(new Vector2(exitRadius, exitY + 0.005f));
            b.Lathe(origin, Vector3.up, outer, Dark, segments);
            b.Lathe(origin, Vector3.up, inner, Black, segments, true);

            float span = exitY - yThroat;
            float ribHeight = Mathf.Max(0.01f, exitRadius * 0.02f), ribWidth = Mathf.Max(0.012f, exitRadius * 0.022f);
            float band = Mathf.Max(0.015f, exitRadius * 0.035f);
            float tube = Mathf.Max(0.022f, exitRadius * 0.045f);
            const float manifoldT = 0.07f, ribEnd = 0.9f;

            float ringR = Surface(manifoldT) + tube * 0.55f, ringY = Y(manifoldT);
            int around = exitRadius > 0.5f ? 12 : 8;
            var torus = new List<Vector2>();
            for (int k = 0; k <= around; k++)
            {
                float a = Mathf.PI + Mathf.PI * 2f * k / around;
                torus.Add(new Vector2(ringR + Mathf.Cos(a) * tube, ringY + Mathf.Sin(a) * tube));
            }
            b.Lathe(origin, Vector3.up, torus, Steel, segments);

            float jumper = tube * 0.45f;
            float coneR = Mathf.Lerp(chamberRadius * 0.9f, throatRadius * 1.2f, 0.35f) - jumper * 0.5f, coneY = yChamber + (yThroat - yChamber) * 0.6f * 0.35f;
            for (int k = 0; k < 4; k++)
            {
                float angle = 45f + 90f * k;
                Vector3 to = origin + Ring(angle, coneR, coneY);
                b.Cylinder(origin + Ring(angle, ringR, ringY - tube * 0.3f), to, jumper, Steel, 8);
                b.Sphere(to, jumper * 1.4f, Dark, 8);
            }

            int ribCount = Mathf.Max(12, Mathf.RoundToInt(exitRadius * 30f));
            int ribSegments = exitRadius > 0.7f ? 5 : exitRadius > 0.5f ? 4 : 3;
            for (int s = 0; s < ribSegments; s++)
            {
                float ta = Mathf.Lerp(manifoldT, ribEnd, (float)s / ribSegments), tb = Mathf.Lerp(manifoldT, ribEnd, (float)(s + 1) / ribSegments);
                var p0 = new Vector2(Surface(ta), Y(ta));
                var p1 = new Vector2(Surface(tb), Y(tb));
                Vector2 d = (p1 - p0).normalized, n = new Vector2(d.y, -d.x);
                float sag = Mathf.Max(0f, Surface((ta + tb) * 0.5f) - (p0.x + p1.x) * 0.5f) * n.x;
                float height = ribHeight + sag + 0.004f;
                Vector2 c = (p0 + p1) * 0.5f + n * (height * 0.5f - 0.004f);
                float length = (p1 - p0).magnitude + 0.004f;
                for (int k = 0; k < ribCount; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI * 2f / ribCount;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    b.Box(origin + radial * c.x + Vector3.up * c.y, new Vector3(ribWidth, height, length), Grey,
                        Quaternion.LookRotation(radial * d.x + Vector3.up * d.y, radial * n.x + Vector3.up * n.y));
                }
            }

            void Band(float t, string color)
            {
                float lo = Surface(t - band / span) - 0.004f, hi = Surface(t + band / span) - 0.004f, top = Surface(t) + ribHeight + band * 0.6f;
                b.Lathe(origin, Vector3.up, new List<Vector2>
                {
                    new Vector2(lo, Y(t) - band), new Vector2(top, Y(t) - band * 0.4f), new Vector2(top, Y(t) + band * 0.4f), new Vector2(hi, Y(t) + band),
                }, color, segments);
            }
            Band(0.4f, Steel);
            if (exitRadius > 1f)
            {
                Band(0.65f, Steel);
            }
            Band(ribEnd, Orange);
            return new Nozzle { Exit = new Vector3(basePos.x, exitY, basePos.z), Radius = exitRadius };
        }

        private static void Pump(MeshBuilder b, Vector3 bottom, float radius, float height, Vector3 target)
        {
            float top = bottom.y + height;
            b.Cylinder(new Vector3(bottom.x, 0.14f, bottom.z), new Vector3(bottom.x, bottom.y + 0.006f, bottom.z), radius * 1.25f, Steel, 20);
            BoltCircle(b, new Vector3(bottom.x, bottom.y + 0.006f, bottom.z), radius * 1.12f, 6, Mathf.Max(0.01f, radius * 0.07f), 0.016f);
            b.Lathe(bottom, Vector3.up, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(radius * 0.8f, 0f), new Vector2(radius, radius * 0.25f),
                new Vector2(radius, height - radius * 0.25f), new Vector2(radius * 0.8f, height), new Vector2(0f, height),
            }, Dark, 16);
            b.Cylinder(bottom + Vector3.up * height * 0.45f, bottom + Vector3.up * height * 0.6f, radius * 1.08f, Orange, 16);
            foreach (float f in new[] { 0.28f, 0.9f })
            {
                float y = bottom.y + height * f;
                b.Cylinder(new Vector3(bottom.x, y - 0.012f, bottom.z), new Vector3(bottom.x, y + 0.012f, bottom.z), radius * 1.14f, Steel, 20);
                BoltCircle(b, new Vector3(bottom.x, y + 0.012f, bottom.z), radius * 1.07f, 8, Mathf.Max(0.008f, radius * 0.05f), 0.012f);
            }
            float cap = top + radius * 0.35f;
            b.Cylinder(new Vector3(bottom.x, top, bottom.z), new Vector3(bottom.x, cap, bottom.z), radius * 0.45f, Steel, 12);

            float pipe = radius * 0.28f;
            Vector3 dir = new Vector3(target.x - bottom.x, 0f, target.z - bottom.z).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            var start = new Vector3(bottom.x, target.y, bottom.z);
            if (target.y < top - pipe * 2f)
            {
                b.Cylinder(start, target + dir * 0.02f, pipe, Steel, 12);
                Flange(b, start + dir * (radius + 0.01f), dir, pipe);
            }
            else
            {
                b.Pipe(new[] { new Vector3(bottom.x, cap - 0.02f, bottom.z), start, target + dir * 0.02f }, pipe, Steel, 12);
                Flange(b, new Vector3(bottom.x, cap + FlangeThickness(pipe) * 0.5f, bottom.z), Vector3.up, pipe);
            }
            Valve(b, Vector3.Lerp(start + dir * radius, target, 0.5f), dir, pipe, side);
            Port(b, target, dir, pipe);
        }

        private static void FeedPipes(MeshBuilder b, float width, Vector3[] hydrogen, Vector3[] oxygen)
        {
            float half = width * 0.5f - 0.12f, socket = width * 0.5f - 0.078f;
            foreach (var (sx, route, color) in new[] { (-1f, hydrogen, Orange), (1f, oxygen, Cyan) })
            {
                b.Cylinder(new Vector3(sx * (half - 0.04f), 0.32f, 0f), new Vector3(sx * socket, 0.32f, 0f), 0.085f, Dark, 16);
                b.Box(new Vector3(sx * (half - 0.01f), 0.225f, 0f), new Vector3(0.12f, 0.19f, 0.12f), Dark);
                var points = new List<Vector3> { new Vector3(sx * half, 0.32f, 0f) };
                points.AddRange(route);
                b.Pipe(points, 0.05f, color, 10);
                bool valve = false;
                for (int k = 0; k + 1 < points.Count; k++)
                {
                    Vector3 d = points[k + 1] - points[k];
                    Vector3 mid = (points[k] + points[k + 1]) * 0.5f;
                    if (!valve && Mathf.Abs(d.y) < 0.01f && d.magnitude >= 0.24f)
                    {
                        Valve(b, mid, d.normalized, 0.05f, Vector3.up);
                        valve = true;
                    }
                    else if (d.magnitude >= 0.35f)
                    {
                        Flange(b, mid, d.normalized, 0.05f);
                    }
                }
            }
            b.Box(new Vector3(0f, 0.29f, width * 0.5f - 0.19f), new Vector3(0.26f, 0.28f, 0.24f), Dark);
            b.Box(new Vector3(0f, 0.44f, width * 0.5f - 0.19f), new Vector3(0.27f, 0.03f, 0.25f), Orange);
        }

        private static List<Nozzle> Medium(MeshBuilder b)
        {
            MountPlate(b, 2f, 7);
            var basePos = new Vector3(0f, 0.22f, 0f);
            Nozzle main = Engine(b, basePos, 0.42f, 0.8f, 0.2f, 0.82f, 2.94f, 32, 0.85f);
            Gimbal(b, basePos, 0.42f);
            Actuator(b, basePos, 0.42f, 200f, 0.3f, 0.58f);
            Actuator(b, basePos, 0.42f, 290f, 0.3f, 0.58f);
            Struts(b, 2f, 0.85f, 0.445f);
            Pump(b, new Vector3(0.7f, 0.2f, -0.32f), 0.15f, 0.55f, new Vector3(0.382f, 0.6f, -0.174f));
            FeedPipes(b, 2f,
                new[] { new Vector3(-0.63f, 0.32f, 0f), new Vector3(-0.63f, 0.75f, 0f), new Vector3(-0.4f, 0.75f, 0f) },
                new[] { new Vector3(0.63f, 0.32f, 0f), new Vector3(0.63f, 0.65f, 0f), new Vector3(0.4f, 0.65f, 0f) });
            Port(b, new Vector3(-0.42f, 0.75f, 0f), Vector3.right, 0.05f);
            Port(b, new Vector3(0.42f, 0.65f, 0f), Vector3.left, 0.05f);
            CableTray(b, 0.7f, 0.25f, 0.08f);
            return new List<Nozzle> { main };
        }

        private static List<Nozzle> Large(MeshBuilder b)
        {
            MountPlate(b, 3f, 10);
            b.Cylinder(new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.19f, 0f), 0.53f, Steel, 32);
            BoltCircle(b, new Vector3(0f, 0.19f, 0f), 0.517f, 16, 0.01f, 0.014f);
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
                var basePos = new Vector3(x, 0.24f, z);
                nozzles.Add(Engine(b, basePos, 0.28f, 0.75f, 0.14f, 0.6f, 3.92f, 24, 0.62f));
                Gimbal(b, basePos, 0.28f);
                Vector3 dir = new Vector3(x, 0f, z).normalized;
                b.Pipe(new[] { new Vector3(x * 0.48f, 0.9f, z * 0.48f), new Vector3(x * 0.8f, 0.9f, z * 0.8f) }, 0.06f, Steel, 10);
                Port(b, dir * 0.55f + Vector3.up * 0.9f, -dir, 0.06f);
                Port(b, dir * 0.823f + Vector3.up * 0.9f, dir, 0.06f);
            }
            Struts(b, 3f, 0.62f, 1.4f);
            Pump(b, new Vector3(-1.1f, 0.2f, 0f), 0.17f, 0.7f, new Vector3(-0.55f, 1.1f, 0f));
            Pump(b, new Vector3(1.1f, 0.2f, 0f), 0.17f, 0.7f, new Vector3(0.55f, 1.1f, 0f));
            FeedPipes(b, 3f, new[] { new Vector3(-1.25f, 0.32f, 0f) }, new[] { new Vector3(1.25f, 0.32f, 0f) });
            Port(b, new Vector3(-1.27f, 0.32f, 0f), Vector3.right, 0.05f);
            Port(b, new Vector3(1.27f, 0.32f, 0f), Vector3.left, 0.05f);
            CableTray(b, 1.2f, 0.52f, 0.45f);
            return nozzles;
        }

        private static List<Nozzle> Huge(MeshBuilder b)
        {
            MountPlate(b, 4f, 13);
            var basePos = new Vector3(0f, 0.24f, 0f);
            Nozzle main = Engine(b, basePos, 0.78f, 1.5f, 0.38f, 1.24f, 5.92f, 40, 1.54f);
            Gimbal(b, basePos, 0.78f);
            Actuator(b, basePos, 0.78f, 67.5f, 0.4f, 0.915f);
            Actuator(b, basePos, 0.78f, 292.5f, 0.4f, 0.915f);
            foreach (float angle in new[] { 45f, 135f, 225f, 315f })
            {
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                b.Box(rot * new Vector3(0f, 0.87f, 0.98f), new Vector3(0.05f, 1.41f, 0.42f), Steel, rot);
                b.Box(rot * new Vector3(0f, 0.8825f, 1.2f), new Vector3(0.08f, 1.435f, 0.04f), Orange, rot);
                b.Box(rot * new Vector3(0f, 0.175f, 1f), new Vector3(0.16f, 0.02f, 0.46f), Dark, rot);
                foreach (float d in new[] { 0.84f, 1.16f })
                {
                    foreach (float s in new[] { -0.055f, 0.055f })
                    {
                        Vector3 p = rot * new Vector3(s, 0f, d);
                        b.Cylinder(new Vector3(p.x, 0.182f, p.z), new Vector3(p.x, 0.197f, p.z), 0.012f, Steel, 6);
                    }
                }
            }
            Struts(b, 4f, 1.45f, 1.17f);
            foreach (var (x, color) in new[] { (-1.38f, Orange), (1.38f, Cyan) })
            {
                b.Sphere(new Vector3(x, 0.85f, 0f), 0.55f, White, 24);
                b.Cylinder(new Vector3(x, 0.82f, 0f), new Vector3(x, 0.92f, 0f), 0.56f, color, 24);
                b.Lathe(new Vector3(x, 0.14f, 0f), Vector3.up, new List<Vector2>
                {
                    new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.27f, 0.03f), new Vector2(0.22f, 0.24f), new Vector2(0f, 0.24f),
                }, Dark, 20);
                float s = Mathf.Sign(x);
                b.Pipe(new[] { new Vector3(s * 1.05f, 0.85f, 0f), new Vector3(s * 0.76f, 0.85f, 0f) }, 0.08f, color, 12);
                Port(b, new Vector3(s * 0.78f, 0.85f, 0f), new Vector3(-s, 0f, 0f), 0.08f);
            }
            var nozzles = new List<Nozzle> { main };
            for (int k = 0; k < 6; k++)
            {
                float angle = 30f + 60f * k;
                Vector3 radial = Ring(angle, 1f, 0f);
                Vector3 pos = Ring(angle, 1.46f, 1.6f);
                Quaternion rot = Quaternion.LookRotation(radial, Vector3.up);
                nozzles.Add(Engine(b, pos, 0.22f, 0.55f, 0.11f, 0.42f, 3.6f, 20));
                b.Cylinder(new Vector3(pos.x, 1.555f, pos.z), new Vector3(pos.x, 1.602f, pos.z), 0.2f, Dark, 20);
                b.Box(Ring(angle, 1.1f, 1.53f), new Vector3(0.1f, 0.06f, 0.72f), Steel, rot);
                b.Cylinder(Ring(angle, 1.3f, 1.52f), Ring(angle, 0.77f, 1.2f), 0.03f, Steel, 10);
                b.Box(Ring(angle, 0.79f, 1.2f), new Vector3(0.08f, 0.1f, 0.05f), Dark, rot);
                b.Pipe(new[] { Ring(angle, 0.5f, 1.9f), Ring(angle, 1.26f, 1.9f) }, 0.03f, Steel, 10);
                Flange(b, Ring(angle, 0.58f, 1.9f), radial, 0.03f);
                Valve(b, Ring(angle, 0.92f, 1.9f), radial, 0.03f, Vector3.up);
                Port(b, Ring(angle, 1.24f, 1.9f), radial, 0.03f);
            }
            Pump(b, new Vector3(0f, 0.2f, -1.35f), 0.2f, 0.9f, new Vector3(0f, 0.85f, -0.78f));
            Pump(b, new Vector3(0f, 0.2f, 1.1f), 0.18f, 0.6f, new Vector3(0f, 1f, 0.78f));
            FeedPipes(b, 4f,
                new[] { new Vector3(-1.76f, 0.32f, 0f), new Vector3(-1.76f, 0.58f, 0f) },
                new[] { new Vector3(1.76f, 0.32f, 0f), new Vector3(1.76f, 0.58f, 0f) });
            CableTray(b, 1.7f, 1.31f, 1.22f);
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
