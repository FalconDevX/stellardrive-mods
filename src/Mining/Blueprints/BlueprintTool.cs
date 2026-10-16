using System;
using System.Linq;
using Core.Services;
using Core.Utils;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Players.Interface.Services;
using Ships;
using Ships.Interface.Services;
using Ships.Services;
using Ships.Interface.Model;
using Tools.Common;
using Tools.Interface.Model;
using Tools.PaintTool;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Mining.Blueprints
{
    internal static class BlueprintToolItem
    {
        internal const uint ItemId = 130;
        internal const uint PaintToolItemId = 3;
        private static ItemSettings _item;

        internal static ItemSettings Item => _item;

        internal static ItemSettings[] EnsureIn(ItemSettings[] items)
        {
            if (items == null || items.Any(i => i != null && i.id == ItemId))
            {
                return items;
            }
            ItemSettings paint = items.FirstOrDefault(i => i != null && i.id == PaintToolItemId);
            if (paint?.toolSettings?.prefab == null)
            {
                return items;
            }
            try
            {
                Create(paint);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Could not create the blueprint tool: {e}");
                return items;
            }
            return items.Concat(new[] { _item }).ToArray();
        }

        private static void ReplaceModel(GameObject prefab)
        {
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }
            Bounds? total = null;
            foreach (MeshRenderer r in renderers)
            {
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null) continue;
                Bounds b = f.sharedMesh.bounds;
                foreach (Vector3 corner in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z) })
                {
                    Vector3 p = prefab.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (total == null) total = new Bounds(p, Vector3.zero);
                    else { Bounds t = total.Value; t.Encapsulate(p); total = t; }
                }
            }
            MeshRenderer template = renderers[0];
            Material material = new Material(template.sharedMaterial) { name = "BlueprintTool" };
            Texture2D albedo = ModAssets.LoadTexture("blueprint_tool_albedo.jpg");
            foreach (string prop in new[] { "_Albedo", "_BaseMap", "_MainTex" })
            {
                if (material.HasProperty(prop)) material.SetTexture(prop, albedo);
            }
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            BlueprintToolModel.Shader = template.sharedMaterial != null ? template.sharedMaterial.shader.name : "-";
            int layer = template.gameObject.layer;
            var shadows = template.shadowCastingMode;
            uint renderingLayers = template.renderingLayerMask;
            foreach (MeshRenderer r in renderers)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (filter != null) Object.DestroyImmediate(filter);
            }
            var model = new GameObject("BlueprintToolModel") { layer = layer };
            model.transform.SetParent(prefab.transform, false);
            Bounds bounds = total ?? new Bounds(Vector3.zero, Vector3.one * 0.3f);
            model.transform.localPosition = bounds.center;
            model.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.05f;
            model.AddComponent<MeshFilter>().sharedMesh = ModAssets.LoadMesh("blueprint_tool_body.bin");
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            renderer.renderingLayerMask = renderingLayers;
        }

        private static void Create(ItemSettings paint)
        {
            if (_item != null)
            {
                return;
            }
            GameObject prefab = Object.Instantiate(paint.toolSettings.prefab, PartFactory.Holder);
            prefab.name = "BlueprintTool";
            PaintTool paintTool = prefab.GetComponent<PaintTool>();
            foreach (MonoBehaviour b in prefab.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b is PaintTool || b is PaintToolKeyTips).ToArray())
            {
                Object.DestroyImmediate(b);
            }
            ReplaceModel(prefab);
            prefab.AddComponent<BlueprintTool>();
            var tool = ScriptableObject.CreateInstance<ToolSettings>();
            tool.name = "BlueprintToolSettings";
            tool.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tool.prefab = prefab;
            _item = Object.Instantiate(paint);
            _item.name = "MiningBlueprintTool";
            _item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _item.id = ItemId;
            _item.itemName = "Blueprint Tool";
            _item.toolSettings = tool;
            Melon<MiningMod>.Logger.Msg($"Added item 'Blueprint Tool' (id {ItemId})");
        }
    }

    internal static class BlueprintToolModel
    {
        internal static string Shader;
    }

    internal class BlueprintTool : MonoBehaviour, IInteractableToolScroll
    {
        private const float Reach = 60f;

        internal static BlueprintEntry Selected;
        private static uint _selectionShip;
        private static Vector3Int? _cornerA, _cornerB;
        private static int _turns, _height;

        private uint _hoverShip;
        private Vector3Int _hoverVoxel;
        private bool _hasHover;
        private BlueprintGhost _preview;
        private BlueprintData _previewData;
        private GameObject _selectionBox;
        private BlueprintTarget _placement;

        public bool BlocksPickInteractions => true;

        public Transform Transform => transform;

        public ToolVisuals Visuals => GetComponentInChildren<ToolVisuals>();

        public IToolAnimator Animator => null;

        internal static void Select(BlueprintEntry entry)
        {
            Selected = entry;
            _cornerA = _cornerB = null;
            _turns = 0;
            _height = 0;
        }

        public void EnableTool()
        {
            enabled = true;
            MiningMod.ShowMessage(Selected != null
                ? $"Placing '{Selected.Data.name}': LMB place, scroll rotate, Shift+scroll height, RMB cancel"
                : "Blueprint Tool: LMB first corner, LMB second corner, LMB again to save. RMB cancel. B opens the catalog", 5f);
        }

        public void DisableTool()
        {
            enabled = false;
            ClearVisuals();
        }

        private void OnDestroy() => ClearVisuals();

        private void ClearVisuals()
        {
            _preview?.Destroy();
            _preview = null;
            _previewData = null;
            if (_selectionBox != null) Destroy(_selectionBox);
            _selectionBox = null;
        }

        public void PrimaryActionBegin()
        {
            try
            {
                if (Selected != null)
                {
                    if (_placement == null)
                    {
                        MiningMod.ShowMessage("Aim at a ship or the ground to place the blueprint", 3f);
                        return;
                    }
                    BlueprintGhosts.Add(Selected.Data, _placement);
                    MiningMod.ShowMessage($"Placed '{Selected.Data.name}' - build it with the Build Tool (LMB on the blue hologram)", 5f);
                    return;
                }
                if (_cornerA.HasValue && _cornerB.HasValue)
                {
                    SaveSelection();
                    return;
                }
                if (!_hasHover)
                {
                    MiningMod.ShowMessage("Aim at a ship to select corners", 3f);
                    return;
                }
                if (!_cornerA.HasValue)
                {
                    _selectionShip = _hoverShip;
                    _cornerA = _hoverVoxel;
                    MiningMod.ShowMessage("First corner set - now the opposite corner", 3f);
                }
                else if (_hoverShip != _selectionShip)
                {
                    MiningMod.ShowMessage("Both corners must be on the same ship", 3f);
                }
                else
                {
                    _cornerB = _hoverVoxel;
                    Vector3Int size = Vector3Int.Max(_cornerA.Value, _cornerB.Value) - Vector3Int.Min(_cornerA.Value, _cornerB.Value) + Vector3Int.one;
                    MiningMod.ShowMessage($"Area {size.x} x {size.y} x {size.z} - LMB again to save, RMB to cancel", 5f);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Blueprint tool: {e}");
            }
        }

        private void SaveSelection()
        {
            if (!ServerInventory.IsHost)
            {
                MiningMod.ShowMessage("Only the host can save blueprints", 4f);
                return;
            }
            BlueprintData data = BlueprintServer.Capture(_selectionShip, _cornerA.Value, _cornerB.Value, out string error);
            if (data == null)
            {
                MiningMod.ShowMessage(error, 4f);
                return;
            }
            data.name = BlueprintStore.NextName();
            BlueprintStore.Save(data);
            _cornerA = _cornerB = null;
            MiningMod.ShowMessage($"Saved '{data.name}' ({data.PartCount} parts, {data.cables.Count} cables). Press B to rename or place it", 6f);
        }

        public void PrimaryActionEnd()
        {
        }

        public void SecondaryActionBegin()
        {
            if (_cornerA.HasValue)
            {
                _cornerA = _cornerB = null;
                MiningMod.ShowMessage("Selection cleared", 2f);
                return;
            }
            Camera cam = Camera.main;
            BlueprintGhost aimed = cam != null ? BlueprintGhosts.Aimed(new Ray(cam.transform.position, cam.transform.forward), Reach) : null;
            if (Selected == null && aimed != null)
            {
                BlueprintGhosts.Remove(aimed);
                MiningMod.ShowMessage($"Removed the hologram of '{aimed.Data.name}'", 3f);
                return;
            }
            if (Selected != null)
            {
                Selected = null;
                ClearVisuals();
                MiningMod.ShowMessage("Placing cancelled", 2f);
            }
        }

        public void TertiaryActionBegin()
        {
        }

        public void ExecuteScrollAction(float scroll)
        {
            if (Selected == null || Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }
            Keyboard kb = Keyboard.current;
            int step = scroll > 0f ? 1 : -1;
            if (kb != null && kb.shiftKey.isPressed)
            {
                _height += step;
            }
            else
            {
                _turns = (_turns + step + 4) % 4;
            }
        }

        public void UpdateChecks(CameraPhysicsData cameraPhysics, float deltaTime)
        {
            try
            {
                Refresh(cameraPhysics);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Blueprint tool update: {e}");
                enabled = false;
            }
        }

        private void Refresh(CameraPhysicsData cameraPhysics)
        {
            _hasHover = false;
            _placement = null;
            bool hit = ClientScene.PhysicsScene.Raycast(cameraPhysics.CameraPos, cameraPhysics.Direction, out RaycastHit info, Reach, RaycastMasks.BuildDetectionLayer, QueryTriggerInteraction.Ignore);
            IShipPartTargeter ship = hit ? info.collider.GetComponentInParent<IShipPartTargeter>() : null;
            Vector3 localPos = Vector3.zero, localNormal = Vector3.up;
            if (ship != null)
            {
                PartHit local = ship.ConvertHitToLocalHit(info);
                localPos = local.Pos;
                localNormal = local.Normal;
                _hoverShip = ship.ShipId;
                _hoverVoxel = Vector3Int.RoundToInt(localPos + localNormal * 0.3f);
                _hasHover = true;
            }

            if (Selected == null)
            {
                _preview?.Destroy();
                _preview = null;
                UpdateSelectionBox();
                return;
            }
            if (_selectionBox != null) Destroy(_selectionBox);
            _selectionBox = null;

            BlueprintData data = Selected.Data;
            if (ship != null)
            {
                Vector3Int anchor = Vector3Int.RoundToInt(localPos + localNormal * 0.5f) + Vector3Int.up * _height;
                _placement = new BlueprintTarget { ShipId = ship.ShipId, Transform = BlueprintTransform.Centered(data, _turns, anchor) };
            }
            else if (hit)
            {
                Vector3 up = ServiceCache.Get<IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
                Vector3 forward = Vector3.ProjectOnPlane(cameraPhysics.Direction, up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
                Quaternion rotation = Quaternion.LookRotation(forward.normalized, up) * Quaternion.Euler(0f, 90f * _turns, 0f);
                _placement = new BlueprintTarget
                {
                    NewShip = true,
                    Transform = BlueprintTransform.Centered(data, 0, Vector3Int.zero),
                    World = new WorldTransform { SpaceId = ClientOrigin.LocalPhysicsSpaceId, Position = info.point + up * (0.55f + _height), Rotation = rotation },
                };
            }
            if (_placement == null)
            {
                _preview?.Destroy();
                _preview = null;
                return;
            }
            if (_preview == null || _previewData != data)
            {
                _preview?.Destroy();
                _preview = new BlueprintGhost { Data = data };
                _previewData = data;
            }
            _preview.Target = _placement;
            _preview.Update();
        }

        private void UpdateSelectionBox()
        {
            if (!_cornerA.HasValue)
            {
                if (_selectionBox != null) Destroy(_selectionBox);
                _selectionBox = null;
                return;
            }
            Vector3Int other = _cornerB ?? (_hasHover && _hoverShip == _selectionShip ? _hoverVoxel : _cornerA.Value);
            var ships = ServiceCache.Get<ShipsClientTracker>();
            if (ships == null || !ships.TryGetTrackedShip(_selectionShip, out TrackedShipClient tracked) || !tracked.IsVisualLoaded || tracked.VisualObject == null)
            {
                return;
            }
            if (_selectionBox == null)
            {
                _selectionBox = new GameObject("BlueprintSelection");
                var builder = new GhostMeshBuilder();
                builder.Box(Vector3.zero, Vector3.one, Quaternion.identity, new Color32(255, 200, 60, 60));
                _selectionBox.AddComponent<MeshFilter>().sharedMesh = builder.Build("BlueprintSelection");
                var r = _selectionBox.AddComponent<MeshRenderer>();
                r.sharedMaterial = GhostMaterials.Material;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            Transform parent = tracked.VisualObject.transform;
            if (_selectionBox.transform.parent != parent) _selectionBox.transform.SetParent(parent, false);
            Vector3Int min = Vector3Int.Min(_cornerA.Value, other), max = Vector3Int.Max(_cornerA.Value, other);
            Vector3 size = max - min + Vector3Int.one;
            _selectionBox.transform.localPosition = (Vector3)(min + max) * 0.5f;
            _selectionBox.transform.localRotation = Quaternion.identity;
            _selectionBox.transform.localScale = size + Vector3.one * 0.04f;
        }
    }
}
