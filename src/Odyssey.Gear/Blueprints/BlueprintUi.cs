using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Managers.Interface.Services;
using MelonLoader;
using UI.Common;
using UI.Interface.Model;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Blueprints
{
    internal static class UiStyle
    {
        internal static GUIStyle Window, Title, Label, Small, Button, Selected, Field, Box;
        private static Texture2D _panel, _button, _buttonHover, _selected, _field;

        internal static float Scale => Mathf.Max(0.6f, Screen.height / 1080f);

        internal static void Ensure()
        {
            if (Window != null && _panel != null)
            {
                return;
            }
            _panel = Solid(new Color(0.05f, 0.1f, 0.16f, 0.96f));
            _button = Solid(new Color(0.12f, 0.24f, 0.36f, 1f));
            _buttonHover = Solid(new Color(0.18f, 0.36f, 0.54f, 1f));
            _selected = Solid(new Color(0.2f, 0.5f, 0.8f, 1f));
            _field = Solid(new Color(0.02f, 0.05f, 0.08f, 1f));
            Window = new GUIStyle { padding = new RectOffset(20, 20, 16, 16) };
            Window.normal.background = _panel;
            Title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            Title.normal.textColor = Color.white;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            Label.normal.textColor = new Color(0.88f, 0.93f, 1f);
            Small = new GUIStyle(Label) { fontSize = 15 };
            Small.normal.textColor = new Color(0.65f, 0.75f, 0.85f);
            Button = new GUIStyle(GUI.skin.button) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 8, 8) };
            Button.normal.background = _button;
            Button.hover.background = _buttonHover;
            Button.active.background = _selected;
            Button.normal.textColor = Button.hover.textColor = Button.active.textColor = Color.white;
            Selected = new GUIStyle(Button);
            Selected.normal.background = Selected.hover.background = _selected;
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 20, padding = new RectOffset(10, 10, 8, 8) };
            Field.normal.background = Field.focused.background = Field.hover.background = _field;
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Color.white;
            Box = new GUIStyle();
            Box.normal.background = _field;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        internal static Rect Centered(float width, float height)
        {
            float s = Scale;
            float w = Mathf.Min(width, Screen.width / s - 40f), h = Mathf.Min(height, Screen.height / s - 40f);
            return new Rect((Screen.width / s - w) / 2f, (Screen.height / s - h) / 2f, w, h);
        }

        internal static string CostText(Dictionary<uint, int> cost) =>
            cost.Count == 0 ? "free" : string.Join(", ", cost.OrderBy(kv => kv.Key).Select(kv => $"{ServerInventory.ItemName(kv.Key)} x{kv.Value}"));
    }

    internal class BlueprintCatalog : MonoBehaviour, IClosableMenu
    {
        private static BlueprintCatalog _instance;
        private Vector2 _scroll;
        private BlueprintEntry _current;
        private string _name = "";
        private bool _confirmDelete;
        private Dictionary<uint, int> _cost;

        internal static bool IsOpen => _instance != null && _instance.enabled;

        internal static bool IsTyping => IsOpen && GUIUtility.keyboardControl != 0;

        internal static void Toggle()
        {
            if (IsOpen)
            {
                _instance.Close();
                return;
            }
            if (_instance == null)
            {
                var go = new GameObject("BlueprintCatalog");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BlueprintCatalog>();
            }
            BlueprintStore.Reload();
            _instance._current = BlueprintStore.Entries.FirstOrDefault();
            _instance.SelectEntry(_instance._current);
            _instance.enabled = true;
            ServiceCache.Get<MenuTracker>()?.TrackMenu(_instance);
        }

        public void Close()
        {
            enabled = false;
            GUIUtility.keyboardControl = 0;
            ServiceCache.Get<MenuTracker>()?.UntrackMenu(this);
        }

        private void SelectEntry(BlueprintEntry entry)
        {
            _current = entry;
            _name = entry?.Data.name ?? "";
            _confirmDelete = false;
            _cost = entry != null ? BlueprintServer.TotalCost(entry.Data) : null;
        }

        private void OnGUI()
        {
            UiStyle.Ensure();
            float s = UiStyle.Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            Rect rect = UiStyle.Centered(1300f, 760f);
            GUILayout.BeginArea(rect, UiStyle.Window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blueprints", UiStyle.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (Esc)", UiStyle.Button, GUILayout.Width(150f))) Close();
            GUILayout.EndHorizontal();
            GUILayout.Label($"Folder: {BlueprintStore.Directory}", UiStyle.Small);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(380f));
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            IReadOnlyList<BlueprintEntry> entries = BlueprintStore.Entries;
            if (entries.Count == 0)
            {
                GUILayout.Label("No blueprints yet. Select an area with the Blueprint Tool: LMB on one corner, LMB on the opposite corner, LMB again to save.", UiStyle.Label);
            }
            foreach (BlueprintEntry e in entries.ToList())
            {
                if (GUILayout.Button($"{e.Data.name}\n<size=14>{e.Data.sizeX}x{e.Data.sizeY}x{e.Data.sizeZ}, {e.Data.PartCount} parts</size>", e == _current ? UiStyle.Selected : UiStyle.Button, GUILayout.Height(62f)))
                {
                    SelectEntry(e);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(20f);
            GUILayout.BeginVertical();
            if (_current != null)
            {
                DrawDetails();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Holograms placed in this world: {BlueprintGhosts.All.Count}. Blueprint Tool: LMB place, scroll rotate, Shift+scroll height, RMB cancel / remove a hologram. Build Tool: LMB on a hologram builds it from your inventory.", UiStyle.Small);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawDetails()
        {
            BlueprintData d = _current.Data;
            GUILayout.BeginHorizontal();
            GUILayout.Box(GUIContent.none, UiStyle.Box, GUILayout.Width(300f), GUILayout.Height(300f));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(GUILayoutUtility.GetLastRect(), _current.Preview, ScaleMode.ScaleToFit);
            }
            GUILayout.Space(16f);
            GUILayout.BeginVertical();
            GUILayout.Label("Name", UiStyle.Small);
            GUI.SetNextControlName("BlueprintName");
            _name = GUILayout.TextField(_name, 60, UiStyle.Field);
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "BlueprintName";
            if (GUILayout.Button("Rename", UiStyle.Button, GUILayout.Width(160f)) || enter)
            {
                BlueprintStore.Rename(_current, _name);
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.Space(10f);
            GUILayout.Label($"Size: {d.sizeX} x {d.sizeY} x {d.sizeZ}", UiStyle.Label);
            GUILayout.Label($"Hull parts: {d.hull.Count}, other parts: {d.parts.Count}, cables: {d.cables.Count}", UiStyle.Label);
            GUILayout.Label($"Materials: {UiStyle.CostText(_cost)}", UiStyle.Label);
            GUILayout.Label($"Saved: {new DateTime(d.created):yyyy-MM-dd HH:mm}", UiStyle.Small);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(14f);
            GUILayout.BeginHorizontal();
            bool placing = BlueprintTool.Selected == _current;
            if (GUILayout.Button(placing ? "Selected for placing" : "Place with the Blueprint Tool", placing ? UiStyle.Selected : UiStyle.Button, GUILayout.Width(320f)))
            {
                BlueprintTool.Select(_current);
                CoreMod.ShowMessage($"'{d.name}' selected - take the Blueprint Tool and aim at a ship or the ground", 5f);
                Close();
            }
            GUILayout.Space(10f);
            if (GUILayout.Button(_confirmDelete ? "Click again to delete" : "Delete", UiStyle.Button, GUILayout.Width(220f)))
            {
                if (_confirmDelete)
                {
                    if (BlueprintTool.Selected == _current) BlueprintTool.Selected = null;
                    BlueprintStore.Delete(_current);
                    SelectEntry(BlueprintStore.Entries.FirstOrDefault());
                }
                else
                {
                    _confirmDelete = true;
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
