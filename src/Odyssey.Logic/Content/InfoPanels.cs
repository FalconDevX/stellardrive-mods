using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Constant;
using Players.Interface.Model;
using Ships.Parts.SignalDisplay;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class InfoPanels
    {
        internal const ushort ResearchId = 211;
        internal const int MaxLines = 4;
        internal static readonly char[] Letters = { 'A', 'B', 'C', 'D' };

        internal sealed class Variant
        {
            public ushort Id;
            public string Label;
            public float Width, Height;
            public int Lines;
            public float Watts;
            public int Iron, Copper, Glass;

            public byte PowerSocket => (byte)Lines;
        }

        internal static readonly Variant[] Variants =
        {
            new Variant { Id = 247, Label = "Info Panel XS", Width = 1f, Height = 0.5f, Lines = 1, Watts = 8f, Iron = 2, Copper = 1, Glass = 1 },
            new Variant { Id = 225, Label = "Info Panel S", Width = 1f, Height = 1f, Lines = 1, Watts = 15f, Iron = 3, Copper = 2, Glass = 2 },
            new Variant { Id = 226, Label = "Info Panel M", Width = 2f, Height = 1f, Lines = 2, Watts = 40f, Iron = 5, Copper = 3, Glass = 4 },
            new Variant { Id = 227, Label = "Info Panel L", Width = 3f, Height = 2f, Lines = 4, Watts = 90f, Iron = 8, Copper = 5, Glass = 8 },
        };

        private const ushort GeneratorPartId = 60;
        private const float Depth = 0.08f;
        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static ushort DisplayPartId { get; private set; }

        internal static IEnumerable<PartSettings> All => Variants.Select(v => Get(v.Id)).Where(p => p != null);

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static Variant Find(ushort id) => Variants.FirstOrDefault(v => v.Id == id);

        internal static bool Is(PartSettings settings) => settings != null && Find(settings.id) != null;

        internal const int MaxText = 200;
        private const char PoweredMark = '\u0001', UnpoweredMark = '\u0002';

        internal static string Encode(bool powered, string text) => (powered ? PoweredMark : UnpoweredMark) + (text ?? "");

        internal static (bool powered, string text) Decode(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return (false, "");
            }
            if (name[0] == PoweredMark || name[0] == UnpoweredMark)
            {
                return (name[0] == PoweredMark, name.Substring(1));
            }
            return (false, name);
        }

        internal static string Clean(string text)
        {
            string clean = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            return clean.Length > MaxText ? clean.Substring(0, MaxText) : clean;
        }

        internal static string[] Labels(string text) => (text ?? "").Split(';').Select(l => l.Trim()).ToArray();

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings display = parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<SignalDisplayVisuals>(true) != null);
            PartSettings generator = PartFactory.Find(parts, GeneratorPartId);
            PartSettings constant = parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<ConstantVisuals>(true) != null);
            if (display == null || generator?.part == null)
            {
                return null;
            }
            DisplayPartId = display.id;
            try
            {
                PartSettings built = Build(display, generator, constant, Find(id));
                Parts[id] = built;
                Melon<LogicMod>.Logger.Msg($"Added part '{built.fullLabel}' (id {id})");
                return built;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<LogicMod>.Logger.Error($"Could not build {Find(id)?.Label}: {e}");
                return null;
            }
        }

        private struct Frame
        {
            public Vector3 Origin;
            public Quaternion Rotation;

            public Vector3 Point(float x, float y, float z) => Origin + Rotation * new Vector3(x, y, z);
        }

        private static void Box(MeshBuilder b, Frame f, float x, float y, float z, float sx, float sy, float sz, string color = null)
        {
            if (color == null)
            {
                b.Box(f.Point(x, y, z), new Vector3(sx, sy, sz), f.Rotation);
            }
            else
            {
                b.Box(f.Point(x, y, z), new Vector3(sx, sy, sz), color, f.Rotation);
            }
        }

        private static PartSettings Build(PartSettings display, PartSettings generator, PartSettings constant, Variant v)
        {
            GameObject root = Object.Instantiate(display.part, PartFactory.Holder);
            root.name = v.Label.Replace(" ", "");
            SignalDisplayVisuals original = root.GetComponentInChildren<SignalDisplayVisuals>(true);
            Transform visual = original.transform;
            TMP_Text text = Traverse.Create(original).Field("displayedText").GetValue<TMP_Text>();
            if (text == null)
            {
                throw new Exception("signal display has no text");
            }
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 textPos = visual.InverseTransformPoint(text.transform.position);
            Quaternion textRot = Quaternion.Inverse(visual.rotation) * text.transform.rotation;
            Vector3 normal = textRot * Vector3.back, up = textRot * Vector3.up;
            float back = 0f;
            if (bounds != null)
            {
                Vector3 c = visual.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                Vector3 h = bounds.bounds * 0.5f;
                back = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    back = Mathf.Min(back, Vector3.Dot(corner - textPos, normal));
                }
            }
            Melon<LogicMod>.Logger.Msg($"{v.Label}: display text at {textPos}, normal {normal}, up {up}, back {back:0.000}, snapping {display.snappingStyle}, size {display.size}");

            MeshRenderer look = visual.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.GetComponent<TMP_Text>() == null && r.GetComponentInParent<SocketObject>() == null);
            if (look == null)
            {
                throw new Exception("signal display has no renderer to copy");
            }
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "InfoPanel");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                if (r.GetComponent<TMP_Text>() != null || r.GetComponentInParent<SocketObject>() != null)
                {
                    continue;
                }
                MeshFilter mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            foreach (DefaultShipPartVisuals old in root.GetComponentsInChildren<DefaultShipPartVisuals>(true))
            {
                Object.DestroyImmediate(old);
            }

            Vector3 widthRoot = root.transform.InverseTransformDirection(visual.TransformDirection(Vector3.Cross(up, normal)));
            Vector3 heightRoot = root.transform.InverseTransformDirection(visual.TransformDirection(up));
            int wAxis = Dominant(widthRoot), hAxis = Dominant(heightRoot);
            Vector3 size = display.size;
            Vector3 shiftRoot = Vector3.zero;
            shiftRoot[wAxis] = (v.Width - size[wAxis]) * 0.5f;
            shiftRoot[hAxis] = (v.Height - size[hAxis]) * 0.5f;
            size[wAxis] = v.Width;
            size[hAxis] = v.Height;
            Vector3 shift = visual.InverseTransformVector(root.transform.TransformVector(shiftRoot));
            var frame = new Frame { Origin = textPos + shift + normal * back, Rotation = Quaternion.LookRotation(normal, up) };

            const float bezel = 0.045f, strip = 0.09f;
            float w = v.Width - 0.04f, hgt = v.Height - 0.04f;
            float stripY = -hgt * 0.5f + strip * 0.5f;
            float screenW = w - bezel * 2f, screenH = hgt - strip - bezel * 2f, screenY = strip * 0.5f;
            var body = new MeshBuilder();
            Box(body, frame, 0f, 0f, 0.01f, w + 0.02f, hgt + 0.02f, 0.02f, "grey");
            Box(body, frame, 0f, 0f, (Depth + 0.011f) * 0.5f, w - 0.004f, hgt - 0.004f, Depth - 0.029f, "dark");
            Box(body, frame, 0f, screenY + screenH * 0.5f + bezel * 0.5f, Depth, w, bezel, 0.02f, "grey");
            Box(body, frame, 0f, screenY - screenH * 0.5f - bezel * 0.5f, Depth, w, bezel, 0.02f, "grey");
            Box(body, frame, -(w - bezel) * 0.5f, screenY, Depth, bezel, screenH, 0.02f, "grey");
            Box(body, frame, (w - bezel) * 0.5f, screenY, Depth, bezel, screenH, 0.02f, "grey");
            Box(body, frame, 0f, stripY, Depth * 0.5f + 0.01f, w - 0.002f, strip, Depth, "steel");
            Box(body, frame, 0f, stripY + strip * 0.5f - 0.006f, Depth + 0.012f, w - 0.02f, 0.012f, 0.004f, "yellow");
            Box(body, frame, w * 0.5f - 0.06f, stripY - 0.005f, Depth + 0.012f, 0.025f, 0.025f, 0.006f, "green");
            int total = v.Lines + 1;
            float spacing = Mathf.Min(0.2f, (w - 0.2f) / total);
            float SocketX(int i) => (i - (total - 1) * 0.5f) * spacing;
            for (int i = 0; i < total; i++)
            {
                Box(body, frame, -SocketX(i), -hgt * 0.5f - 0.004f, Depth * 0.5f + 0.01f, 0.07f, 0.008f, 0.07f, i < v.Lines ? "dark" : "yellow");
            }
            Mesh bodyMesh = body.Build("InfoPanelBody");
            PartFactory.CreateRenderer("Body", visual, bodyMesh, material, lookCopy);

            var screen = new MeshBuilder();
            Box(screen, frame, 0f, screenY, Depth - 0.004f, screenW, screenH, 0.008f);
            var screenMaterial = new Material(material) { name = "InfoPanelScreen" };
            screenMaterial.SetTexture("_Albedo", Texture2D.blackTexture);
            screenMaterial.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            screenMaterial.SetColor("_EmissionColor", InfoPanelVisuals.ScreenOff);
            screenMaterial.EnableKeyword("_EMISSION");
            GameObject screenObject = PartFactory.CreateRenderer("Screen", visual, screen.Build("InfoPanelScreen"), screenMaterial, lookCopy);
            screenObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(lookCopy.gameObject);

            Vector3 scale = text.transform.lossyScale;
            Vector3 visualScale = visual.lossyScale;
            float sx = Mathf.Max(1e-5f, Mathf.Abs(scale.x / visualScale.x)), sy = Mathf.Max(1e-5f, Mathf.Abs(scale.y / visualScale.y));
            float lineH = screenH / v.Lines;
            for (int i = 0; i < v.Lines; i++)
            {
                TMP_Text line = Object.Instantiate(text.gameObject, text.transform.parent).GetComponent<TMP_Text>();
                line.name = "Line" + i;
                float y = screenY + screenH * 0.5f - lineH * (i + 0.5f);
                line.transform.position = visual.TransformPoint(frame.Point(0f, y, Depth + 0.006f));
                line.transform.rotation = visual.rotation * textRot;
                RectTransform rect = line.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                line.margin = Vector4.zero;
                float lineW = screenW * 0.9f - 0.04f, lineTextH = Mathf.Min(lineH * 0.8f, 0.3f);
                rect.sizeDelta = new Vector2(LocalLength(line.transform, visual, frame.Rotation * Vector3.right * lineW, sx, lineW),
                                             LocalLength(line.transform, visual, frame.Rotation * Vector3.up * lineTextH, sy, lineTextH));
                line.enableAutoSizing = true;
                line.fontSizeMin = 0.01f;
                line.fontSizeMax = 1000f;
                line.textWrappingMode = TextWrappingModes.NoWrap;
                line.overflowMode = TextOverflowModes.Truncate;
                line.alignment = TextAlignmentOptions.Midline;
                line.color = InfoPanelVisuals.TextColor;
                line.text = "";
            }
            text.gameObject.SetActive(false);
            Melon<LogicMod>.Logger.Msg($"{v.Label}: text rect {text.transform.parent.Find("Line0")?.GetComponent<RectTransform>()?.rect.size}, screen {screenW:0.00}x{screenH:0.00}");
            AddTextInput(visual, constant, visual.TransformPoint(frame.Point(0f, screenY, Depth + 0.008f)), visual.rotation * textRot, screenW - 0.08f);
            visual.gameObject.AddComponent<InfoPanelVisuals>();
            ReplaceInteractions(root);

            Bounds newBounds = bodyMesh.bounds;
            Vector3 rootCenter = root.transform.InverseTransformPoint(visual.TransformPoint(newBounds.center));
            if (bounds != null)
            {
                bounds.center = rootCenter;
                bounds.bounds = Abs(root.transform.InverseTransformVector(visual.TransformVector(newBounds.size)));
            }
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true).Where(c => c.GetComponentInParent<SocketObject>() == null))
            {
                Transform t = box.transform;
                box.center = t.InverseTransformPoint(visual.TransformPoint(newBounds.center));
                box.size = Abs(t.InverseTransformVector(visual.TransformVector(newBounds.size + new Vector3(0.02f, 0.02f, 0.02f))));
            }

            SocketObject signal = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Signal).OrderBy(s => s.Id).FirstOrDefault();
            if (signal == null)
            {
                throw new Exception("signal display has no signal socket");
            }
            Transform socketParent = signal.transform.parent;
            Quaternion socketRot = Quaternion.Inverse(socketParent.rotation) * visual.rotation * Quaternion.LookRotation(up, normal);
            for (int i = 0; i < total; i++)
            {
                SocketObject socket;
                if (i == 0)
                {
                    socket = signal;
                }
                else if (i < v.Lines)
                {
                    socket = PartFactory.CloneSocket(signal, socketParent, (byte)i, Vector3.zero, 0f);
                }
                else
                {
                    socket = PartFactory.CloneSocket(PartFactory.FindSocket(generator, CableType.Power), socketParent, v.PowerSocket, Vector3.zero, 0f);
                }
                socket.transform.localPosition = socketParent.InverseTransformPoint(visual.TransformPoint(frame.Point(-SocketX(i), -hgt * 0.5f - 0.02f, Depth * 0.5f + 0.01f)));
                socket.transform.localRotation = socketRot;
            }
            Melon<LogicMod>.Logger.Msg($"{v.Label}: width axis {wAxis}, height axis {hAxis}, size {size}, shift {shiftRoot}");

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "Mining" + root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.Id;
            settings.fullLabel = v.Label;
            settings.internalStateClass = "BeaconState";
            settings.snappingStyle = display.snappingStyle;
            string inputs = v.Lines == 1 ? "one signal input (A)" : $"{v.Lines} signal inputs ({string.Join(", ", Letters.Take(v.Lines))})";
            settings.description = $"{v.Width:0.##}x{v.Height:0.##} display panel. Shows the values of its {inputs}, one per line. " +
                                   $"Needs power: {v.Watts:0} W, the screen stays dark without it. Sockets on the bottom edge, so cables come from below: signals A-D from the left, power (yellow mark) on the right. " +
                                   "Press E to write your own text: one label per line, separated by ';' (e.g. 'Fuel;Oxygen'). A line with a signal shows its label and value, a line without a signal shows only the text.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = display.thumbnailRotation;
            settings.mass = 4f * v.Width * v.Height;
            settings.size = size;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.Iron), (112, v.Copper), (Items.Model.ItemsConstants.GlassItemId, v.Glass));
            return settings;
        }

        private static void AddTextInput(Transform visual, PartSettings constant, Vector3 center, Quaternion facing, float width)
        {
            TMP_InputField source = constant?.part.GetComponentInChildren<ConstantVisuals>(true)?.InputField;
            if (source == null)
            {
                Melon<LogicMod>.Logger.Warning("No text input to copy - info panels without editable text");
                return;
            }
            Transform top = source.transform;
            Canvas canvas = source.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas.transform.IsChildOf(constant.part.transform))
            {
                top = canvas.transform;
            }
            var rect = (RectTransform)source.transform;
            float sourceWidth = Mathf.Max(1e-4f, rect.rect.width * Mathf.Abs(source.transform.lossyScale.x));
            Quaternion inner = Quaternion.Inverse(top.rotation) * source.transform.rotation;
            Vector3 innerOffset = Quaternion.Inverse(top.rotation) * (source.transform.TransformPoint(rect.rect.center) - top.position);

            GameObject copy = Object.Instantiate(top.gameObject);
            copy.name = "TextInput";
            copy.transform.SetParent(visual, true);
            float k = width / sourceWidth;
            copy.transform.localScale *= k;
            copy.transform.rotation = facing * Quaternion.Inverse(inner);
            copy.transform.position = center - copy.transform.rotation * (innerOffset * k);
            foreach (TMP_InputField field in copy.GetComponentsInChildren<TMP_InputField>(true))
            {
                field.contentType = TMP_InputField.ContentType.Standard;
                field.lineType = TMP_InputField.LineType.SingleLine;
                field.characterValidation = TMP_InputField.CharacterValidation.None;
                field.characterLimit = MaxText;
                field.text = "";
            }
            Melon<LogicMod>.Logger.Msg($"Info panel text input copied from '{top.name}', scale {k:0.###}");
        }

        private static void ReplaceInteractions(GameObject root)
        {
            Transform interactions = root.transform.Find("Interactions");
            PartInteraction[] found = interactions != null ? interactions.GetComponentsInChildren<PartInteraction>(true) : root.GetComponentsInChildren<PartInteraction>(true);
            if (found.Length == 0)
            {
                Melon<LogicMod>.Logger.Warning("Info panel has no interaction to extend - text editing unavailable");
                return;
            }
            foreach (PartInteraction original in found)
            {
                GameObject go = original.gameObject;
                var fields = Traverse.Create(original);
                bool active = fields.Field<bool>("isActive").Value;
                float distance = fields.Field<float>("detectionDistance").Value;
                List<PartInteractionConfig> configs = fields.Field<List<PartInteractionConfig>>("interactions").Value;
                Object.DestroyImmediate(original);
                var copy = Traverse.Create(go.AddComponent<InfoPanelInteraction>());
                copy.Field("isActive").SetValue(active);
                copy.Field("detectionDistance").SetValue(distance);
                copy.Field("interactions").SetValue(configs ?? new List<PartInteractionConfig>());
            }
        }

        private static float LocalLength(Transform t, Transform visual, Vector3 visualVector, float scale, float length)
        {
            float local = t.InverseTransformVector(visual.TransformVector(visualVector)).magnitude;
            return local > 1e-6f && !float.IsNaN(local) ? local : length / scale;
        }

        private static int Dominant(Vector3 v)
        {
            Vector3 a = Abs(v);
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    internal class InfoPanelInteraction : PartInteraction
    {
        internal static readonly UnityEngine.Localization.LocalizedString EditLabel = new UnityEngine.Localization.LocalizedString();

        public override IReadOnlyCollection<PartInteractionConfig> Interactions
        {
            get
            {
                var list = new List<PartInteractionConfig> { new PartInteractionConfig { type = InteractionType.Interact, localizedLabel = EditLabel } };
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
            InteractionService.StartInteractWithPart(new Managers.Interface.Model.PartInteractNetworkRequest { ShipId = PartContext.ShipId, PartId = PartContext.PartId });
            return true;
        }
    }

    internal class InfoPanelVisuals : DefaultShipPartVisuals, Ships.Parts.Common.Model.IContextAwarePart, Ships.Parts.Common.Model.IPartVisualWritable
    {
        internal static readonly Color ScreenOff = new Color(0.01f, 0.012f, 0.015f);
        internal static readonly Color ScreenOn = new Color(0.03f, 0.12f, 0.16f);
        internal static readonly Color TextColor = new Color(0.55f, 0.95f, 1f);

        private uint _shipId;
        private ushort _partId;
        private bool _powered;
        private float _next;
        private int _lines;
        private TMP_Text[] _texts = new TMP_Text[0];
        private Material _screen;
        private TMP_InputField _input;
        private bool _editing, _shownEditing;
        private string[] _labels = new string[0];

        public TMP_InputField InputField => _input;

        public void SetContext(Ships.Parts.Common.Utils.ShipPartContext context)
        {
            _shipId = context.Part.ShipId;
            _partId = context.Part.PartId;
        }

        private void Awake()
        {
            var list = new List<TMP_Text>();
            for (int i = 0; i < InfoPanels.MaxLines; i++)
            {
                TMP_Text t = GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.name == "Line" + i);
                if (t != null) list.Add(t);
            }
            _texts = list.ToArray();
            _lines = _texts.Length;
            _screen = transform.Find("Screen")?.GetComponent<Renderer>()?.material;
            _input = GetComponentInChildren<TMP_InputField>(true);
            HideInput();
            Apply();
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            Read(state);
            Apply();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            Read(change.NewState);
            Apply();
        }

        private void Read(IStatefulPartState state)
        {
            if (state is BeaconState beacon)
            {
                (bool powered, string text) = InfoPanels.Decode(beacon.Name);
                _powered = powered;
                _editing = beacon.IsInUse;
                _labels = InfoPanels.Labels(text);
                if (_input != null && !_input.isFocused && _input.text != text)
                {
                    _input.text = text;
                }
            }
            else
            {
                _powered = state is IDoorState door && door.IsOpen;
                _editing = false;
            }
        }

        private void HideInput()
        {
            if (_input == null)
            {
                return;
            }
            Transform top = _input.GetComponentInParent<Canvas>(true)?.transform ?? _input.transform;
            if (!top.IsChildOf(transform))
            {
                top = _input.transform;
            }
            (top.GetComponent<CanvasGroup>() ?? top.gameObject.AddComponent<CanvasGroup>()).alpha = 0f;
            foreach (Renderer r in top.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        private void Update()
        {
            bool editing = _input != null && (_editing || _input.isFocused);
            if (editing != _shownEditing)
            {
                _shownEditing = editing;
                Apply();
            }
            if (editing)
            {
                ShowTyping();
                return;
            }
            if (!_powered || Time.time < _next)
            {
                return;
            }
            _next = Time.time + 0.15f;
            var connections = ServiceCache.Get<ConnectionsClientTracker>();
            for (int i = 0; i < _lines; i++)
            {
                var socket = new EntityPartSocket(_shipId, _partId, (byte)i);
                string label = i < _labels.Length ? _labels[i] : "";
                bool wired = connections != null && connections.TryGetSignalGroupId(socket, out _);
                string line;
                if (wired)
                {
                    double value = connections.GetSignalValue(socket);
                    line = $"{(label.Length > 0 ? label : InfoPanels.Letters[i].ToString())}  {value:0.##}";
                }
                else
                {
                    line = label;
                }
                SetLine(i, line);
            }
        }

        private void ShowTyping()
        {
            string text = _input.text ?? "";
            string[] parts = text.Split(';');
            int caret = Mathf.Clamp(_input.isFocused ? _input.caretPosition : text.Length, 0, text.Length);
            int current = text.Substring(0, caret).Count(c => c == ';');
            int first = Mathf.Max(0, current - _lines + 1);
            bool blink = Mathf.Repeat(Time.time, 1f) < 0.5f;
            for (int i = 0; i < _lines; i++)
            {
                int index = first + i;
                string line = index < parts.Length ? parts[index].Trim() : "";
                if (index == current)
                {
                    line += blink ? "_" : " ";
                }
                SetLine(i, line);
            }
        }

        private void SetLine(int i, string line)
        {
            if (_texts[i].text != line)
            {
                _texts[i].text = line;
            }
        }

        private void Apply()
        {
            bool lit = _powered || _shownEditing;
            foreach (TMP_Text t in _texts)
            {
                t.gameObject.SetActive(lit);
            }
            if (_screen != null)
            {
                _screen.SetColor("_EmissionColor", lit ? ScreenOn : ScreenOff);
            }
            _next = 0f;
        }
    }
}
