using HarmonyLib;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PickBlock.Patches
{
    // Budowanie z przytrzymania (jak w Minecrafcie): przytrzymany lewy przycisk z narzędziem budowania stawia część
    // za każdym razem, gdy podgląd przeskoczy na nowe wolne miejsce. Tylko przy dobudowywaniu do statku -
    // na gołej ziemi przytrzymanie tworzyłoby mnóstwo osobnych statków.
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

        // Nie kończymy na PrimaryActionEnd: gra potrafi wysłać je od razu po kliknięciu (przycisk typu "naciśnij"),
        // wtedy przytrzymanie nigdy nie działało. Koniec = puszczony lewy przycisk myszy (sprawdzane w Update).

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

        // To samo miejsce = ta sama pozycja i obrót podglądu (z dokładnością do kilku cm).
        private static string Key(PreviewPlacement placement)
        {
            if (placement == null)
            {
                return null;
            }
            Vector3 p = placement.Position * 20f;
            Vector3 r = placement.Rotation.eulerAngles;
            return $"{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)},{Mathf.RoundToInt(p.z)}|{Mathf.RoundToInt(r.x)},{Mathf.RoundToInt(r.y)},{Mathf.RoundToInt(r.z)}";
        }
    }
}
