using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class RetractableStairs
    {
        internal const ushort PartId = 236;
        internal const byte SignalSocket = 0;
        internal const float Width = 1f, Length = 2f, Plate = 0.1f, Tread = 0.25f, Front = Length / 2f;
        internal const float Speed = 0.8f, RetractSpeed = 1.2f, MaxRun = 40f;
        private const ushort ChestPartId = 30;
        private const float StepWidth = 0.86f, Side = 0.46f;
        private static PartSettings _part;
        private static bool _failed;
        private static readonly Dictionary<int, Mesh> Settled = new Dictionary<int, Mesh>();
        private static Mesh _tip;
        private static Material _material;

        internal static readonly Dictionary<(uint ship, ushort part), float> Extension = new Dictionary<(uint, ushort), float>();

        internal static PartSettings Part => _part;

        internal static float Run(uint ship, ushort part) => Extension.TryGetValue((ship, part), out float run) ? run : 0f;

        internal static float Surface(float z, float run) => Plate - Tread / 2f - Mathf.Clamp(z - Front, 0f, run);

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            PartSettings chest = PartFactory.Find(gameParts, ChestPartId);
            if (chest?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chest);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the retractable stairs: {e}");
            }
            return _part;
        }

        private static Mesh PlateMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, Plate / 2f, 0f), new Vector3(Width - 0.02f, Plate, Length - 0.02f), "steel");
            b.Box(new Vector3(0f, Plate + 0.003f, Front - 0.07f), new Vector3(Width - 0.06f, 0.006f, 0.1f), "yellow");
            b.Box(new Vector3(0f, Plate + 0.003f, -Front + 0.07f), new Vector3(Width - 0.06f, 0.006f, 0.1f), "yellow");
            for (int i = 0; i < 9; i++)
            {
                b.Box(new Vector3(0f, Plate + 0.002f, -Front + 0.3f + i * 0.175f), new Vector3(Width - 0.16f, 0.004f, 0.03f), "dark");
            }
            b.Box(new Vector3(0f, Plate / 2f, Front - 0.004f), new Vector3(StepWidth + 0.02f, Plate * 0.6f, 0.012f), "dark");
            foreach (float x in new[] { -1f, 1f })
            {
                b.Box(new Vector3(x * (Width / 2f - 0.03f), Plate / 2f, 0f), new Vector3(0.05f, Plate + 0.01f, Length - 0.04f), "grey");
            }
            return b.Build("RetractableStairsPlate");
        }

        private static void Step(MeshBuilder b, int j)
        {
            float front = Front + j * Tread, top = Plate - Tread * (j + 1), center = front + Tread / 2f;
            b.Box(new Vector3(0f, top - 0.025f, center), new Vector3(StepWidth, 0.05f, Tread + 0.01f), "steel");
            b.Box(new Vector3(0f, top - 0.012f, front + 0.025f), new Vector3(StepWidth, 0.026f, 0.05f), "yellow");
            for (int g = 1; g <= 3; g++)
            {
                b.Box(new Vector3(0f, top + 0.002f, front + 0.05f + g * 0.045f), new Vector3(StepWidth - 0.08f, 0.006f, 0.014f), "dark");
            }
            b.Box(new Vector3(0f, top - 0.09f, front + Tread - 0.03f), new Vector3(StepWidth, 0.08f, 0.02f), "dark");
            Quaternion slope = Quaternion.Euler(45f, 0f, 0f);
            foreach (float x in new[] { -Side, Side })
            {
                b.Box(new Vector3(x, top - 0.13f, center), new Vector3(0.06f, 0.2f, Tread * 1.4142f + 0.004f), "grey", slope);
            }
        }

        internal const int FlightSteps = 8;
        internal const float FlightRun = FlightSteps * Tread;

        internal static Mesh SettledMesh(int steps)
        {
            if (Settled.TryGetValue(steps, out Mesh mesh))
            {
                return mesh;
            }
            var b = new MeshBuilder();
            for (int j = 0; j < steps; j++)
            {
                Step(b, j);
            }
            mesh = b.Build("RetractableStairsSteps" + steps);
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Settled[steps] = mesh;
            return mesh;
        }

        internal static Mesh TipMesh()
        {
            if (_tip != null)
            {
                return _tip;
            }
            var b = new MeshBuilder();
            Step(b, 0);
            foreach (float x in new[] { -Side, Side })
            {
                b.Box(new Vector3(x, Plate - Tread - 0.2f, Front + Tread - 0.02f), new Vector3(0.08f, 0.12f, 0.06f), "dark");
            }
            _tip = b.Build("RetractableStairsTip");
            _tip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return _tip;
        }

        internal static Material Material => _material;

        internal static Mesh RampMesh(float run)
        {
            float h = Width / 2f, top = Plate - Tread / 2f;
            var profile = new[]
            {
                new Vector2(Front - 0.05f, top + 0.05f), new Vector2(Front + run, top - run), new Vector2(Front + run, top - run - 0.3f), new Vector2(Front - 0.05f, top - 0.3f),
            };
            var vertices = new List<Vector3>();
            foreach (float x in new[] { -h, h })
            {
                vertices.AddRange(profile.Select(p => new Vector3(x, p.y, p.x)));
            }
            var mesh = new Mesh { name = "RetractableStairsRamp" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static PartSettings Build(PartSettings chest)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "RetractableStairs";
            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            _material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "RetractableStairs");
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
            PartFactory.CreateRenderer("Body", visual, PlateMesh(), _material, lookCopy);
            PartFactory.CreateRenderer("Steps", visual, SettledMesh(0), _material, lookCopy);
            GameObject tip = PartFactory.CreateRenderer("Tip", visual, TipMesh(), _material, lookCopy);
            tip.GetComponent<MeshRenderer>().enabled = false;
            Object.DestroyImmediate(lookCopy.gameObject);
            visual.gameObject.AddComponent<RetractableStairsVisuals>();

            var bounds = new Bounds(new Vector3(0f, Plate / 2f, 0f), new Vector3(Width * 0.98f, Plate, Length * 0.98f));
            PartFactory.SetBounds(root, bounds);
            Transform interactions = PartFactory.RemoveInteract(root);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                box.center = box.transform.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = bounds.size + new Vector3(0.02f, 0.02f, 0.02f);
            }

            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(0f, Plate / 2f, -Front - 0.02f), 0f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No signal socket found to copy - retractable stairs without a signal input");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningRetractableStairs";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Retractable Stairs";
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "A 1 x 2 floor plate with steel stairs folded inside. Signal 1 (socket on the back end) slowly slides the stairs out of the front end, " +
                                   $"down at 45 degrees, until they touch the ground or another obstacle (up to {MaxRun:0} m). Signal 0 pulls them back in. " +
                                   "Put it at the edge of a deck with the front facing out, e.g. as a boarding ramp to the ground.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(20f, 60f, 0f);
            settings.mass = 80f;
            settings.size = new Vector3(Width, 1f, Length);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 20), (112, 4));
            return settings;
        }
    }

    internal class RetractableStairsVisuals : DefaultShipPartVisuals, Ships.Parts.Common.Model.IContextAwarePart
    {
        private static readonly Collider[] Hits = new Collider[32];
        private uint _shipId;
        private ushort _partId;
        private bool _hasContext, _open;
        private float _run;
        private int _shown = -1;
        private Transform _tip;
        private MeshRenderer _tipRenderer;
        private MeshFilter _steps;
        private readonly List<GameObject> _flights = new List<GameObject>();
        private string _ownPrefix;
        private readonly Audio.LoopEmitter _sound = new Audio.LoopEmitter("bigdoor", 30f, MiningMod.DoorVolume);

        public void SetContext(Ships.Parts.Common.Utils.ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _partId = context.Part.PartId;
            _ownPrefix = $"ship {_shipId} ";
            _hasContext = true;
            _run = RetractableStairs.Run(_shipId, _partId);
            Apply();
        }

        private void Awake()
        {
            _tip = transform.Find("Tip");
            _tipRenderer = _tip != null ? _tip.GetComponent<MeshRenderer>() : null;
            _steps = transform.Find("Steps")?.GetComponent<MeshFilter>();
        }

        public override void SetNetworkState(IStatefulPartState state) => _open = state is IDoorState door && door.IsOpen;

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => _open = change.NewState is IDoorState door && door.IsOpen;

        private void Update()
        {
            if (!_hasContext)
            {
                return;
            }
            float before = _run, dt = Time.deltaTime;
            if (_open)
            {
                if (!Blocked(_run))
                {
                    _run = Mathf.Min(RetractableStairs.MaxRun, _run + RetractableStairs.Speed * dt);
                }
                else if (_run > 0f && Blocked(_run - 0.06f))
                {
                    _run = Mathf.Max(0f, _run - RetractableStairs.Speed * dt);
                }
            }
            else
            {
                _run = Mathf.Max(0f, _run - RetractableStairs.RetractSpeed * dt);
            }
            bool moving = !Mathf.Approximately(before, _run);
            if (moving)
            {
                _sound.Update(transform.TransformPoint(new Vector3(0f, 0f, RetractableStairs.Front)), 0.6f, 1.25f);
            }
            else
            {
                _sound.Stop();
            }
            RetractableStairs.Extension[(_shipId, _partId)] = _run;
            if (moving || _shown < 0)
            {
                Apply();
            }
        }

        private bool Blocked(float run)
        {
            if (run <= 0.02f)
            {
                return false;
            }
            float top = RetractableStairs.Plate - run;
            var local = new Vector3(0f, top - 0.14f, RetractableStairs.Front + run - RetractableStairs.Tread / 2f + 0.03f);
            Vector3 half = new Vector3(0.4f, 0.12f, RetractableStairs.Tread / 2f);
            PhysicsScene scene = gameObject.scene.GetPhysicsScene();
            int count = scene.OverlapBox(transform.TransformPoint(local), half, Hits, transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                if (hit == null || hit.CompareTag("Player") || IsOwn(hit.transform)) continue;
                return true;
            }
            return false;
        }

        private bool IsOwn(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t == transform || t.name.StartsWith(_ownPrefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void Apply()
        {
            int settled = Mathf.Clamp(Mathf.FloorToInt(_run / RetractableStairs.Tread), 0, Mathf.CeilToInt(RetractableStairs.MaxRun / RetractableStairs.Tread));
            if (_steps != null && settled != _shown)
            {
                int full = settled / RetractableStairs.FlightSteps;
                while (_flights.Count < full)
                {
                    int k = _flights.Count;
                    var flight = new GameObject("Flight" + k) { layer = _steps.gameObject.layer };
                    flight.transform.SetParent(transform, false);
                    flight.transform.localPosition = new Vector3(0f, -k * RetractableStairs.FlightRun, k * RetractableStairs.FlightRun);
                    flight.AddComponent<MeshFilter>().sharedMesh = RetractableStairs.SettledMesh(RetractableStairs.FlightSteps);
                    var source = _steps.GetComponent<MeshRenderer>();
                    var r = flight.AddComponent<MeshRenderer>();
                    r.sharedMaterial = source.sharedMaterial;
                    r.shadowCastingMode = source.shadowCastingMode;
                    r.renderingLayerMask = source.renderingLayerMask;
                    _flights.Add(flight);
                }
                for (int k = 0; k < _flights.Count; k++)
                {
                    if (_flights[k].activeSelf != k < full) _flights[k].SetActive(k < full);
                }
                _steps.sharedMesh = RetractableStairs.SettledMesh(settled % RetractableStairs.FlightSteps);
                _steps.transform.localPosition = new Vector3(0f, -full * RetractableStairs.FlightRun, full * RetractableStairs.FlightRun);
            }
            _shown = settled;
            if (_tip != null)
            {
                float offset = _run - RetractableStairs.Tread;
                _tip.localPosition = new Vector3(0f, -offset, offset);
                if (_tipRenderer != null) _tipRenderer.enabled = _run > 0.005f;
            }
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy()
        {
            _sound.Stop();
            if (_hasContext) RetractableStairs.Extension.Remove((_shipId, _partId));
        }
    }

    internal sealed class RetractableStairsCollider : MonoBehaviour
    {
        private const float Step = 0.05f;
        private MeshCollider _ramp;
        private Mesh _mesh;
        private float _applied = -1f;
        private uint _shipId;
        private ushort _partId;
        private bool _resolved, _skip;

        private bool Resolve()
        {
            if (_resolved)
            {
                return !_skip;
            }
            var id = GetComponent<Ships.Parts.Common.Model.ShipPartIdentifier>();
            if (id == null)
            {
                return false;
            }
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                string[] words = t.name.Split(' ');
                if (words.Length >= 3 && words[0] == "ship" && words[2] == "colliders" && uint.TryParse(words[1], out _shipId))
                {
                    break;
                }
            }
            _partId = id.Id;
            _resolved = true;
            Collider own = GetComponent<Collider>();
            _skip = own != null && own.attachedRigidbody != null && !own.attachedRigidbody.isKinematic;
            if (!_skip)
            {
                var child = new GameObject("Ramp") { layer = gameObject.layer };
                child.tag = gameObject.tag;
                child.transform.SetParent(transform, false);
                _ramp = child.AddComponent<MeshCollider>();
                _ramp.convex = true;
                _ramp.sharedMaterial = own != null ? own.sharedMaterial : null;
                _ramp.enabled = false;
                child.AddComponent<Patches.StairsCollider>();
            }
            return !_skip;
        }

        private void Update()
        {
            if (!Resolve())
            {
                return;
            }
            float run = RetractableStairs.Run(_shipId, _partId);
            if (run < Step)
            {
                run = 0f;
            }
            if (Mathf.Abs(run - _applied) < Step && !(run == 0f && _applied != 0f))
            {
                return;
            }
            _applied = run;
            Mesh old = _mesh;
            if (run > 0f)
            {
                _mesh = RetractableStairs.RampMesh(run);
                _ramp.sharedMesh = _mesh;
                _ramp.enabled = true;
            }
            else
            {
                _ramp.enabled = false;
                _ramp.sharedMesh = null;
                _mesh = null;
            }
            if (old != null) Destroy(old);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
