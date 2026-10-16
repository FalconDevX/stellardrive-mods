using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Settings;
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
            Electrolyzer,
            WaterPump,
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
            new Variant { Id = 241, Kind = Kind.Electrolyzer, Label = "Electrolyzer M", Scale = 2, Iron = 40, Copper = 20, Glass = 10 },
            new Variant { Id = 242, Kind = Kind.Electrolyzer, Label = "Electrolyzer L", Scale = 3, Iron = 90, Copper = 45, Glass = 20 },
            new Variant { Id = 245, Kind = Kind.WaterPump, Label = "Water Pump M", Scale = 2, Iron = 25, Copper = 14, Glass = 4 },
            new Variant { Id = 246, Kind = Kind.WaterPump, Label = "Water Pump L", Scale = 3, Iron = 55, Copper = 30, Glass = 8 },
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
                case Kind.WaterPump:
                    return WaterPump.GetOrCreatePart(parts);
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
                case Kind.WaterPump:
                    return $"A {size} water pump with a thick hose: press F over a lake on Otrera b to lower it, or set the hose length with a signal (1 = full, 0 = up). " +
                           $"Pumps {v.Rate:0} times more water than the small pump and uses {v.Rate:0} times the power. " +
                           "Sockets on the cell centres: right - two water outputs, back - signal (left) and power (right).";
                default:
                    return $"An industrial electrolyzer ({v.Scale}x{v.Scale}): a long electrolysis stack with radiators, {v.Scale} hydrogen and {v.Scale} oxygen tanks, " +
                           $"{v.Rate:0} times faster than the small one and uses {v.Rate:0} times the power. Put ice in the hopper on the front, or pipe water in. " +
                           "Sockets on the cell centres: hydrogen out on the left, oxygen out on the right, power (left) and water (right) on the back.";
            }
        }

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
            else
            {
                root = Object.Instantiate(source.part, PartFactory.Holder);
                root.name = v.Label.Replace(" ", "");
                PartFactory.ScaleUniform(root, source.size, v.Scale, v.Label);
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
