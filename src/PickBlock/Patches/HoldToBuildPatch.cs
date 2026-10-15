using HarmonyLib;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickBlock.Patches
{
    [HarmonyPatch(typeof(BuildToolLocalClient))]
    internal static class HoldToBuildPatch
    {
        private static readonly AccessTools.FieldRef<BuildToolLocalClient, PartPreview> PreviewRef =
            AccessTools.FieldRefAccess<BuildToolLocalClient, PartPreview>("_partPreview");
        private static readonly AccessTools.FieldRef<BuildToolLocalClient, BuildToolAnimator> AnimatorRef =
            AccessTools.FieldRefAccess<BuildToolLocalClient, BuildToolAnimator>("_animator");

        private static bool _held;
        private static string _lastKey;
        private static float _nextPlaceTime;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BuildToolLocalClient.PrimaryActionBegin))]
        private static void Begin(BuildToolLocalClient __instance)
        {
            _held = PickBlockMod.HoldToBuild.Value;
            _lastKey = Key(PreviewRef(__instance)?.GetPlacement());
            _nextPlaceTime = Time.time + PickBlockMod.HoldDelay.Value;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BuildToolLocalClient.DisableTool))]
        private static void Disable() => _held = false;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BuildToolLocalClient.UpdateChecks))]
        private static void Update(BuildToolLocalClient __instance)
        {
            if (!_held)
            {
                return;
            }
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            {
                _held = false;
                return;
            }
            if (Time.time < _nextPlaceTime)
            {
                return;
            }
            PreviewPlacement placement = PreviewRef(__instance)?.GetPlacement();
            if (!(placement is SnappedPreviewPlacement))
            {
                return;
            }
            string key = Key(placement);
            if (key == _lastKey)
            {
                return;
            }
            if (Traverse.Create(__instance).Method("TryToPlacePart").GetValue<bool>())
            {
                AnimatorRef(__instance)?.PlayPlacePartAnimation();
                _lastKey = key;
                _nextPlaceTime = Time.time + PickBlockMod.HoldRepeat.Value;
            }
        }

        private static string Key(PreviewPlacement placement)
        {
            if (placement == null)
            {
                return null;
            }
            if (placement is SnappedPreviewPlacement snapped)
            {
                if (snapped.HullPartPlacement.HasValue)
                {
                    var hull = snapped.HullPartPlacement.Value;
                    return $"{snapped.ShipId}|{hull.Coord}|{hull.Position.Type}|{hull.ForwardOrientation}|{hull.UpOrientation}";
                }
                var floating = snapped.FloatingPartPlacement.Value;
                Vector3 c = floating.Coord * 20f;
                return $"{snapped.ShipId}|{Mathf.RoundToInt(c.x)},{Mathf.RoundToInt(c.y)},{Mathf.RoundToInt(c.z)}|{floating.ForwardOrientation}|{floating.UpOrientation}";
            }
            Vector3 p = placement.Position * 20f;
            Vector3 r = placement.Rotation.eulerAngles;
            return $"{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)},{Mathf.RoundToInt(p.z)}|{Mathf.RoundToInt(r.x)},{Mathf.RoundToInt(r.y)},{Mathf.RoundToInt(r.z)}";
        }
    }
}
