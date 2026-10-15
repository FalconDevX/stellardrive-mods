using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Interface.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickBlock.Patches
{
    [HarmonyPatch(typeof(BuildToolLocalClient))]
    internal static class AreaBuildPatch
    {
        private static readonly AccessTools.FieldRef<BuildToolLocalClient, PartPreview> PreviewRef =
            AccessTools.FieldRefAccess<BuildToolLocalClient, PartPreview>("_partPreview");

        private const float PlayerHeight = 1.8f;
        private const float PlayerClearance = 0.95f;

        private static int _size = 1;
        private static bool _active;
        private static LineRenderer _outline;

        internal static int Size => _size;
        internal static bool Showing => _active && _size > 1;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BuildToolLocalClient.ExecuteScrollAction))]
        private static bool Scroll(BuildToolLocalClient __instance, float scroll)
        {
            if (!PickBlockMod.AreaBuild.Value || Keyboard.current == null || !Keyboard.current.altKey.isPressed)
            {
                return true;
            }
            if (!TryGetHull(Placement(__instance), out _, out _))
            {
                return true;
            }
            int max = Mathf.Max(1, PickBlockMod.AreaMaxSize.Value | 1);
            _size = Mathf.Clamp(_size + (scroll > 0f ? 2 : -2), 1, max);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("PlacePart")]
        private static bool Place(SnappedPreviewPlacement snappedPlacement)
        {
            if (!PickBlockMod.AreaBuild.Value || _size <= 1 || !TryGetHull(snappedPlacement, out HullPartPlacement hull, out _))
            {
                return true;
            }
            var service = ServiceLocator.GetService<IShipInteractionNetworkService>();
            if (service == null)
            {
                return true;
            }
            Quaternion ship = snappedPlacement.Rotation * Quaternion.Inverse(hull.Rotation);
            Camera cam = Camera.main;
            foreach (Vector3Int offset in Offsets(hull))
            {
                if (cam != null && HitsPlayer(snappedPlacement.Position + ship * (Vector3)offset, cam.transform.position, ship * Vector3.up))
                {
                    continue;
                }
                var placement = new HullPartPlacement(new HullPartPosition(hull.Coord + offset, hull.Position.Type), hull.ForwardOrientation, hull.UpOrientation);
                service.PlaceHullPart(new PlaceHullPartNetworkRequest
                {
                    ShipId = snappedPlacement.ShipId,
                    SettingsId = snappedPlacement.PartSettings.id,
                    Placement = placement,
                });
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BuildToolLocalClient.UpdateChecks))]
        private static void Update(BuildToolLocalClient __instance)
        {
            PreviewPlacement placement = Placement(__instance);
            _active = PickBlockMod.AreaBuild.Value && TryGetHull(placement, out _, out _);
            if (!_active || _size <= 1)
            {
                Hide();
                return;
            }
            TryGetHull(placement, out HullPartPlacement hull, out _);
            Quaternion ship = placement.Rotation * Quaternion.Inverse(hull.Rotation);
            Axes(hull, out Vector3Int a, out Vector3Int b);
            Vector3 normal = Vector3.Cross(a, b).normalized;
            float half = _size * 0.5f;
            Vector3 center = placement.Position + ship * (normal * 0.03f);
            Vector3 u = ship * (Vector3)a * half, v = ship * (Vector3)b * half;
            LineRenderer line = Outline();
            line.gameObject.SetActive(true);
            line.SetPosition(0, center - u - v);
            line.SetPosition(1, center + u - v);
            line.SetPosition(2, center + u + v);
            line.SetPosition(3, center - u + v);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BuildToolLocalClient.DisableTool))]
        private static void Disable()
        {
            _active = false;
            Hide();
        }

        private static PreviewPlacement Placement(BuildToolLocalClient tool)
        {
            try
            {
                return PreviewRef(tool)?.GetPlacement();
            }
            catch (System.NullReferenceException)
            {
                return null;
            }
        }

        private static bool TryGetHull(PreviewPlacement placement, out HullPartPlacement hull, out VoxelPartType type)
        {
            hull = default;
            type = VoxelPartType.Interior;
            if (!(placement is SnappedPreviewPlacement snapped) || !snapped.HullPartPlacement.HasValue)
            {
                return false;
            }
            hull = snapped.HullPartPlacement.Value;
            type = hull.Position.Type;
            return Axes(hull, out _, out _);
        }

        private static bool HitsPlayer(Vector3 cell, Vector3 head, Vector3 up)
        {
            Vector3 feet = head - up * PlayerHeight;
            Vector3 along = head - feet;
            float t = Mathf.Clamp01(Vector3.Dot(cell - feet, along) / along.sqrMagnitude);
            return (cell - (feet + along * t)).magnitude < PlayerClearance;
        }

        private static bool Axes(HullPartPlacement hull, out Vector3Int a, out Vector3Int b)
        {
            switch (hull.Position.Type)
            {
                case VoxelPartType.ForwardWall:
                    a = Vector3Int.right;
                    b = Vector3Int.up;
                    break;
                case VoxelPartType.RightWall:
                    a = new Vector3Int(0, 0, 1);
                    b = Vector3Int.up;
                    break;
                case VoxelPartType.Floor:
                    a = Vector3Int.right;
                    b = new Vector3Int(0, 0, 1);
                    break;
                default:
                    return Diagonal(hull.ForwardOrientation.GetVector3(), out a, out b);
            }
            return true;
        }

        private static bool Diagonal(Vector3 normal, out Vector3Int a, out Vector3Int b)
        {
            a = b = Vector3Int.zero;
            var n = new Vector3Int(Sign(normal.x), Sign(normal.y), Sign(normal.z));
            int flat = n.x == 0 ? 0 : n.y == 0 ? 1 : n.z == 0 ? 2 : -1;
            if (flat < 0 || Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z) != 2)
            {
                return false;
            }
            a[flat] = 1;
            int i = (flat + 1) % 3, j = (flat + 2) % 3;
            b[i] = n[j];
            b[j] = -n[i];
            return true;
        }

        private static int Sign(float value) => value > 0.1f ? 1 : value < -0.1f ? -1 : 0;

        private static IEnumerable<Vector3Int> Offsets(HullPartPlacement hull)
        {
            Axes(hull, out Vector3Int a, out Vector3Int b);
            int r = _size / 2;
            for (int ring = 0; ring <= r; ring++)
            for (int i = -ring; i <= ring; i++)
            for (int j = -ring; j <= ring; j++)
            {
                if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) == ring)
                {
                    yield return a * i + b * j;
                }
            }
        }

        private static LineRenderer Outline()
        {
            if (_outline != null)
            {
                return _outline;
            }
            var go = new GameObject("AreaBuildOutline");
            Object.DontDestroyOnLoad(go);
            _outline = go.AddComponent<LineRenderer>();
            _outline.loop = true;
            _outline.positionCount = 4;
            _outline.useWorldSpace = true;
            _outline.widthMultiplier = 0.04f;
            _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outline.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "AreaBuildOutline" };
            var color = new Color(0.35f, 0.85f, 1f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            _outline.sharedMaterial = material;
            _outline.startColor = _outline.endColor = color;
            return _outline;
        }

        private static void Hide()
        {
            if (_outline != null && _outline.gameObject.activeSelf)
            {
                _outline.gameObject.SetActive(false);
            }
        }
    }
}
