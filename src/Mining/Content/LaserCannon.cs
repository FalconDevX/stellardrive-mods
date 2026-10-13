using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Mining.Content
{
    internal static class LaserCannon
    {
        internal const ushort PartId = 221;
        internal const ushort ResearchId = 209;
        internal const byte PowerSocket = 0, SignalSocket = 1;
        internal const float PowerWatts = 6000f;
        internal const float BarrelY = 0.36f;
        internal static readonly Vector3 Muzzle = new Vector3(0f, BarrelY, -1.02f);

        private const ushort ChestPartId = 30, GeneratorPartId = 60;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId);
            if (chest?.part == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chest, generator);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the laser cannon: {e}");
            }
            return _part;
        }

        private static Mesh BodyMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, 0.03f, 0.48f), new Vector3(0.44f, 0.06f, 0.6f), "dark");
            b.Box(new Vector3(0f, 0.1f, 0.48f), new Vector3(0.3f, 0.08f, 0.5f), "grey");
            b.Box(new Vector3(0f, 0.27f, 0.48f), new Vector3(0.56f, 0.3f, 0.9f), "light");
            b.Box(new Vector3(0f, 0.495f, 0.5f), new Vector3(0.38f, 0.15f, 0.8f), "light");
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * 0.235f, 0.495f, 0.5f), new Vector3(0.175f, 0.03f, 0.8f), "grey",
                    Quaternion.Euler(0f, 0f, side > 0f ? 120.96f : 59.04f));
                b.Box(new Vector3(side * 0.283f, 0.3f, 0.36f), new Vector3(0.012f, 0.06f, 0.54f), "black");
                for (int i = 0; i < 3; i++)
                {
                    b.Box(new Vector3(side * 0.29f, 0.27f, 0.7f + i * 0.08f), new Vector3(0.03f, 0.22f, 0.035f), "dark");
                }
            }
            b.Box(new Vector3(0f, 0.572f, 0.5f), new Vector3(0.3f, 0.006f, 0.56f), "black");
            b.Box(new Vector3(0f, 0.31f, 0.95f), new Vector3(0.48f, 0.36f, 0.04f), "dark");
            b.Box(new Vector3(0f, 0.45f, 0.968f), new Vector3(0.2f, 0.1f, 0.01f), "black");
            b.Box(new Vector3(0f, 0.33f, 0.025f), new Vector3(0.46f, 0.38f, 0.03f), "dark");
            b.Cylinder(new Vector3(0f, BarrelY, 0.02f), new Vector3(0f, BarrelY, -0.3f), 0.14f, "grey", 24);
            b.Cylinder(new Vector3(0f, BarrelY, -0.3f), new Vector3(0f, BarrelY, -0.34f), 0.15f, "dark", 24);
            return b.Build("LaserCannonBody");
        }

        private static Mesh FlapMesh(float side)
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(-side * 0.075f, 0.012f, 0f), new Vector3(0.15f, 0.024f, 0.56f), "grey");
            for (int i = 0; i < 3; i++)
            {
                b.Box(new Vector3(-side * 0.075f, 0.026f, -0.16f + i * 0.16f), new Vector3(0.11f, 0.006f, 0.05f), "dark");
            }
            return b.Build("LaserCannonFlap");
        }

        private static Mesh BarrelTubeMesh()
        {
            var b = new MeshBuilder();
            b.Cylinder(new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, -0.8f), 0.055f, "steel", 20);
            return b.Build("LaserCannonBarrel");
        }

        private static Mesh BarrelPartsMesh()
        {
            var b = new MeshBuilder();
            b.Cylinder(new Vector3(0f, 0f, -0.34f), new Vector3(0f, 0f, -0.4f), 0.085f, "dark", 20);
            b.Cylinder(new Vector3(0f, 0f, -0.78f), new Vector3(0f, 0f, -0.86f), 0.09f, "dark", 20);
            foreach (float deg in new[] { 90f, 210f, 330f })
            {
                float r = deg * Mathf.Deg2Rad;
                b.Box(new Vector3(Mathf.Cos(r) * 0.07f, Mathf.Sin(r) * 0.07f, -0.93f), new Vector3(0.03f, 0.03f, 0.14f), "grey", Quaternion.Euler(0f, 0f, deg));
            }
            return b.Build("LaserCannonBarrelParts");
        }

        private static Mesh BarrelGlowMesh()
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                float z = -0.45f - i * 0.08f;
                b.Cylinder(new Vector3(0f, 0f, z), new Vector3(0f, 0f, z - 0.025f), 0.072f, 20);
            }
            b.Cylinder(new Vector3(0f, 0f, -0.86f), new Vector3(0f, 0f, -0.87f), 0.05f, 20);
            return b.Build("LaserCannonGlow");
        }

        private static Mesh LampMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, 0.46f, 0.975f), new Vector3(0.14f, 0.04f, 0.008f));
            b.Box(new Vector3(0f, 0.42f, 0.975f), new Vector3(0.14f, 0.015f, 0.008f));
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * 0.29f, 0.3f, 0.36f), new Vector3(0.006f, 0.022f, 0.5f));
            }
            return b.Build("LaserCannonLamp");
        }

        private static Material Emissive(Material source, string name, Color color, bool keepAlbedo)
        {
            var m = new Material(source) { name = name };
            if (!keepAlbedo)
            {
                m.SetTexture("_Albedo", Texture2D.whiteTexture);
            }
            m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            m.SetColor("_EmissionColor", color);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "LaserCannon";
            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "LaserCannon");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (DefaultShipPartVisuals v in root.GetComponentsInChildren<DefaultShipPartVisuals>(true))
            {
                Object.DestroyImmediate(v);
            }
            Mesh body = BodyMesh();
            PartFactory.CreateRenderer("Body", visual, body, material, lookCopy);
            Bounds bounds = body.bounds;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject flap = PartFactory.CreateRenderer(side > 0f ? "FlapR" : "FlapL", visual, FlapMesh(side), material, lookCopy);
                flap.transform.localPosition = new Vector3(side * 0.16f, 0.575f, 0.5f);
            }
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(visual, false);
            barrel.localPosition = new Vector3(0f, BarrelY, 0f);
            PartFactory.CreateRenderer("Tube", barrel, BarrelTubeMesh(), Emissive(material, "LaserCannonBarrel", Color.black, true), lookCopy);
            PartFactory.CreateRenderer("Parts", barrel, BarrelPartsMesh(), material, lookCopy);
            GameObject glow = PartFactory.CreateRenderer("Glow", barrel, BarrelGlowMesh(), Emissive(material, "LaserCannonGlow", LaserCannonVisuals.IdleGlow, false), lookCopy);
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject lamp = PartFactory.CreateRenderer("Lamp", visual, LampMesh(), Emissive(material, "LaserCannonLamp", LaserCannonVisuals.LampIdle, false), lookCopy);
            lamp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bounds.Encapsulate(new Vector3(0f, BarrelY, -1.0f));
            Object.DestroyImmediate(lookCopy.gameObject);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(visual, false);
            muzzle.localPosition = Muzzle;
            muzzle.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            visual.gameObject.AddComponent<LaserCannonVisuals>();
            PartFactory.SetBounds(root, bounds);

            Transform interactions = PartFactory.RemoveInteract(root);
            var box = interactions.GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                box.center = new Vector3(0f, 0.3f, 0f);
                box.size = new Vector3(0.6f, 0.6f, 1.96f);
            }

            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, new Vector3(0.12f, 0.22f, 0.97f), 180f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(-0.12f, 0.22f, 0.97f), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No signal socket found to copy - laser cannon without a signal input");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningLaserCannon";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Laser Cannon";
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = chest.snappingStyle;
            settings.description = $"Fixed laser cannon. Fires while the signal is above 0, e.g. from the L or P socket of the advanced pilot seat " +
                                   $"(left / right mouse button). Draws {PowerWatts:0} W while firing. Overheats after about {1f / LaserHeat.HeatPerSecond:0} s of continuous fire: " +
                                   $"the vents open and it stops firing until it cools down (lamp on the back: green ready, yellow hot, blinking red overheated). " +
                                   $"Sockets on the back: power (right) and signal (left).";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(20f, 135f, 0f);
            settings.mass = 90f;
            settings.size = new Vector3(1f, 1f, 2f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 20), (112, 10), (Items.Model.ItemsConstants.GlassItemId, 10));
            return settings;
        }
    }

    internal static class LaserHeat
    {
        internal const float HeatPerSecond = 1f / 6f, CoolPerSecond = 0.25f, ResumeBelow = 0.3f;

        internal static void Step(ref float heat, ref bool overheated, bool firing, float dt)
        {
            heat = Mathf.Clamp01(heat + (firing ? HeatPerSecond : -CoolPerSecond) * dt);
            if (heat >= 1f)
            {
                overheated = true;
            }
            else if (overheated && heat <= ResumeBelow)
            {
                overheated = false;
            }
        }
    }

    internal class LaserCannonVisuals : DefaultShipPartVisuals
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Unset = new Color(-1f, -1f, -1f, -1f);
        private Color _glowColor = Unset, _tubeColor = Unset, _lampColor = Unset;

        private static void SetEmission(Material material, ref Color last, Color value)
        {
            if (last == value)
            {
                return;
            }
            last = value;
            material.SetColor(EmissionId, value);
        }

        private const float ShotInterval = 0.16f, Recoil = 0.09f, FlapAngle = 62f, SteamInterval = 0.05f;

        internal static readonly Color IdleGlow = new Color(0.25f, 0.02f, 0.02f);
        internal static readonly Color LampIdle = new Color(0.1f, 0.9f, 0.25f);
        private static readonly Color HotGlow = new Color(6f, 0.8f, 0.6f);
        private static readonly Color LampHot = new Color(2.2f, 1.4f, 0.1f);
        private static readonly Color LampAlarm = new Color(4f, 0.15f, 0.08f);
        private static readonly Color BarrelHot = new Color(2.6f, 0.45f, 0.06f);

        private Transform _muzzle, _barrel, _flapL, _flapR;
        private Material _glow, _tube, _lamp;
        private bool _firing, _overheated;
        private float _cooldown, _flash, _heat, _recoil, _flaps, _steam;
        private Vector3 _lastMuzzle;
        private bool _hasLast;
        private Vector3 _velocity;

        private void Awake()
        {
            _muzzle = transform.Find("Muzzle");
            _barrel = transform.Find("Barrel");
            _flapL = transform.Find("FlapL");
            _flapR = transform.Find("FlapR");
            _glow = _barrel?.Find("Glow")?.GetComponent<Renderer>()?.material;
            _tube = _barrel?.Find("Tube")?.GetComponent<Renderer>()?.material;
            _lamp = transform.Find("Lamp")?.GetComponent<Renderer>()?.material;
        }

        public override void SetNetworkState(IStatefulPartState state) => _firing = state is IDoorState door && door.IsOpen;

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            bool firing = change.NewState is IDoorState door && door.IsOpen;
            if (firing && !_firing)
            {
                _cooldown = 0f;
            }
            _firing = firing;
        }

        private void Update()
        {
            if (_muzzle == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            Vector3 position = _muzzle.position;
            if (_hasLast && dt > 0f)
            {
                Vector3 raw = (position - _lastMuzzle) / dt;
                _velocity = raw.magnitude < 500f ? Vector3.Lerp(_velocity, raw, 0.3f) : Vector3.zero;
            }
            _lastMuzzle = position;
            _hasLast = true;

            bool wasOverheated = _overheated;
            LaserHeat.Step(ref _heat, ref _overheated, _firing, dt);
            if (_firing && _heat < 0.97f)
            {
                _overheated = false;
            }
            if (!_firing && _heat > 0.95f)
            {
                _overheated = true;
            }
            if (_overheated && !wasOverheated)
            {
                Audio.LaserSound.PlayOverheat(position);
            }

            _cooldown -= dt;
            if (_firing && _cooldown <= 0f)
            {
                _cooldown = ShotInterval;
                _flash = 1f;
                _recoil = 1f;
                LaserFx.Fire(position, _muzzle.forward, _velocity);
            }
            _flash = Mathf.MoveTowards(_flash, _firing ? 0.45f : 0f, dt * 4f);
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 6f);
            if (_barrel != null)
            {
                _barrel.localPosition = new Vector3(0f, LaserCannon.BarrelY, Recoil * _recoil * _recoil);
            }

            bool vent = _overheated || (!_firing && _heat > 0.6f);
            _flaps = Mathf.MoveTowards(_flaps, vent ? 1f : 0f, dt * (vent ? 4f : 1.5f));
            float angle = FlapAngle * Mathf.SmoothStep(0f, 1f, _flaps);
            if (_flapR != null) _flapR.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (_flapL != null) _flapL.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (_flaps > 0.5f && _heat > 0.15f)
            {
                _steam -= dt;
                if (_steam <= 0f)
                {
                    _steam = SteamInterval / Mathf.Max(0.3f, _heat);
                    Vector3 vent0 = transform.TransformPoint(new Vector3(Random.Range(-0.08f, 0.08f), 0.58f, Random.Range(0.3f, 0.7f)));
                    LaserFx.Vent(vent0, transform.up, _velocity, _heat);
                }
            }

            if (_glow != null)
            {
                SetEmission(_glow, ref _glowColor, Color.Lerp(IdleGlow, HotGlow, _flash));
            }
            if (_tube != null)
            {
                float t = Mathf.InverseLerp(0.25f, 1f, _heat);
                SetEmission(_tube, ref _tubeColor, BarrelHot * (t * t));
            }
            if (_lamp != null)
            {
                Color lamp = _overheated
                    ? (Mathf.Repeat(Time.time * 3f, 1f) < 0.5f ? LampAlarm : LampAlarm * 0.08f)
                    : Color.Lerp(LampIdle, LampHot, Mathf.InverseLerp(0.4f, 0.8f, _heat));
                SetEmission(_lamp, ref _lampColor, lamp);
            }
        }
    }
}
