using System;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Model;
using Players.LocalClient.Controls.Capture;
using Players.Visuals.Camera;
using Ships;
using Ships.Interface.Services;
using Ships.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PlayerCamerasUpdater), "PlacePilotCamera")]
    internal static class ChaseCameraPatch
    {
        internal static bool Enabled;
        private static int _lastToggleFrame = -1;
        private static bool _failed;

        private static bool Prefix(PlayerState localState, PlayerPilotControlsCapture ____pilotControls, PlayerCamera ____camera, PlayerDistantCamera ____distantCamera,
            IShipsClientProvider ____shipsProvider)
        {
            if (_failed)
            {
                return true;
            }
            try
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.vKey.wasPressedThisFrame && Time.frameCount != _lastToggleFrame && !Diagnostics.DevTools.IsTyping())
                {
                    _lastToggleFrame = Time.frameCount;
                    Enabled = !Enabled;
                    MiningMod.ShowMessage(Enabled ? "Camera: behind the ship (V)" : "Camera: cockpit (V)", 1.5f);
                }
                if (!Enabled || !____shipsProvider.TryGetPilotSeatCameraTransform(localState.EntityId, localState.PartId, out Transform seat) ||
                    !____shipsProvider.TryGetShipVisualTransform(localState.EntityId, out Transform ship))
                {
                    return true;
                }
                var tracker = ServiceCache.Get<ShipsClientTracker>();
                Vector3 center = ship.position;
                float distance = 12f, height = 3f;
                if (tracker != null && tracker.TryGetTrackedShip(localState.EntityId, out TrackedShipClient tracked))
                {
                    var bounds = tracked.GetHullCoordBounds();
                    Vector3 extents = bounds.Extents;
                    center = ship.TransformPoint(bounds.Center);
                    distance = Mathf.Clamp(extents.magnitude * 2.4f + 4f, 8f, 400f);
                    height = extents.y * 0.6f + 2f;
                }
                Quaternion look = seat.rotation * ____pilotControls.CameraRotation;
                Vector3 up = seat.up;
                Vector3 position = center - look * Vector3.forward * distance + up * height;
                Quaternion rotation = Quaternion.LookRotation(center + up * (height * 0.35f) - position, up);
                ____camera.UpdateCamera(position, rotation);
                ____distantCamera.UpdateCamera(position, rotation);
                return false;
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Chase camera disabled: {e}");
                return true;
            }
        }
    }
}
