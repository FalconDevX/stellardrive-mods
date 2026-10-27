using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Constant;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class PowerMeter
    {
        internal const ushort PartId = 243;
        internal const byte SignalSocket = 0, PowerSocket = 1, SignalSocket2 = 2, PowerSocket2 = 3;
        private const ushort GeneratorPartId = 60;
        private const float Width = 0.25f, Height = 0.25f, Depth = 0.25f, SocketY = 0.125f;
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
            PartSettings generator = PartFactory.Find(parts, GeneratorPartId);
            if (constant == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(constant, generator);
                Melon<LogicMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<LogicMod>.Logger.Error($"Could not build Power Meter: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings constant, PartSettings generator)
        {
            GameObject root = Object.Instantiate(constant.part, PartFactory.Holder);
            root.name = "MiningPowerMeter";
            PartFactory.RemoveInteract(root);

            SocketObject signal = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Signal).OrderBy(s => s.Id).FirstOrDefault();
            if (signal == null)
            {
                throw new Exception("constant has no signal socket");
            }
            Transform visual = (root.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>(true) as Component)?.transform ?? root.transform;
            MeshRenderer look = visual.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<TMP_Text>() == null && r.GetComponentInParent<SocketObject>() == null);
            if (look == null)
            {
                throw new Exception("constant has no renderer to copy");
            }
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "PowerMeter");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true).ToArray())
            {
                if (r.GetComponentInParent<SocketObject>() != null)
                {
                    continue;
                }
                if (r.GetComponent<TMP_Text>() != null)
                {
                    r.gameObject.SetActive(false);
                    continue;
                }
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }

            var body = new MeshBuilder();
            float hw = Width * 0.5f, hd = Depth * 0.5f;
            body.Box(new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width - 0.04f, Height - 0.04f, Depth - 0.04f), "steel");
            body.Box(new Vector3(0f, 0.012f, 0f), new Vector3(Width - 0.02f, 0.024f, Depth - 0.02f), "grey");
            body.Box(new Vector3(0f, Height - 0.012f, 0f), new Vector3(Width - 0.02f, 0.024f, Depth - 0.02f), "grey");
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    body.Box(new Vector3(sx * (hw - 0.017f), Height * 0.5f, sz * (hd - 0.017f)), new Vector3(0.026f, Height - 0.03f, 0.026f), "dark");
                    body.Cylinder(new Vector3(sx * (hw - 0.04f), Height - 0.003f, sz * (hd - 0.04f)), new Vector3(sx * (hw - 0.04f), Height + 0.002f, sz * (hd - 0.04f)), 0.007f, "steel", 6);
                }
            }
            float bezel = Width - 0.09f;
            foreach (float s in new[] { -1f, 1f })
            {
                body.Box(new Vector3(0f, Height + 0.002f, s * (bezel * 0.5f + 0.006f)), new Vector3(bezel + 0.024f, 0.008f, 0.012f), "dark");
                body.Box(new Vector3(s * (bezel * 0.5f + 0.006f), Height + 0.002f, 0f), new Vector3(0.012f, 0.008f, bezel), "dark");
            }
            body.Box(new Vector3(0f, Height - 0.001f, 0f), new Vector3(bezel, 0.004f, bezel), "screen");
            body.Cylinder(new Vector3(hw - 0.04f, Height - 0.002f, 0f), new Vector3(hw - 0.04f, Height + 0.004f, 0f), 0.006f, "green", 8);
            var faces = new[] { (Vector3.left, "dark"), (Vector3.right, "dark"), (Vector3.back, "yellow"), (Vector3.forward, "yellow") };
            foreach ((Vector3 n, string color) in faces)
            {
                Vector3 face = new Vector3(n.x * hw, SocketY, n.z * hd);
                Vector3 side = Vector3.Cross(Vector3.up, n);
                body.Cylinder(face - n * 0.03f, face - n * 0.002f, 0.045f, color);
                body.Cylinder(face - n * 0.025f, face - n * 0.004f, 0.056f, "grey");
                for (int i = -1; i <= 1; i++)
                {
                    body.Box(face - n * 0.019f + side * (i * 0.03f) + Vector3.up * 0.075f, new Vector3(0.014f, 0.014f, 0.014f), "black");
                }
                body.Box(face - n * 0.019f - Vector3.up * 0.078f, new Vector3(Mathf.Abs(side.x) * 0.08f + Mathf.Abs(n.x) * 0.006f, 0.016f, Mathf.Abs(side.z) * 0.08f + Mathf.Abs(n.z) * 0.006f), color == "yellow" ? "yellow" : "white");
            }
            Mesh mesh = body.Build("PowerMeterBody");
            GameObject meter = PartFactory.CreateRenderer("Body", visual, mesh, material, lookCopy);
            Matrix4x4 place = (root.transform.worldToLocalMatrix * visual.localToWorldMatrix).inverse;
            meter.transform.localPosition = place.GetColumn(3);
            meter.transform.localRotation = place.rotation;
            meter.transform.localScale = place.lossyScale;
            Melon<LogicMod>.Logger.Msg($"Power Meter: body under {visual.name}");
            Object.DestroyImmediate(lookCopy.gameObject);

            var bounds = new Bounds(new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width, Height, Depth));
            PartFactory.SetBounds(root, bounds);
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.GetComponentInParent<SocketObject>() == null))
            {
                Transform t = box.transform;
                box.center = t.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = Abs(t.InverseTransformVector(root.transform.TransformVector(bounds.size)));
            }

            Transform parent = signal.transform.parent;
            Quaternion local = Quaternion.Inverse(parent.rotation) * root.transform.rotation;
            signal.transform.localPosition = parent.InverseTransformPoint(root.transform.TransformPoint(new Vector3(-Width * 0.5f, SocketY, 0f)));
            signal.transform.localRotation = local * Quaternion.Euler(0f, 90f, 0f);
            SocketObject signal2 = PartFactory.CloneSocket(signal, parent, SignalSocket2, Vector3.zero, 0f);
            signal2.transform.localPosition = parent.InverseTransformPoint(root.transform.TransformPoint(new Vector3(Width * 0.5f, SocketY, 0f)));
            signal2.transform.localRotation = local * Quaternion.Euler(0f, 270f, 0f);
            Traverse.Create(signal).Field("relayedSockets").SetValue(new[] { signal2 });
            Traverse.Create(signal2).Field("relayedSockets").SetValue(new[] { signal });
            SocketObject powerSource = PartFactory.FindSocket(generator, CableType.Power);
            SocketObject power = PartFactory.CloneSocket(powerSource, parent, PowerSocket, Vector3.zero, 0f);
            power.transform.localPosition = parent.InverseTransformPoint(root.transform.TransformPoint(new Vector3(0f, SocketY, -Depth * 0.5f)));
            power.transform.localRotation = local;
            SocketObject power2 = PartFactory.CloneSocket(powerSource, parent, PowerSocket2, Vector3.zero, 0f);
            power2.transform.localPosition = parent.InverseTransformPoint(root.transform.TransformPoint(new Vector3(0f, SocketY, Depth * 0.5f)));
            power2.transform.localRotation = local * Quaternion.Euler(0f, 180f, 0f);
            Traverse.Create(power).Field("relayedSockets").SetValue(new[] { power2 });
            Traverse.Create(power2).Field("relayedSockets").SetValue(new[] { power });
            Melon<LogicMod>.Logger.Msg($"Power Meter: constant size {constant.size}, snapping {constant.snappingStyle}");

            PartSettings settings = Object.Instantiate(constant);
            settings.size = new Vector3(Width, Height, Depth);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Power Meter";
            settings.description = "A small cube like a valve: two power sockets (yellow, front and back) pass power straight through, two signal sockets (dark, left and right) both send the reading. " +
                                   "It sends the network load in percent: power needed divided by what the generators can give (100 = fully used, above 100 = overloaded). " +
                                   "The meter itself uses no power.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 2), (112, 3));
            return settings;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
