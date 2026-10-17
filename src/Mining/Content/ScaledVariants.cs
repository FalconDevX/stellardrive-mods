using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.FluidPump;
using Ships.Parts.GasExhaust;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class ScaledVariants
    {
        internal enum Kind
        {
            Vent,
            Pump,
            Electrolyzer,
            WaterPump,
            Intake,
        }

        internal sealed class Variant
        {
            public ushort Id;
            public Kind Kind;
            public string Label;
            public int Scale;
            public int Iron, Copper, Glass;
            public float Rate => Scale * Scale;
        }

        internal static readonly Variant[] Variants =
        {
            new Variant { Id = 237, Kind = Kind.Vent, Label = "Air Vent M", Scale = 2, Iron = 8, Copper = 4, Glass = 0 },
            new Variant { Id = 238, Kind = Kind.Vent, Label = "Air Vent L", Scale = 3, Iron = 18, Copper = 8, Glass = 0 },
            new Variant { Id = 239, Kind = Kind.Pump, Label = "Fluid Pump M", Scale = 2, Iron = 20, Copper = 10, Glass = 0 },
            new Variant { Id = 240, Kind = Kind.Pump, Label = "Fluid Pump L", Scale = 3, Iron = 45, Copper = 20, Glass = 0 },
            new Variant { Id = 241, Kind = Kind.Electrolyzer, Label = "Electrolyzer M", Scale = 2, Iron = 40, Copper = 20, Glass = 10 },
            new Variant { Id = 242, Kind = Kind.Electrolyzer, Label = "Electrolyzer L", Scale = 3, Iron = 90, Copper = 45, Glass = 20 },
            new Variant { Id = 245, Kind = Kind.WaterPump, Label = "Water Pump M", Scale = 2, Iron = 25, Copper = 14, Glass = 4 },
            new Variant { Id = 246, Kind = Kind.WaterPump, Label = "Water Pump L", Scale = 3, Iron = 55, Copper = 30, Glass = 8 },
            new Variant { Id = 249, Kind = Kind.Intake, Label = "Gas Intake M", Scale = 2, Iron = 10, Copper = 4, Glass = 0 },
            new Variant { Id = 250, Kind = Kind.Intake, Label = "Gas Intake L", Scale = 3, Iron = 22, Copper = 9, Glass = 0 },
        };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly Dictionary<ushort, float> Rates = Variants.ToDictionary(v => v.Id, v => v.Rate);
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static IEnumerable<ushort> Ids => Variants.Select(v => v.Id);

        internal static PartSettings[] All => Variants.Select(v => Get(v.Id)).Where(p => p != null).ToArray();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static ushort BaseId(ushort id) => BaseIds.TryGetValue(id, out ushort b) ? b : (ushort)0;

        private static readonly Dictionary<ushort, ushort> BaseIds = new Dictionary<ushort, ushort>();

        internal static float Rate(PartSettings settings) => settings != null && Rates.TryGetValue(settings.id, out float r) ? r : 1f;

        private static PartSettings FindBase(Kind kind, List<PartSettings> parts)
        {
            switch (kind)
            {
                case Kind.Vent:
                    return parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<GasExhaustVisuals>(true) != null);
                case Kind.Pump:
                    return parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<FluidPumpVisuals>(true) != null);
                case Kind.WaterPump:
                    return WaterPump.GetOrCreatePart(parts);
                case Kind.Intake:
                    return parts.FirstOrDefault(p => p?.part != null && p.internalStateClass == "GasIntakeState");
                default:
                    return ElectrolyzerPart.GetOrCreatePart(parts);
            }
        }

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            Variant v = Variants.First(x => x.Id == id);
            List<PartSettings> parts = gameParts.ToList();
            PartSettings source = FindBase(v.Kind, parts);
            if (source?.part == null)
            {
                return null;
            }
            try
            {
                PartSettings built = Build(source, v, parts);
                Parts[id] = built;
                BaseIds[id] = source.id;
                Melon<MiningMod>.Logger.Msg($"Added part '{built.fullLabel}' (id {id}, {v.Scale}x of part {source.id})");
                return built;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build {v.Label}: {e}");
                return null;
            }
        }

        private static string Describe(Variant v)
        {
            string size = $"{v.Scale}x larger";
            switch (v.Kind)
            {
                case Kind.Vent:
                    return $"An {size} air vent: pipe oxygen into it and it fills the room {v.Rate:0} times faster than the small vent ({0.5f * v.Rate:0.#} units per second).";
                case Kind.Pump:
                    return $"A {size} fluid pump: moves up to {5f * v.Rate:0} units per second ({v.Rate:0} times the small pump) from the inlet pipe into the outlet pipe until the outlet side is full, also against a fuller tank. The signal sets the speed (0 = off, 0.5 = half, 1 = full). Connect the two tanks only through the pump, a direct pipe between them makes them one network that just evens out.";
                case Kind.Intake:
                    return $"An {size} gas intake: put it outside a room and pipe it to a tank, it collects oxygen from the atmosphere {v.Rate:0} times faster than the small intake. " +
                           "The pipe socket is on the back.";
                case Kind.WaterPump:
                    return $"A {size} water pump with a thick hose: press F over a lake on Otrera b to lower it, or set the hose length with a signal (1 = full, 0 = up). " +
                           $"Pumps {v.Rate:0} times more water than the small pump and uses {v.Rate:0} times the power. " +
                           "All four sockets are on the service panel at the back: power (yellow), signal (white) and two water outputs (blue).";
                default:
                    return $"An industrial electrolyzer ({v.Scale}x{v.Scale}): a long electrolysis stack with radiators, {v.Scale} hydrogen and {v.Scale} oxygen tanks, " +
                           $"{v.Rate:0} times faster than the small one and uses {v.Rate:0} times the power. Put ice in the hopper on the front, or pipe water in. " +
                           "Sockets on the cell centres: hydrogen out on the left, oxygen out on the right, power (left) and water (right) on the back.";
            }
        }

        private static GameObject BuildGasModel(PartSettings source, Variant v)
        {
            bool vent = v.Kind == Kind.Vent;
            int n = v.Scale;
            float off = (n - 1) * 0.5f;
            var centre = new Vector3(off, 0f, off);
            GameObject root = Object.Instantiate(source.part, PartFactory.Holder);
            root.name = v.Label.Replace(" ", "");
            Mesh body = ModAssets.LoadMesh(vent ? "vent_big.bin" : "intake_big.bin");
            Transform visual = root.transform.Find("Visual") ?? root.transform;
            MeshRenderer look = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponentInParent<SocketObject>() == null);
            if (look == null)
            {
                throw new Exception("base part has no renderer to copy");
            }
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), root.name);
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                if (r == lookCopy || r.GetComponentInParent<SocketObject>() != null)
                {
                    continue;
                }
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            GameObject model = PartFactory.CreateRenderer("Body", visual, body, material, lookCopy);
            model.transform.position = root.transform.TransformPoint(centre);
            model.transform.rotation = root.transform.rotation;
            model.transform.localScale = Vector3.one * n;
            Object.DestroyImmediate(lookCopy.gameObject);

            var bounds = new Bounds(centre + body.bounds.center * n, body.bounds.size * n);
            PartFactory.SetBounds(root, bounds);
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.GetComponentInParent<SocketObject>() == null))
            {
                Transform t = box.transform;
                box.center = t.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = Abs(t.InverseTransformVector(root.transform.TransformVector(bounds.size)));
            }
            float top = bounds.max.y;
            foreach (UnityEngine.VFX.VisualEffect fx in root.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
            {
                fx.transform.position = root.transform.TransformPoint(new Vector3(centre.x, top, centre.z));
                fx.transform.localScale *= n;
            }

            SocketObject[] sockets = root.GetComponentsInChildren<SocketObject>(true);
            SocketObject fluid = sockets.Where(s => s.Type == Ships.Interface.Model.Parts.CableType.Fluid).OrderBy(s => s.Id).FirstOrDefault();
            if (fluid == null)
            {
                throw new Exception("base part has no pipe socket");
            }
            SocketObject socket = PartFactory.CloneSocket(fluid, root.transform, fluid.Id, centre + new Vector3(0f, GasSocketY * n, -0.49f * n - 0.02f), 0f);
            socket.transform.localScale *= n;
            foreach (SocketObject old in sockets)
            {
                Object.DestroyImmediate(old.gameObject);
            }
            return root;
        }

        private const float GasSocketY = 0.13f;

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static PartSettings Build(PartSettings source, Variant v, List<PartSettings> parts)
        {
            PartSettings settings;
            GameObject root;
            if (v.Kind == Kind.WaterPump || v.Kind == Kind.Electrolyzer)
            {
                settings = v.Kind == Kind.WaterPump ? WaterPump.BuildScaled(parts, v.Scale) : ElectrolyzerPart.BuildScaled(parts, v.Scale);
                if (settings == null)
                {
                    throw new Exception("base parts missing");
                }
                root = settings.part;
                root.name = v.Label.Replace(" ", "");
            }
            else if (v.Kind == Kind.Vent || v.Kind == Kind.Intake)
            {
                root = BuildGasModel(source, v);
                settings = Object.Instantiate(source);
            }
            else
            {
                root = Object.Instantiate(source.part, PartFactory.Holder);
                root.name = v.Label.Replace(" ", "");
                PartFactory.ScaleUniform(root, source.size, v.Scale, v.Label, v.Kind != Kind.Pump, v.Kind == Kind.Pump);
                settings = Object.Instantiate(source);
            }
            settings.name = "Mining" + root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.Id;
            settings.fullLabel = v.Label;
            settings.description = Describe(v);
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = source.size * v.Scale;
            settings.part = root;
            settings.mass = source.mass * v.Rate;
            var cost = new List<(uint, int)> { (Items.Model.ItemsConstants.IronItemId, v.Iron), (112, v.Copper) };
            if (v.Glass > 0) cost.Add((Items.Model.ItemsConstants.GlassItemId, v.Glass));
            PartFactory.SetCost(settings, cost.ToArray());
            return settings;
        }
    }
}
