using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Thruster;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class HydrogenThruster
    {
        internal const ushort PartId = 201;
        internal const uint HydrogenItemId = 106;
        internal const uint WaterItemId = 105;
        internal const byte PowerSocket = 2;
        internal const float PowerWatts = 3000f;
        internal static readonly Vector3 NozzleTop = new Vector3(0f, 1.98f, 0f);

        internal const ushort ChemicalThrusterId = 19;
        internal const ushort GeneratorPartId = 60;
        private static ThrusterSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && (settings.id == PartId || HydrogenEngines.Find(settings.id) != null);

        internal static float Scale(PartSettings settings) => HydrogenEngines.Find(settings.id)?.Scale ?? 1f;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> list = gameParts.ToList();
            var chemical = PartFactory.Find(list, ChemicalThrusterId) as ThrusterSettings;
            PartSettings generator = PartFactory.Find(list, GeneratorPartId);
            if (chemical?.part == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chemical, generator);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the hydrogen thruster: {e}");
            }
            return _part;
        }

        private static ThrusterSettings Build(ThrusterSettings chemical, PartSettings generator)
        {
            GameObject root = Object.Instantiate(chemical.part, PartFactory.Holder);
            root.name = "HydrogenThruster";

            Mesh mesh = ModAssets.LoadMesh("hydrogen_thruster.bin");
            Texture2D albedo = ModAssets.LoadTexture("hydrogen_thruster_albedo.jpg");

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
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, albedo, "HydrogenThruster");
            PartFactory.CreateRenderer("Body", visual.transform, mesh, material, look);
            PartFactory.CreateRenderer("Mounts", visual.transform, ModAssets.LoadMesh("thruster_mounts.bin"),
                PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("hydrogen_albedo.png"), "HydrogenThrusterMounts"), look);

            Vector3 nozzleWorld = root.transform.TransformPoint(NozzleTop);
            if (flame != null)
            {
                flame.transform.position = nozzleWorld;
                TintFlame(flame.gameObject);
            }
            if (sound != null) sound.transform.position = nozzleWorld;

            PartFactory.SetBounds(root, mesh.bounds);

            foreach (SocketObject extra in root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Fluid && s.Id > 1).ToArray())
            {
                Object.DestroyImmediate(extra.gameObject);
            }
            SocketObject[] fluids = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Fluid).OrderBy(s => s.Id).ToArray();
            foreach (SocketObject f in fluids)
            {
                HarmonyLib.Traverse.Create(f).Field("relayedSockets").SetValue(new SocketObject[0]);
            }
            for (int i = 0; i < fluids.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                fluids[i].transform.localPosition = new Vector3(side * 0.42f, 0.30f, 0.05f);
                fluids[i].transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : 270f, 0f);
            }
            PartFactory.CloneSocket(PartFactory.FindSocket(generator, CableType.Power), root.transform, PowerSocket, new Vector3(0f, 0.30f, 0.44f), 180f);

            var settings = ScriptableObject.CreateInstance<ThrusterSettings>();
            settings.name = "MiningHydrogenThruster";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Hydrogen Thruster";
            settings.internalStateClass = chemical.internalStateClass;
            settings.snappingStyle = chemical.snappingStyle;
            settings.description = $"Hydrogen thruster: needs hydrogen and oxygen (one pipe in each side socket) and power (back, {PowerWatts:0} W, e.g. from the Nuclear Reactor). " +
                                   "It waits until all three are there. " +
                                   $"{MiningMod.HydrogenThrustMultiplier.Value:0.#}x stronger than the chemical thruster. Make hydrogen in the Electrolyzer.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = chemical.thumbnailRotation;
            settings.mass = 45f;
            settings.size = new Vector3(1f, 2f, 1f);
            settings.thrustStrength = chemical.thrustStrength;
            settings.maxFuel = chemical.maxFuel;
            settings.fuelDurationPerUnit = chemical.fuelDurationPerUnit;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 20), (112, 10), (Items.Model.ItemsConstants.GlassItemId, 5));
            return settings;
        }

        private static readonly Color FlameColor = new Color(0.22f, 0.62f, 1f);

        private static Color Tint(Color c)
        {
            float brightness = Mathf.Max(c.r, c.g, c.b);
            return new Color(FlameColor.r * brightness, FlameColor.g * brightness, FlameColor.b * brightness, c.a);
        }

        private static Gradient Tint(Gradient g)
        {
            return new Gradient
            {
                mode = g.mode,
                colorKeys = g.colorKeys.Select(k => new GradientColorKey(Tint(k.color), k.time)).ToArray(),
                alphaKeys = g.alphaKeys
            };
        }

        private static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Tint(g.color));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Tint(g.colorMin), Tint(g.colorMax));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Tint(g.gradientMin), Tint(g.gradientMax));
                default:
                    return new ParticleSystem.MinMaxGradient(Tint(g.gradient)) { mode = g.mode };
            }
        }

        internal static void TintFlame(GameObject flame)
        {
            foreach (ParticleSystem ps in flame.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = Tint(main.startColor);
                ParticleSystem.ColorOverLifetimeModule overLifetime = ps.colorOverLifetime;
                if (overLifetime.enabled)
                {
                    overLifetime.color = Tint(overLifetime.color);
                }
                ParticleSystem.ColorBySpeedModule bySpeed = ps.colorBySpeed;
                if (bySpeed.enabled)
                {
                    bySpeed.color = Tint(bySpeed.color);
                }
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var material = new Material(renderer.sharedMaterial) { name = "HydrogenFlame" };
                    foreach (string property in new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor" })
                    {
                        if (material.HasProperty(property))
                        {
                            Color c = material.GetColor(property);
                            Color tinted = Tint(c);
                            if (property == "_EmissionColor")
                            {
                                float scale = Mathf.Max(c.r, c.g, c.b, 1e-4f);
                                tinted = new Color(FlameColor.r * scale, FlameColor.g * scale, FlameColor.b * scale, c.a);
                            }
                            material.SetColor(property, tinted);
                        }
                    }
                    renderer.sharedMaterial = material;
                }
            }
            foreach (Light light in flame.GetComponentsInChildren<Light>(true))
            {
                light.color = FlameColor;
            }
        }
    }
}
