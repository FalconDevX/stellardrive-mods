using System;
using System.Collections.Generic;
using System.Linq;
using Audio.Interface;
using Core.Services;
using Core.Values;
using HarmonyLib;
using MelonLoader;
using Mining.Sites;
using Players.Interface.Model;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class WaterPump
    {
        internal const ushort PartId = 214;
        internal const byte PowerSocket = 0, SignalSocket = 1, OutSocketB = 2, OutSocketA = 3;

        internal const float FanY = 0.835f;
        private const float SocketY = 0.3f;
        internal const float NozzleTop = 0.36f;
        internal const float NozzleDepth = 0.26f;
        internal const float Immersion = 0.3f;
        internal const float HoseSpeed = 1.5f;
        internal const float SeaLevel = 64f;
        internal const ulong LakePlanetId = 4;

        private const ushort ChestPartId = 30, GeneratorPartId = 60, DistillerPartId = 80;
        private static PartSettings _part;
        private static bool _failed;
        private static readonly Dictionary<ulong, Planets.Network.TrackedPlanet> Planets = new Dictionary<ulong, Planets.Network.TrackedPlanet>();

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && Ids.Contains(settings.id);

        internal static IEnumerable<ushort> Ids => new[] { PartId }.Concat(ScaledVariants.Variants.Where(v => v.Kind == ScaledVariants.Kind.WaterPump).Select(v => v.Id));

        private const float ProbeSpread = 2.5f;

        internal static float HoseNeeded(float above, bool water, float scale) => above + (NozzleTop - NozzleDepth) * scale + (water ? Immersion : 0f);

        internal static float HoseMax(float scale) => Mathf.Max(1f, MiningMod.PumpHoseLength.Value) + NozzleTop * scale;

        internal static float HoseLimit(Ships.Cables.ConnectionsTracker connections, uint shipId, ushort partId, float scale, out bool signalled, out float signal)
        {
            signal = 1f;
            signalled = false;
            var socket = new EntityPartSocket(shipId, partId, SignalSocket);
            if (connections != null && connections.TryGetSignalConnectionGroupWithSocket(socket, out _))
            {
                signalled = true;
                signal = Mathf.Clamp01((float)connections.GetSignalValue(socket));
            }
            return signalled ? signal * Mathf.Max(1f, MiningMod.PumpHoseLength.Value) + NozzleTop * scale : HoseMax(scale);
        }

        internal static bool TryProbe(ulong planetId, Vector3 planetPos, out float above, out bool water)
        {
            above = 0f;
            water = false;
            if (!Planets.TryGetValue(planetId, out Planets.Network.TrackedPlanet tracked) || tracked?.Planet == null)
            {
                tracked = MiningSites.FindTrackedPlanet(planetId);
                if (tracked != null) Planets[planetId] = tracked;
            }
            if (tracked?.Planet == null)
            {
                return false;
            }
            Vector3 dir = planetPos.normalized;
            float ground = tracked.PointsProvider.GetElevationAtPoint(dir);
            float radius = tracked.Planet.Radius;
            water = planetId == LakePlanetId && (ground - radius < SeaLevel + 0.15f || LakeAround(tracked, dir, radius));
            above = planetPos.magnitude - (water ? radius + SeaLevel : ground);
            return true;
        }

        private static bool LakeAround(Planets.Network.TrackedPlanet tracked, Vector3 dir, float radius)
        {
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 forward = Vector3.Cross(dir, side);
            float step = ProbeSpread / (radius + SeaLevel);
            int wet = 0;
            foreach (Vector3 offset in new[] { side, -side, forward, -forward })
            {
                if (tracked.PointsProvider.GetElevationAtPoint((dir + offset * step).normalized) - radius < SeaLevel + 0.15f)
                {
                    wet++;
                }
            }
            return wet >= 2;
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
                _part = Build(chest, generator, distiller, 1);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the pump: {e}");
            }
            return _part;
        }

        internal static PartSettings BuildScaled(IEnumerable<PartSettings> gameParts, int scale)
        {
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId), distiller = PartFactory.Find(parts, DistillerPartId);
            return chest?.part == null || generator?.part == null || distiller?.part == null ? null : Build(chest, generator, distiller, scale);
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller, int n)
        {
            float off = (n - 1) * 0.5f;
            var centre = new Vector3(off, 0f, off);
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "WaterPump";
            Mesh body = ModAssets.LoadMesh("pump_body.bin");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "WaterPump");
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
            void Piece(string name, Mesh mesh, float y)
            {
                Transform t = PartFactory.CreateRenderer(name, visual, mesh, material, lookCopy).transform;
                t.localPosition = centre + new Vector3(0f, y * n, 0f);
                t.localScale = Vector3.one * n;
            }
            Piece("Body", body, 0f);
            Piece("Fan", ModAssets.LoadMesh("pump_fan.bin"), FanY);
            Piece("Hose", ModAssets.LoadMesh("pump_hose.bin"), NozzleTop);
            Piece("Nozzle", ModAssets.LoadMesh("pump_nozzle.bin"), NozzleTop);
            Object.DestroyImmediate(lookCopy.gameObject);
            visual.gameObject.AddComponent<WaterPumpVisuals>();

            PartFactory.SetBounds(root, new Bounds(centre + body.bounds.center * n, body.bounds.size * n));

            Transform interactions = root.transform.Find("Interactions");
            foreach (DefaultShipPartInteractions i in interactions.GetComponentsInChildren<DefaultShipPartInteractions>(true))
            {
                GameObject go = i.gameObject;
                Object.DestroyImmediate(i);
                go.AddComponent<WaterPumpInteractions>();
            }
            foreach (PartInteraction original in interactions.GetComponentsInChildren<PartInteraction>(true).ToArray())
            {
                GameObject go = original.gameObject;
                var fields = Traverse.Create(original);
                bool active = fields.Field<bool>("isActive").Value;
                float distance = fields.Field<float>("detectionDistance").Value;
                List<PartInteractionConfig> configs = fields.Field<List<PartInteractionConfig>>("interactions").Value;
                Object.DestroyImmediate(original);
                var pump = go.AddComponent<WaterPumpInteraction>();
                var copy = Traverse.Create(pump);
                copy.Field("isActive").SetValue(active);
                copy.Field("detectionDistance").SetValue(distance);
                copy.Field("interactions").SetValue(configs ?? new List<PartInteractionConfig>());
            }
            var box = interactions.GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                box.center = centre + new Vector3(0f, 0.47f, -0.035f) * n;
                box.size = new Vector3(0.86f, 0.96f, 0.97f) * n;
            }

            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            float face = body.bounds.max.z * n + 0.02f;
            Vector3 At(float x) => centre + new Vector3(x * n, SocketY * n, 0f) + new Vector3(0f, 0f, face);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, At(-0.33f), 180f).transform.localScale *= n;
            PartFactory.CloneSocket(fluid, root.transform, OutSocketA, At(0.11f), 180f).transform.localScale *= n;
            PartFactory.CloneSocket(fluid, root.transform, OutSocketB, At(0.33f), 180f).transform.localScale *= n;
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, At(-0.11f), 180f).transform.localScale *= n;
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No signal socket found to copy - pump without a signal input");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningWaterPump";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Water Pump";
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "Pumps water from lakes into pipes. Press F to lower the hose over a lake on Otrera b " +
                                   $"(hose {MiningMod.PumpHoseLength.Value:0} m), or connect a signal: its value sets the hose length (1 = full, 0 = up). " +
                                   "Needs power. " +
                                   "All four sockets are on the service panel at the back: power (yellow), signal (white) and two water outputs (blue).";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 45f * n * n;
            settings.size = Vector3.one * n;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 10), (112, 6), (Items.Model.ItemsConstants.GlassItemId, 2));
            return settings;
        }
    }

    internal class WaterPumpInteractions : DefaultShipPartInteractions
    {
        public override void SetNetworkState(IStatefulPartState state) => Set(state);

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Set(change.NewState);

        private void Set(IStatefulPartState state)
        {
            foreach (WaterPumpInteraction i in GetComponentsInChildren<WaterPumpInteraction>(true))
            {
                i.HoseDown = state is IDoorState door && door.IsOpen;
            }
        }
    }

    internal class WaterPumpInteraction : PartInteraction
    {
        internal static readonly UnityEngine.Localization.LocalizedString LowerLabel = new UnityEngine.Localization.LocalizedString();
        internal static readonly UnityEngine.Localization.LocalizedString RaiseLabel = new UnityEngine.Localization.LocalizedString();

        internal bool HoseDown;

        public override IReadOnlyCollection<PartInteractionConfig> Interactions
        {
            get
            {
                var list = new List<PartInteractionConfig> { new PartInteractionConfig { type = InteractionType.Interact, localizedLabel = HoseDown ? RaiseLabel : LowerLabel } };
                list.AddRange(base.Interactions.Where(c => c.type != InteractionType.Interact));
                return list;
            }
        }

        public override bool Interact(InteractionType type)
        {
            if (type != InteractionType.Interact)
            {
                return base.Interact(type);
            }
            var request = new Managers.Interface.Model.PartInteractNetworkRequest { ShipId = PartContext.ShipId, PartId = PartContext.PartId };
            if (HoseDown)
            {
                InteractionService.CloseDoor(request);
            }
            else
            {
                InteractionService.OpenDoor(request);
            }
            return true;
        }
    }

    internal class WaterPumpVisuals : DefaultShipPartVisuals, Ships.Parts.Common.Model.IContextAwarePart
    {
        private uint _shipId;
        private ushort _partId;

        public void SetContext(Ships.Parts.Common.Utils.ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _partId = context.Part.PartId;
        }

        private Transform _fan, _hose, _nozzle;
        private float _scale = 1f;
        private Vector3 _centre;
        private Renderer _hoseRenderer;
        private bool _down;
        private float _extension, _target, _nextProbe;
        private bool _inWater, _snap;
        private Vector3 _downDir = Vector3.down;
        private float _pumpLevel, _bubbleCarry;
        private UnityEngine.VFX.VisualEffect _gas;
        private bool _gasPlaying;
        private readonly Audio.LoopEmitter _pumpSound = new Audio.LoopEmitter("pump", 35f);

        private void Awake()
        {
            _fan = transform.Find("Fan");
            _hose = transform.Find("Hose");
            _nozzle = transform.Find("Nozzle");
            _hoseRenderer = _hose != null ? _hose.GetComponent<Renderer>() : null;
            if (_fan != null)
            {
                _scale = Mathf.Max(1f, _fan.localScale.x);
                _centre = new Vector3(_fan.localPosition.x, 0f, _fan.localPosition.z);
            }
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _down = state is IDoorState door && door.IsOpen;
            _snap = true;
            _nextProbe = 0f;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            bool down = change.NewState is IDoorState door && door.IsOpen;
            if (down != _down)
            {
                ServiceCache.Get<IAudioPlayer>()?.PlayAudioEvent3D(down ? "Event:/Ships/DoorOpen" : "Event:/Ships/DoorClose", gameObject);
            }
            _down = down;
            _nextProbe = 0f;
        }

        private void Update()
        {
            if (_hose == null || _nozzle == null)
            {
                return;
            }
            if (Time.time >= _nextProbe)
            {
                _nextProbe = Time.time + 0.25f;
                Probe();
            }
            _extension = _snap ? _target : Mathf.MoveTowards(_extension, _target, Time.deltaTime * WaterPump.HoseSpeed);
            _snap = false;
            Vector3 anchor = transform.TransformPoint(_centre + new Vector3(0f, WaterPump.NozzleTop * _scale, 0f));
            Quaternion hang = Quaternion.FromToRotation(Vector3.down, _downDir);
            _nozzle.SetPositionAndRotation(anchor + _downDir * _extension, hang);
            _hose.SetPositionAndRotation(anchor, hang);
            _nozzle.localScale = Vector3.one * _scale;
            _hose.localScale = new Vector3(_scale, Mathf.Max(0.001f, _extension), _scale);
            if (_hoseRenderer != null)
            {
                _hoseRenderer.enabled = _extension > 0.05f;
            }
            bool pumping = _down && _inWater && _extension >= _target - 0.05f && _target > 0f;
            _pumpLevel = Mathf.MoveTowards(_pumpLevel, pumping ? 1f : 0f, Time.deltaTime / (pumping ? 1.2f : 0.8f));
            if (_fan != null && _pumpLevel > 0f)
            {
                _fan.Rotate(Vector3.up, 540f * _pumpLevel * Time.deltaTime, Space.Self);
            }
            Vector3 position = transform.TransformPoint(_centre + new Vector3(0f, 0.4f * _scale, 0f));
            _pumpSound.Update(position, _pumpLevel, 0.6f + 0.4f * _pumpLevel);
            UpdateBubbles();
        }

        private void UpdateBubbles()
        {
            bool active = _pumpLevel > 0.05f && _inWater;
            Vector3 intake = _nozzle.position + _downDir * WaterPump.NozzleDepth * _scale;
            Vector3 surface = intake - _downDir * WaterPump.Immersion;
            if (active)
            {
                PumpFx.Emit(intake, surface, -_downDir, _pumpLevel * _scale, Time.deltaTime, ref _bubbleCarry);
            }
            if (_gas == null && active)
            {
                _gas = PumpFx.CreateGas(_nozzle);
            }
            if (_gas == null)
            {
                return;
            }
            _gas.transform.SetPositionAndRotation(intake, Quaternion.FromToRotation(Vector3.up, -_downDir));
            if (active != _gasPlaying)
            {
                _gasPlaying = active;
                if (active) _gas.Play(); else _gas.Stop();
            }
            if (active && _gas.HasFloat("OutputRate"))
            {
                _gas.SetFloat("OutputRate", 0.6f * _pumpLevel);
            }
        }

        private void OnDisable()
        {
            _pumpSound.Stop();
        }

        private void OnDestroy() => OnDisable();

        private void Probe()
        {
            _inWater = false;
            _downDir = -transform.up;
            float reach = _down ? WaterPump.HoseLimit(ServiceCache.Get<Ships.Cables.ConnectionsClientTracker>(), _shipId, _partId, _scale, out _, out _) : 0f;
            try
            {
                var transpose = ServiceCache.Get<IWorldTransposeClient>();
                if (transpose != null && ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace surface)
                {
                    var planetSpace = new PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Vector3Double.Zero };
                    Vector3 bottom = transform.TransformPoint(_centre);
                    Vector3 planetPos = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)bottom);
                    _downDir = (transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * -planetPos.normalized).normalized;
                    if (_down && WaterPump.TryProbe(surface.PlanetId, planetPos, out float above, out bool water))
                    {
                        float needed = WaterPump.HoseNeeded(above, water, _scale);
                        _inWater = water && needed <= reach;
                        reach = Mathf.Clamp(needed, 0f, reach);
                    }
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Pump: {e.Message}");
            }
            _target = _down ? reach : 0f;
        }
    }
}
