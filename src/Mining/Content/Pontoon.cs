using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class Pontoon
    {
        internal const ushort PartId = 222, MediumPartId = 251, LargePartId = 252;
        internal const ushort ResearchId = 210;
        internal const float FloatY = 0.62f, FloatRadius = 0.38f, FloatHalfLength = 1.15f;
        internal const byte OxygenSocket = 0;
        internal const float OxygenToInflate = 2f, InflateSeconds = 6f, DeflateSeconds = 8f;

        private const ushort ChestPartId = 30;
        private static readonly (ushort id, string label, int scale, int iron, int wood)[] Sizes =
        {
            (PartId, "Pontoon", 1, 8, 6),
            (MediumPartId, "Pontoon M", 2, 40, 30),
            (LargePartId, "Pontoon L", 3, 120, 80),
        };
        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static IEnumerable<ushort> Ids => Sizes.Select(s => s.id);

        internal static PartSettings Part => Get(PartId);

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static PartSettings[] All => Sizes.Select(s => Get(s.id)).Where(p => p != null).ToArray();

        internal static int Scale(ushort id)
        {
            foreach (var s in Sizes)
            {
                if (s.id == id) return s.scale;
            }
            return 1;
        }

        internal static Vector3 Centre(int n) => new Vector3((n - 1) * 0.5f, 0f, (3 * n - 3) * 0.5f);

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts) => GetOrCreate(PartId, gameParts);

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            PartSettings chest = PartFactory.Find(gameParts, ChestPartId);
            if (chest?.part == null)
            {
                return null;
            }
            var size = Sizes.First(s => s.id == id);
            try
            {
                PartSettings built = Build(chest, size);
                Parts[id] = built;
                Melon<MiningMod>.Logger.Msg($"Added part '{built.fullLabel}' (id {id})");
                return built;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build {size.label}: {e}");
                return null;
            }
        }

        private static Mesh BodyMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.08f, 1.7f), "dark");
            foreach (float z in new[] { -0.6f, 0.6f })
            {
                b.Box(new Vector3(0f, 0.17f, z), new Vector3(0.14f, 0.2f, 0.14f), "steel");
                b.Box(new Vector3(0f, 0.27f, z), new Vector3(0.34f, 0.06f, 0.24f), "grey");
            }
            b.Cylinder(new Vector3(0f, 0.1f, 0.84f), new Vector3(0f, 0.1f, 0.66f), 0.035f, "orange", 12);
            b.Box(new Vector3(0f, 0.04f, 0.855f), new Vector3(0.2f, 0.06f, 0.02f), "steel");
            return b.Build("PontoonBody");
        }

        private static Mesh FloatMesh()
        {
            var b = new MeshBuilder();
            b.Cylinder(new Vector3(0f, 0f, -FloatHalfLength), new Vector3(0f, 0f, FloatHalfLength), FloatRadius, "yellow", 24);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = s * FloatHalfLength;
                b.Cylinder(new Vector3(0f, 0f, z), new Vector3(0f, 0f, z + s * 0.1f), 0.31f, "yellow", 24);
                b.Cylinder(new Vector3(0f, 0f, z + s * 0.1f), new Vector3(0f, 0f, z + s * 0.18f), 0.2f, "yellow_dark", 20);
                b.Cylinder(new Vector3(0f, 0f, s * 0.75f), new Vector3(0f, 0f, s * 0.85f), FloatRadius + 0.012f, "dark", 24);
            }
            b.Box(Vector3.zero, new Vector3(FloatRadius * 2f + 0.03f, 0.05f, 1.2f), "black");
            return b.Build("PontoonFloat");
        }

        private static PartSettings Build(PartSettings chest, (ushort id, string label, int scale, int iron, int wood) size)
        {
            int n = size.scale;
            float volume = n * n * n;
            Vector3 centre = Centre(n);
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = size.label.Replace(" ", "");
            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "Pontoon");
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
            Transform bodyRenderer = PartFactory.CreateRenderer("Body", visual, body, material, lookCopy).transform;
            bodyRenderer.localPosition = centre;
            bodyRenderer.localScale = Vector3.one * n;
            Mesh floatMesh = FloatMesh();
            Transform floatRenderer = PartFactory.CreateRenderer("Float", visual, floatMesh, material, lookCopy).transform;
            floatRenderer.localPosition = centre + new Vector3(0f, FloatY * n, 0f);
            floatRenderer.localScale = Vector3.one * n;
            Object.DestroyImmediate(lookCopy.gameObject);
            visual.gameObject.AddComponent<PontoonVisuals>();
            var bounds = new Bounds(centre + body.bounds.center * n, body.bounds.size * n);
            bounds.Encapsulate(new Bounds(centre + (floatMesh.bounds.center + new Vector3(0f, FloatY, 0f)) * n, floatMesh.bounds.size * n));
            PartFactory.SetBounds(root, bounds);

            Transform interactions = PartFactory.RemoveInteract(root);
            var box = interactions.GetComponentInChildren<BoxCollider>(true);
            if (box != null)
            {
                box.center = bounds.center;
                box.size = bounds.size + new Vector3(0.04f, 0.04f, 0.04f);
            }

            SocketObject gas = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(x => x.Type == CableType.Fluid && x.visualObject != null && x.interactive != null);
            if (gas != null)
            {
                PartFactory.CloneSocket(gas, root.transform, OxygenSocket, centre + new Vector3(0f, 0.04f * n, 0.88f * n), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No fluid socket found to copy - pontoon without an oxygen inlet");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "Mining" + root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = size.id;
            settings.fullLabel = size.label;
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = chest.snappingStyle;
            settings.description = $"Inflatable float landing gear: attach it under the ship (mount plate against the floor). It comes deflated - " +
                                   $"pipe oxygen into the inlet at the end of its mount plate and it takes {OxygenToInflate * volume:0} oxygen and inflates over {InflateSeconds:0} s. " +
                                   $"Air keeps leaking out, so keep the oxygen flowing - it tops the pontoon up; close the valve or cut the supply and it goes flat within about half a minute. " +
                                   $"Inflated, in a lake it holds up about {2000f * volume:0} kg (Water mod setting PontoonLift, times {volume:0} for this size), so with enough of them the ship floats " +
                                   $"on the surface and can sail. Deflated pontoons do not float. Spread them out (e.g. one at each corner) to keep the ship level. " +
                                   $"On land they work as skids. Needs the Water mod.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(20f, 135f, 0f);
            settings.mass = 25f * volume;
            settings.size = new Vector3(n, n, 3f * n);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, size.iron), (Items.Model.ItemsConstants.WoodItemId, size.wood));
            return settings;
        }
    }

    internal class PontoonVisuals : DefaultShipPartVisuals
    {
        private const float Deflated = 0.3f;

        private Transform _float;
        private float _scale = 1f;
        private Vector3 _centre;
        private float _inflation, _target;
        private bool _wasMoving;
        private readonly Audio.LoopEmitter _inflateSound = new Audio.LoopEmitter("pontoon_inflate", 25f, MiningMod.DoorVolume);
        private readonly Audio.LoopEmitter _deflateSound = new Audio.LoopEmitter("pontoon_deflate", 25f, MiningMod.DoorVolume);

        private void Awake()
        {
            _float = transform.Find("Float");
            Transform body = transform.Find("Body");
            if (body != null)
            {
                _scale = Mathf.Max(1f, body.localScale.x);
                _centre = body.localPosition;
            }
            Apply();
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _target = _inflation = state is IDoorState door && door.IsOpen ? 1f : 0f;
            Apply();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            _target = change.NewState is IDoorState door && door.IsOpen ? 1f : 0f;
        }

        private void Update()
        {
            bool moving = !Mathf.Approximately(_inflation, _target);
            bool inflating = moving && _target > _inflation;
            bool deflating = moving && _target < _inflation;
            if (!moving)
            {
                if (_wasMoving)
                {
                    _inflateSound.Stop();
                    _deflateSound.Stop();
                }
                _wasMoving = false;
                return;
            }
            _wasMoving = true;
            _inflation = Mathf.MoveTowards(_inflation, _target, Time.deltaTime / (inflating ? Pontoon.InflateSeconds : Pontoon.DeflateSeconds));
            float edge = Mathf.Clamp01(Mathf.Abs(_target - _inflation) * 8f);
            if (inflating)
            {
                _deflateSound.Stop();
                _inflateSound.Update(transform.position, edge, 0.9f + 0.25f * _inflation);
            }
            else if (deflating)
            {
                _inflateSound.Stop();
                _deflateSound.Update(transform.position, edge * (0.4f + 0.6f * _inflation), 0.8f + 0.3f * _inflation);
            }
            Apply();
        }

        private void OnDisable()
        {
            _inflateSound.Stop();
            _deflateSound.Stop();
            _wasMoving = false;
        }

        private void Apply()
        {
            if (_float == null)
            {
                return;
            }
            float k = Mathf.SmoothStep(0f, 1f, _inflation);
            float wobble = _inflation > 0f && _inflation < 1f ? Mathf.Sin(Time.time * 9f) * 0.03f * (1f - k) : 0f;
            float radial = Mathf.Lerp(Deflated, 1f, k) + wobble;
            float radius = Pontoon.FloatRadius * radial;
            _float.localScale = new Vector3(radial, Mathf.Lerp(Deflated * 0.7f, 1f, k) + wobble, Mathf.Lerp(0.9f, 1f, k)) * _scale;
            _float.localPosition = _centre + new Vector3(0f, Mathf.Lerp(0.3f + radius * 0.7f, Pontoon.FloatY, k) * _scale, 0f);
        }
    }
}
