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
        internal const ushort PartId = 222;
        internal const ushort ResearchId = 210;
        internal const float FloatY = 0.62f, FloatRadius = 0.38f, FloatHalfLength = 1.15f;
        internal const byte OxygenSocket = 0;
        internal const float OxygenToInflate = 2f, InflateSeconds = 6f;

        private const ushort ChestPartId = 30;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

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
                Melon<MiningMod>.Logger.Error($"Could not build the pontoon: {e}");
            }
            return _part;
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

        private static PartSettings Build(PartSettings chest)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "Pontoon";
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
            PartFactory.CreateRenderer("Body", visual, body, material, lookCopy);
            Mesh floatMesh = FloatMesh();
            PartFactory.CreateRenderer("Float", visual, floatMesh, material, lookCopy).transform.localPosition = new Vector3(0f, FloatY, 0f);
            Object.DestroyImmediate(lookCopy.gameObject);
            visual.gameObject.AddComponent<PontoonVisuals>();
            Bounds bounds = body.bounds;
            bounds.Encapsulate(new Bounds(floatMesh.bounds.center + new Vector3(0f, FloatY, 0f), floatMesh.bounds.size));
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
                PartFactory.CloneSocket(gas, root.transform, OxygenSocket, new Vector3(0f, 0.04f, 0.88f), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No fluid socket found to copy - pontoon without an oxygen inlet");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningPontoon";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Pontoon";
            settings.internalStateClass = "DoorState";
            settings.snappingStyle = chest.snappingStyle;
            settings.description = $"Inflatable float landing gear: attach it under the ship (mount plate against the floor). It comes deflated - " +
                                   $"pipe oxygen into the inlet at the end of its mount plate and it takes {OxygenToInflate:0} oxygen and inflates over {InflateSeconds:0} s. " +
                                   $"Inflated, in a lake each pontoon holds up about 2000 kg (Water mod setting PontoonLift), so with enough of them the ship floats " +
                                   $"on the surface and can sail. Deflated pontoons do not float. Spread them out (e.g. one at each corner) to keep the ship level. " +
                                   $"On land they work as skids. Needs the Water mod.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(20f, 135f, 0f);
            settings.mass = 25f;
            settings.size = new Vector3(1f, 1f, 3f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 8), (Items.Model.ItemsConstants.WoodItemId, 6));
            return settings;
        }
    }

    internal class PontoonVisuals : DefaultShipPartVisuals
    {
        private const float Deflated = 0.3f;

        private Transform _float;
        private float _inflation, _target;
        private bool _wasMoving;

        private void Awake()
        {
            _float = transform.Find("Float");
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
            if (moving && !_wasMoving && _target > _inflation)
            {
                Audio.OneShot.Play("pontoon_inflate", transform.position, 25f, MiningMod.DoorVolume.Value, 1f);
            }
            _wasMoving = moving;
            if (!moving)
            {
                return;
            }
            _inflation = Mathf.MoveTowards(_inflation, _target, Time.deltaTime / Pontoon.InflateSeconds);
            Apply();
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
            _float.localScale = new Vector3(radial, Mathf.Lerp(Deflated * 0.7f, 1f, k) + wobble, Mathf.Lerp(0.9f, 1f, k));
            _float.localPosition = new Vector3(0f, Mathf.Lerp(0.3f + radius * 0.7f, Pontoon.FloatY, k), 0f);
        }
    }
}
