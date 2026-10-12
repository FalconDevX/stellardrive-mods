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
    // Pompa wody 1x1: z wysuwanym wężem do jezior na Otrera b. Stan jak drzwi (DoorState: IsOpen = wąż opuszczony),
    // więc E przełącza go tym samym zapytaniem do serwera co drzwi; sygnał 1/0 na wejściu sygnału też.
    // Gniazda: 0 = prąd, 1 = sygnał, 2 = wejście wody (rura), 3 i 4 = wyjścia wody. Logika: Server/WaterPumpServer.
    internal static class WaterPump
    {
        internal const ushort PartId = 214;
        internal const byte PowerSocket = 0, SignalSocket = 1, InSocket = 2, OutSocketA = 3, OutSocketB = 4;

        internal const float FanY = 0.835f;
        internal const float NozzleTop = 0.36f;     // góra głowicy przy zwiniętym wężu (schowana w obudowie)
        internal const float NozzleDepth = 0.26f;   // wysokość głowicy
        internal const float Immersion = 0.3f;      // jak głęboko głowica wchodzi pod wodę
        internal const float HoseSpeed = 1.5f;      // m/s
        internal const float SeaLevel = 64f;        // tafla jezior Otrera b (płaski teren gry na 64 m)
        internal const ulong LakePlanetId = 4;

        private const ushort ChestPartId = 30, GeneratorPartId = 60, DistillerPartId = 80;
        private static PartSettings _part;
        private static bool _failed;
        private static readonly Dictionary<ulong, Planets.Network.TrackedPlanet> Planets = new Dictionary<ulong, Planets.Network.TrackedPlanet>();

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        // Ile węża trzeba wysunąć, żeby głowica sięgnęła powierzchni będącej `above` m pod spodem pompy.
        internal static float HoseNeeded(float above, bool water) => above + NozzleTop - NozzleDepth + (water ? Immersion : 0f);

        internal static float HoseMax => Mathf.Max(1f, MiningMod.PumpHoseLength.Value) + NozzleTop;

        // Punkt w układzie planety (środek planety = 0): ile metrów nad powierzchnią i czy pod spodem jest jezioro.
        internal static bool TryProbe(ulong planetId, Vector3 planetPos, out float above, out bool water)
        {
            above = 0f;
            water = false;
            if (!Planets.TryGetValue(planetId, out Planets.Network.TrackedPlanet tracked) || tracked?.Planet == null)
            {
                Planets[planetId] = tracked = MiningSites.FindTrackedPlanet(planetId); // szukanie po scenie - tylko raz
            }
            if (tracked?.Planet == null)
            {
                return false;
            }
            Vector3 dir = planetPos.normalized;
            float ground = tracked.PointsProvider.GetElevationAtPoint(dir);
            float radius = tracked.Planet.Radius;
            water = planetId == LakePlanetId && ground - radius < SeaLevel + 0.15f;
            above = planetPos.magnitude - (water ? radius + SeaLevel : ground);
            return true;
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
                Melon<MiningMod>.Logger.Msg($"Dodano część '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować pompy: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "WaterPump";
            Mesh body = ModAssets.LoadMesh("pump_body.bin");

            // Wygląd: własny model (korpus, wirnik, wąż, głowica) i własna animacja zamiast wyglądu skrzyni.
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
            PartFactory.CreateRenderer("Body", visual, body, material, lookCopy);
            PartFactory.CreateRenderer("Fan", visual, ModAssets.LoadMesh("pump_fan.bin"), material, lookCopy).transform.localPosition = new Vector3(0f, FanY, 0f);
            PartFactory.CreateRenderer("Hose", visual, ModAssets.LoadMesh("pump_hose.bin"), material, lookCopy).transform.localPosition = new Vector3(0f, NozzleTop, 0f);
            PartFactory.CreateRenderer("Nozzle", visual, ModAssets.LoadMesh("pump_nozzle.bin"), material, lookCopy).transform.localPosition = new Vector3(0f, NozzleTop, 0f);
            Object.DestroyImmediate(lookCopy.gameObject);
            visual.gameObject.AddComponent<WaterPumpVisuals>();

            PartFactory.SetBounds(root, body.bounds);

            // Interakcja: E opuszcza / zwija wąż (zamiast otwierania skrzyni); podnoszenie części zostaje.
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
                // musi wystawać poza kolizję części (inaczej promień trafia raz w nią, raz w bryłę i "F" miga):
                // z przodu i u góry; boki i tył zostają wolne dla gniazd
                box.center = new Vector3(0f, 0.47f, -0.035f);
                box.size = new Vector3(0.86f, 0.96f, 0.97f);
            }

            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            const float y = 0.3f;
            PartFactory.CloneSocket(power, root.transform, PowerSocket, new Vector3(0.15f, y, 0.5f), 180f);
            PartFactory.CloneSocket(fluid, root.transform, InSocket, new Vector3(-0.5f, y, 0f), 90f);
            PartFactory.CloneSocket(fluid, root.transform, OutSocketA, new Vector3(0.5f, y, -0.2f), 270f);
            PartFactory.CloneSocket(fluid, root.transform, OutSocketB, new Vector3(0.5f, y, 0.2f), 270f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(-0.15f, y, 0.5f), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("Nie znaleziono gniazda sygnałowego do skopiowania - pompa bez wejścia sygnału");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningWaterPump";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Water Pump";
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "Pumps water from lakes into pipes. Press E (or send signal 1) to lower the hose - hover over a lake on Otrera b " +
                                   $"(hose {MiningMod.PumpHoseLength.Value:0} m). Also pushes water from a pipe on the input. Needs power. " +
                                   "Sockets: left - water in, right - two water outputs, back - power and signal.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 45f;
            settings.size = Vector3.one;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 10), (112, 6), (Items.Model.ItemsConstants.GlassItemId, 2));
            return settings;
        }
    }

    // Stan pompy dla interakcji (podpis "Lower hose" / "Raise hose").
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

    // Wąż zwisa pionowo w dół (względem planety, nie statku) do tafli albo ziemi; wirnik kręci się, gdy głowica jest w wodzie.
    internal class WaterPumpVisuals : DefaultShipPartVisuals
    {
        private Transform _fan, _hose, _nozzle;
        private Renderer _hoseRenderer;
        private bool _down;
        private float _extension, _target, _nextProbe;
        private bool _inWater, _snap;
        private Vector3 _downDir = Vector3.down;
        private float _pumpLevel, _winchLevel;
        private readonly Audio.LoopEmitter _winchSound = new Audio.LoopEmitter("pump_winch", 30f);
        private readonly Audio.LoopEmitter _pumpSound = new Audio.LoopEmitter("pump", 35f);

        private void Awake()
        {
            _fan = transform.Find("Fan");
            _hose = transform.Find("Hose");
            _nozzle = transform.Find("Nozzle");
            _hoseRenderer = _hose != null ? _hose.GetComponent<Renderer>() : null;
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
                ServiceLocator.GetService<IAudioPlayer>()?.PlayAudioEvent3D(down ? "Event:/Ships/DoorOpen" : "Event:/Ships/DoorClose", gameObject);
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
            Vector3 anchor = transform.TransformPoint(new Vector3(0f, WaterPump.NozzleTop, 0f));
            Quaternion hang = Quaternion.FromToRotation(Vector3.down, _downDir);
            _nozzle.SetPositionAndRotation(anchor + _downDir * _extension, hang);
            _hose.SetPositionAndRotation(anchor, hang);
            _hose.localScale = new Vector3(1f, Mathf.Max(0.001f, _extension), 1f);
            if (_hoseRenderer != null)
            {
                _hoseRenderer.enabled = _extension > 0.05f;
            }
            bool pumping = _down && _inWater && _extension >= _target - 0.05f && _target > 0f;
            // wirnik i dźwięk pompy rozkręcają się i zwalniają płynnie
            _pumpLevel = Mathf.MoveTowards(_pumpLevel, pumping ? 1f : 0f, Time.deltaTime / (pumping ? 1.2f : 0.8f));
            if (_fan != null && _pumpLevel > 0f)
            {
                _fan.Rotate(Vector3.up, 540f * _pumpLevel * Time.deltaTime, Space.Self);
            }
            bool moving = Mathf.Abs(_extension - _target) > 0.01f;
            _winchLevel = Mathf.MoveTowards(_winchLevel, moving ? 1f : 0f, Time.deltaTime / 0.15f);
            Vector3 position = transform.TransformPoint(new Vector3(0f, 0.4f, 0f));
            _winchSound.Update(position, _winchLevel * 0.8f, _target > _extension ? 0.95f : 1.08f); // zwijanie trochę wyżej
            _pumpSound.Update(position, _pumpLevel, 0.6f + 0.4f * _pumpLevel);
        }

        private void OnDisable()
        {
            _winchSound.Stop();
            _pumpSound.Stop();
        }

        private void OnDestroy() => OnDisable();

        private void Probe()
        {
            _inWater = false;
            _downDir = -transform.up;
            float reach = _down ? WaterPump.HoseMax : 0f;
            try
            {
                var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
                if (transpose != null && ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace surface)
                {
                    var planetSpace = new PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Vector3Double.Zero };
                    Vector3 bottom = transform.TransformPoint(Vector3.zero);
                    Vector3 planetPos = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)bottom);
                    _downDir = (transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * -planetPos.normalized).normalized;
                    if (_down && WaterPump.TryProbe(surface.PlanetId, planetPos, out float above, out bool water))
                    {
                        float needed = WaterPump.HoseNeeded(above, water);
                        _inWater = water && needed <= reach;
                        reach = Mathf.Clamp(needed, 0f, reach);
                    }
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Pompa: {e.Message}");
            }
            _target = _down ? reach : 0f;
        }
    }
}
