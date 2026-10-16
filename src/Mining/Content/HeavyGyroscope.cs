using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Settings;
using Ships.Parts.Gyroscope;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class HeavyGyroscope
    {
        internal const ushort PartId = 248;
        internal const ushort GyroscopePartId = 53;
        internal const float Strength = 8f;
        internal const float Scale = 2f;
        private static bool _failed;

        internal static PartSettings Part { get; private set; }

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (Part != null || _failed)
            {
                return Part;
            }
            PartSettings source = PartFactory.Find(gameParts, GyroscopePartId);
            if (source?.part == null)
            {
                return null;
            }
            try
            {
                Part = Build(source);
                Melon<MiningMod>.Logger.Msg($"Added part '{Part.fullLabel}' (id {PartId}, {Scale:0}x size, {Strength:0}x gyroscope)");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the Heavy Gyroscope: {e}");
            }
            return Part;
        }

        private static PartSettings Build(PartSettings source)
        {
            GameObject root = Object.Instantiate(source.part, PartFactory.Holder);
            root.name = "HeavyGyroscope";
            Transform visuals = root.GetComponentInChildren<GyroscopeVisuals>(true)?.transform ?? throw new Exception("gyroscope has no visuals");
            MeshRenderer look = visuals.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Albedo"))
                                ?? throw new Exception("gyroscope has no textured renderer");

            Material frame = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("reactor_palette.png"), "HeavyGyroscopeFrame");
            Material glow = new Material(frame) { name = "HeavyGyroscopeGlow" };
            glow.SetTexture("_EmissionMap", ModAssets.LoadTexture("reactor_palette.png"));
            glow.SetColor("_EmissionColor", Color.black);
            glow.EnableKeyword("_EMISSION");
            PartFactory.CreateRenderer("Frame", visuals, ModAssets.LoadMesh("heavy_gyro_frame.bin"), frame, look);
            GameObject lamps = PartFactory.CreateRenderer("Lamps", visuals, ModAssets.LoadMesh("heavy_gyro_glow.bin"), glow, look);
            lamps.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Transform bar = visuals.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GyroscopeBar") ?? throw new Exception("gyroscope has no bar");
            PartFactory.CreateRenderer("BarDetail", bar, ModAssets.LoadMesh("heavy_gyro_bar.bin"), frame, look);
            Tint(visuals, "GyroscopeWheel", new Color(0.62f, 0.66f, 0.74f));
            visuals.gameObject.AddComponent<HeavyGyroscopeVisuals>();
            PartFactory.ScaleUniform(root, source.size, Scale, "Heavy Gyroscope", false);

            var settings = Object.Instantiate(source);
            settings.name = "MiningHeavyGyroscope";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Heavy Gyroscope";
            settings.description = $"A {Scale:0}x{Scale:0}x{Scale:0} gyroscope with a heavy flywheel, reinforced bearings and its own power conditioners, {Strength:0} times stronger than the normal one. " +
                                   $"Uses {Strength:0} times the power (up to {800f * Strength / 1000f:0.#} kW while turning). Wire it like the normal gyroscope: three rotation signals and power on either side.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.size = source.size * Scale;
            settings.mass = source.mass * Strength;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 80), (112, 16));
            return settings;
        }

        private static void Tint(Transform visuals, string name, Color color)
        {
            MeshRenderer renderer = visuals.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name);
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }
            var material = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + "Heavy" };
            foreach (string property in new[] { "_Color", "_BaseColor" })
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, material.GetColor(property) * color);
                }
            }
            renderer.sharedMaterial = material;
        }
    }

    internal class HeavyGyroscopeVisuals : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<GyroscopeAnimator, float> BarSpeed = AccessTools.FieldRefAccess<GyroscopeAnimator, float>("_currentBarRotationSpeed");
        private static readonly AccessTools.FieldRef<GyroscopeAnimator, float> WheelSpeed = AccessTools.FieldRefAccess<GyroscopeAnimator, float>("_currentWheelRotationSpeed");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Lamp = new Color(0.3f, 0.9f, 1f);
        private const float Idle = 0.6f, Busy = 4f;

        private GyroscopeAnimator _animator;
        private Material _material;
        private float _shown = -1f;

        private void Awake()
        {
            _animator = GetComponent<GyroscopeAnimator>() ?? GetComponentInParent<GyroscopeAnimator>();
            Renderer lamps = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Lamps");
            _material = lamps != null ? lamps.material : null;
        }

        private void Update()
        {
            if (_material == null)
            {
                return;
            }
            float activity = 0f;
            if (_animator != null)
            {
                activity = Mathf.Clamp01(Mathf.Max(Mathf.Abs(BarSpeed(_animator)) / 20f, Mathf.Abs(WheelSpeed(_animator)) / 10f));
            }
            float level = Mathf.Lerp(Idle, Busy, activity);
            if (Mathf.Abs(level - _shown) < 0.02f)
            {
                return;
            }
            _shown = level;
            _material.SetColor(EmissionId, Lamp * level);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
