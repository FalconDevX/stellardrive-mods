using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Constant;
using Ships.Parts.GasMeter;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class GasMeterSignal
    {
        internal const ushort PartId = 260;
        internal const ushort GasMeterPartId = 67;
        internal const byte SignalSocket = 0;
        private const float Overlap = 0.002f;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings constant = parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<ConstantVisuals>(true) != null);
            PartSettings meter = PartFactory.Find(parts, GasMeterPartId);
            if (constant == null || meter?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(constant, meter);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build Gas Meter (Signal): {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings constant, PartSettings meter)
        {
            GameObject root = Object.Instantiate(meter.part, PartFactory.Holder);
            root.name = "MiningGasMeterSignal";
            SocketObject source = PartFactory.FindSocket(constant, CableType.Signal);
            if (source == null)
            {
                throw new Exception("constant has no signal socket");
            }

            GasMeterVisuals visuals = root.GetComponentInChildren<GasMeterVisuals>(true);
            if (visuals == null)
            {
                throw new Exception("gas meter has no visuals");
            }
            TMP_Text screen = Traverse.Create(visuals).Field<TMP_Text>("screen").Value;
            GameObject host = visuals.gameObject;
            Object.DestroyImmediate(visuals);
            host.AddComponent<GasMeterSignalVisuals>().Screen = screen;

            Bounds body = LocalBounds(root);
            SocketObject socket = PartFactory.CloneSocket(source, root.transform, SignalSocket, new Vector3(body.min.x, body.center.y, body.center.z), 90f);
            Bounds plug = LocalBounds(socket.gameObject, root.transform);
            Vector3 p = socket.transform.localPosition;
            p.x += body.min.x + Overlap - plug.max.x;
            socket.transform.localPosition = p;
            Melon<MiningMod>.Logger.Msg($"Gas Meter (Signal): body {body.min} - {body.max}, socket {plug.size} at {p}");

            PartSettings settings = Object.Instantiate(constant);
            settings.size = meter.size;
            settings.snappingStyle = meter.snappingStyle;
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Gas Meter (Signal)";
            settings.description = "Shows the oxygen in the room like the Gas Meter and also sends it as a signal from the socket on its left side, in atmospheres (1 = normal air, 0 = vacuum). " +
                                   "Connect it to an info panel, a math gate or a door to react to the air in the room.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 1), (112, 2));
            return settings;
        }

        private static Bounds LocalBounds(GameObject subtree, Transform space = null)
        {
            space = space ?? subtree.transform;
            bool any = false;
            var bounds = new Bounds();
            bool own = space == subtree.transform;
            foreach (MeshFilter mf in subtree.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<TMP_Text>() != null) continue;
                if (own && mf.GetComponentInParent<SocketObject>() != null) continue;
                Matrix4x4 m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 w = m.MultiplyPoint3x4(c);
                    if (any) bounds.Encapsulate(w);
                    else { bounds = new Bounds(w, Vector3.zero); any = true; }
                }
            }
            if (!any) throw new Exception($"{subtree.name} has no mesh");
            return bounds;
        }
    }

    internal class GasMeterSignalVisuals : DefaultShipPartVisuals
    {
        public TMP_Text Screen;

        public override void SetNetworkState(IStatefulPartState state) => Show(state);

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Show(change.NewState);

        private void Show(IStatefulPartState state)
        {
            if (Screen != null && state is ConstantState constant)
            {
                Screen.text = $"Oxygen {constant.ConstantValue:F} atmo";
            }
        }
    }
}
