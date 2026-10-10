using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Utils;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Services;
using Research;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Physics.Proxies;
using Tools.Build;
using Tools.Common.Hover;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldTracking.Interface.Values;

namespace PickBlock.Patches
{
    // "Pick block" jak w Minecrafcie: trzymając narzędzie budowania, środkowy przycisk myszy na części statku
    // wybiera tę samą część do budowania. Działa dla ścian/podłóg (hull) i pozostałych części (floating parts).
    // UpdateChecks woła gra co klatkę tylko dla aktywnego narzędzia - mamy tu też dane kamery do raycastu.
    [HarmonyPatch(typeof(BuildToolLocalClient), nameof(BuildToolLocalClient.UpdateChecks))]
    internal static class PickBlockPatch
    {
        private static readonly AccessTools.FieldRef<ShipPartsTargetingProxy, TrackedShipClient> ShipRef =
            AccessTools.FieldRefAccess<ShipPartsTargetingProxy, TrackedShipClient>("_trackedShip");

        private static int _layerMask = -1;
        private static Dictionary<ushort, PartSettings> _parts;

        private static void Postfix(CameraPhysicsData cameraPhysics)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.middleButton.wasPressedThisFrame || Cursor.visible)
            {
                return;
            }
            try
            {
                Pick(cameraPhysics);
            }
            catch (Exception e)
            {
                Melon<PickBlockMod>.Logger.Error("Błąd pobierania części: " + e);
            }
        }

        private static void Pick(CameraPhysicsData cameraPhysics)
        {
            if (_layerMask == -1)
            {
                _layerMask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Ship"));
            }
            if (!ClientScene.PhysicsScene.Raycast(cameraPhysics.CameraPos, cameraPhysics.Direction, out RaycastHit hit,
                    PickBlockMod.Range.Value, _layerMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            PartSettings settings = FindSettings(hit, cameraPhysics.Direction);
            if (settings == null)
            {
                return;
            }
            settings = ToBuildableVariant(settings);

            if (PickBlockMod.RequireUnlocked.Value)
            {
                var research = ServiceLocator.GetService<ResearchClientProxy>();
                if (research != null && !research.IsPartUnlocked(settings))
                {
                    Melon<PickBlockMod>.Logger.Msg($"'{settings.fullLabel}' nie jest jeszcze odblokowane w badaniach");
                    return;
                }
            }

            var selection = ServiceLocator.GetService<ILocalSelectedPartProvider>();
            if (selection == null || selection.CurrentSelectedPartId == settings.id)
            {
                return;
            }
            selection.SelectPart(settings.id);
            Melon<PickBlockMod>.Logger.Msg($"Wybrano: {settings.fullLabel} (id {settings.id})");
        }

        private static PartSettings FindSettings(RaycastHit hit, Vector3 direction)
        {
            Collider collider = hit.collider;
            var proxy = collider.GetComponentInParent<ShipPartsTargetingProxy>();
            TrackedShipClient ship = proxy != null ? ShipRef(proxy) : null;
            if (ship == null)
            {
                return null;
            }

            if (ShipPartsTagDetection.ColliderIsHull(collider))
            {
                // HullHover liczy pozycję trafionej ściany/podłogi dokładnie tak jak recykler i pędzel.
                var hover = new HullHover(hit, direction);
                var position = Traverse.Create(hover).Field("_hullPartPosition").GetValue<HullPartPosition>();
                return ship.State.TryGetHullPartState(position, out var hull) ? GetPart(hull.PartId) : null;
            }

            if (ShipPartsTagDetection.ColliderIsFloatingPart(collider))
            {
                var partId = collider.GetComponentInParent<IShipFloatingPartId>();
                if (partId != null && ship.State.TryGetPart(partId.Id, out StatefulPart part))
                {
                    return part.Settings;
                }
            }
            return null;
        }

        // Ściany mają warianty "obrócone" (pivotedSettings), których nie ma w menu - wybieramy wersję bazową,
        // narzędzie budowania samo przełącza się na obróconą przy dopasowaniu.
        private static PartSettings ToBuildableVariant(PartSettings settings)
        {
            EnsureParts();
            foreach (PartSettings p in _parts.Values)
            {
                if (p is HullPartSettings hull && hull.pivotedSettings == settings && hull != settings)
                {
                    return hull;
                }
            }
            return settings;
        }

        private static PartSettings GetPart(ushort id)
        {
            EnsureParts();
            return _parts.TryGetValue(id, out PartSettings p) ? p : null;
        }

        private static void EnsureParts()
        {
            if (_parts == null || _parts.Count == 0)
            {
                _parts = Resources.FindObjectsOfTypeAll<PartSettings>()
                    .GroupBy(p => p.id)
                    .ToDictionary(g => g.Key, g => g.First());
            }
        }
    }
}
