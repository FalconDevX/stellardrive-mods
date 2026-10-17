using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MelonLoader;
using MelonLoader.Utils;
using Tools.Common;
using UnityEngine;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal sealed class JetpackFx : MonoBehaviour
    {
        private const float MaxGroundDistance = 6f;
        private const float MaxHearDistance = 40f;

        private ParticleSystem _fire;
        private ParticleSystem _dust;
        private bool _firePlaying;
        private bool _dustPlaying;
        private FMOD.Channel _channel;
        private bool _hasChannel;
        private float _intensity;

        internal bool Flying;
        internal Vector3 Up = Vector3.up;

        private sealed class NozzleFlame
        {
            public Transform Root;
            public ParticleSystem Core, Flame, Smoke;
            public Light Glow;
            public bool Playing;
        }

        private readonly NozzleFlame[] _nozzles = new NozzleFlame[2];
        private static Material _additive;
        private static bool _additiveTried;

        internal static JetpackFx For(Component effects)
        {
            var fx = effects.GetComponent<JetpackFx>();
            return fx != null ? fx : effects.gameObject.AddComponent<JetpackFx>();
        }

        private void Awake()
        {
            _fire = CreateEffect("JetpackGroundFire", lifetime: (0.25f, 0.55f), size: (0.25f, 0.6f), speed: (2.5f, 4.5f), rate: 45f, angle: 80f, radius: 0.2f, grow: 1.6f);
            if (_fire != null)
            {
                var color = _fire.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) },
                });
                var main = _fire.main;
                main.startColor = Color.white;
            }
            _dust = CreateEffect("JetpackGroundDust", lifetime: (1.2f, 2.2f), size: (0.6f, 1.3f), speed: (1.2f, 2.4f), rate: 14f, angle: 85f, radius: 0.4f, grow: 2.2f);
            if (_dust != null)
            {
                var main = _dust.main;
                main.startColor = new Color(0.55f, 0.5f, 0.45f, 0.35f);
            }
        }

        internal bool UpdateNozzles(Transform left, Transform right, Quaternion bodyRotation)
        {
            bool ok = UpdateNozzle(0, left, bodyRotation);
            ok &= UpdateNozzle(1, right, bodyRotation);
            return ok;
        }

        private bool UpdateNozzle(int index, Transform nozzle, Quaternion bodyRotation)
        {
            if (nozzle == null)
            {
                return true;
            }
            NozzleFlame flame = _nozzles[index] ?? (_nozzles[index] = CreateNozzleFlame(index));
            if (flame.Core == null)
            {
                return false;
            }
            flame.Root.SetPositionAndRotation(nozzle.position, bodyRotation * Quaternion.LookRotation(Vector3.down));
            return true;
        }

        private NozzleFlame CreateNozzleFlame(int index)
        {
            var flame = new NozzleFlame { Root = new GameObject($"JetpackFlame{index}").transform };
            flame.Root.SetParent(transform, false);
            Material additive = AdditiveMaterial();
            flame.Core = CreateEffect("Core", (0.05f, 0.09f), (0.1f, 0.16f), (10f, 13f), 140f, 2.5f, 0.015f, 0.35f, flame.Root, 120, additive, ParticleSystemSimulationSpace.Local);
            flame.Flame = CreateEffect("Flame", (0.12f, 0.22f), (0.14f, 0.26f), (6f, 9f), 110f, 7f, 0.03f, 1.8f, flame.Root, 120, additive, ParticleSystemSimulationSpace.World);
            flame.Smoke = CreateEffect("Smoke", (0.6f, 1.1f), (0.2f, 0.35f), (2.5f, 4f), 22f, 12f, 0.05f, 3.5f, flame.Root, 60, null, ParticleSystemSimulationSpace.World);
            if (flame.Core == null)
            {
                return flame;
            }
            Colorize(flame.Core,
                new[] { new GradientColorKey(new Color(0.75f, 0.85f, 1f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0.4f), new GradientColorKey(new Color(1f, 0.7f, 0.3f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            Colorize(flame.Flame,
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.08f), 0.35f), new GradientColorKey(new Color(0.5f, 0.1f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.55f, 0.45f), new GradientAlphaKey(0f, 1f) });
            Colorize(flame.Smoke,
                new[] { new GradientColorKey(new Color(0.35f, 0.32f, 0.3f), 0f), new GradientColorKey(new Color(0.55f, 0.53f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.28f, 0.2f), new GradientAlphaKey(0f, 1f) });
            if (flame.Flame != null)
            {
                var noise = flame.Flame.noise;
                noise.enabled = true;
                noise.strength = 0.6f;
                noise.frequency = 2.5f;
                noise.scrollSpeed = 1.5f;
            }
            if (flame.Smoke != null)
            {
                var noise = flame.Smoke.noise;
                noise.enabled = true;
                noise.strength = 0.4f;
                noise.frequency = 0.8f;
            }
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(flame.Root, false);
            glow.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.25f);
            glow.range = 4f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            flame.Glow = glow;
            return flame;
        }

        private static void Colorize(ParticleSystem effect, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            if (effect == null)
            {
                return;
            }
            var main = effect.main;
            main.startColor = Color.white;
            var overLifetime = effect.colorOverLifetime;
            overLifetime.enabled = true;
            overLifetime.color = new ParticleSystem.MinMaxGradient(new Gradient { colorKeys = colors, alphaKeys = alphas });
        }

        private static Material AdditiveMaterial()
        {
            if (_additiveTried)
            {
                return _additive;
            }
            _additiveTried = true;
            ParticleSystem template = ParticleTemplates.Get();
            ParticleSystemRenderer templateRenderer = template != null ? template.GetComponent<ParticleSystemRenderer>() : null;
            Material source = templateRenderer != null ? templateRenderer.sharedMaterial : null;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (source == null || shader == null)
            {
                Melon<GearMod>.Logger.Msg($"Jetpack flame: no additive particle shader ({(shader == null ? "shader missing" : "template missing")}), using the dust material");
                return null;
            }
            var m = new Material(shader) { name = "JetpackFlame" };
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3100;
            _additive = m;
            Melon<GearMod>.Logger.Msg($"Jetpack flame: additive material, texture {texture?.name}");
            return m;
        }

        private void UpdateFlames()
        {
            float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            foreach (NozzleFlame flame in _nozzles)
            {
                if (flame == null || flame.Core == null)
                {
                    continue;
                }
                bool play = Flying;
                if (play != flame.Playing)
                {
                    foreach (ParticleSystem ps in new[] { flame.Core, flame.Flame, flame.Smoke })
                    {
                        if (ps == null) continue;
                        if (play) ps.Play(true);
                        else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                    flame.Playing = play;
                }
                if (flame.Glow != null)
                {
                    flame.Glow.intensity = _intensity * 2.2f * flicker;
                    flame.Glow.enabled = flame.Glow.intensity > 0.01f;
                }
            }
        }

        private ParticleSystem CreateEffect(string name, (float, float) lifetime, (float, float) size, (float, float) speed, float rate, float angle, float radius, float grow)
        {
            return CreateEffect(name, lifetime, size, speed, rate, angle, radius, grow, transform, 80, null, ParticleSystemSimulationSpace.World);
        }

        private ParticleSystem CreateEffect(string name, (float, float) lifetime, (float, float) size, (float, float) speed, float rate, float angle, float radius, float grow,
            Transform parent, int maxParticles, Material material, ParticleSystemSimulationSpace space)
        {
            ParticleSystem template = ParticleTemplates.Get();
            if (template == null)
            {
                return null;
            }
            ParticleSystem effect = Instantiate(template.gameObject, parent).GetComponent<ParticleSystem>();
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            if (material != null)
            {
                effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            }
            effect.name = name;
            effect.transform.localScale = Vector3.one;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.maxParticles = maxParticles;
            main.simulationSpace = space;
            var emission = effect.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            var sizeOverLifetime = effect.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            return effect;
        }

        private void LateUpdate()
        {
            bool onGround = false;
            if (Flying && ClientScene.PhysicsScene.Raycast(transform.position + Up * 0.5f, -Up, out RaycastHit hit, MaxGroundDistance + 0.5f, RaycastMasks.BuildDetectionLayer))
            {
                onGround = true;
                Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.ProjectOnPlane(transform.forward, hit.normal) + hit.normal * 0.001f);
                float closeness = 1f - Mathf.Clamp01((hit.distance - 0.5f) / MaxGroundDistance);
                if (_fire != null)
                {
                    _fire.transform.SetPositionAndRotation(hit.point + hit.normal * 0.05f, rotation);
                    var emission = _fire.emission;
                    emission.rateOverTime = 45f * closeness;
                }
                if (_dust != null)
                {
                    _dust.transform.SetPositionAndRotation(hit.point + hit.normal * 0.1f, rotation);
                    var emission = _dust.emission;
                    emission.rateOverTime = 14f * Mathf.Sqrt(closeness);
                }
            }
            SetPlaying(_fire, ref _firePlaying, onGround);
            SetPlaying(_dust, ref _dustPlaying, onGround);

            _intensity = Mathf.MoveTowards(_intensity, Flying ? 1f : 0f, Time.deltaTime * (Flying ? 6f : 4f));
            UpdateFlames();
            UpdateSound();
        }

        private static void SetPlaying(ParticleSystem effect, ref bool playing, bool shouldPlay)
        {
            if (effect == null || playing == shouldPlay)
            {
                return;
            }
            if (shouldPlay) effect.Play(true);
            else effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            playing = shouldPlay;
        }

        internal static bool HasCustomSound => JetpackSound.TryGet(out _);

        private void UpdateSound()
        {
            Camera cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / MaxHearDistance);
            float volume = _intensity * attenuation * attenuation * GearMod.JetpackVolume.Value;
            if (volume <= 0.001f || !JetpackSound.TryGet(out FMOD.Sound sound))
            {
                StopSound();
                return;
            }
            if (_hasChannel && !Audio.DrillSound.IsAlive(_channel))
            {
                _hasChannel = false;
            }
            if (_hasChannel && (_channel.isVirtual(out bool isVirtual) != FMOD.RESULT.OK || isVirtual))
            {
                _channel.stop();
                _hasChannel = false;
                Melon<GearMod>.Logger.Msg("Jetpack sound: channel went virtual, restarting");
            }
            if (_hasChannel && _channel.getPaused(out bool paused) == FMOD.RESULT.OK && paused)
            {
                _channel.setPaused(false);
            }
            if (!_hasChannel)
            {
                _channel = Audio.DrillSound.Play(sound);
                _hasChannel = true;
            }
            _channel.setVolume(volume);
            _channel.setPitch(0.85f + 0.15f * _intensity);
            if (cam != null)
            {
                Vector3 local = cam.transform.InverseTransformPoint(transform.position);
                _channel.setPan(Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f);
            }
        }

        private void StopSound()
        {
            if (_hasChannel)
            {
                _channel.stop();
                _hasChannel = false;
            }
        }

        private void OnDisable() => StopSound();

        private void OnDestroy() => StopSound();
    }

    internal static class JetpackSound
    {
        private static FMOD.Sound _sound;
        private static bool _created;
        private static bool _missing;

        internal static bool TryGet(out FMOD.Sound sound)
        {
            sound = _sound;
            if (_created || _missing)
            {
                return _created;
            }
            try
            {
                if (!FMODUnity.RuntimeManager.IsInitialized)
                {
                    return false;
                }
                string source = null;
                byte[] data = null;
                foreach (string ext in new[] { "wav", "ogg", "mp3" })
                {
                    string path = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "jetpack." + ext);
                    if (File.Exists(path)) { data = File.ReadAllBytes(path); source = path; break; }
                    data = ModAssets.Read("jetpack_loop." + ext);
                    if (data != null) { source = "jetpack_loop." + ext; break; }
                }
                if (data == null)
                {
                    _missing = true;
                    return false;
                }
                var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
                FMOD.MODE mode = FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.OPENMEMORY;
                FMOD.RESULT result = FMODUnity.RuntimeManager.CoreSystem.createSound(data, mode, ref info, out _sound);
                if (result != FMOD.RESULT.OK)
                {
                    throw new Exception($"createSound: {result}");
                }
                _created = true;
                sound = _sound;
                Melon<GearMod>.Logger.Msg($"Jetpack sound: {source}");
            }
            catch (Exception e)
            {
                _missing = true;
                Melon<GearMod>.Logger.Warning($"Could not prepare the jetpack sound: {e.Message}");
            }
            return _created;
        }
    }
}
