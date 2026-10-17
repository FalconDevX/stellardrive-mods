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
        private const float OrbitSpeed = 0.15f, MaxPitch = 80f;
        private static int _lastPilotFrame = -1000;
        private static float _yaw, _pitch;
        private static bool _failed;


        internal static bool IsOrbiting(PlayerPilotControlsCapture controls)
        {
            if (!Enabled || Time.frameCount - _lastPilotFrame > 30)
            {
                return false;
            }
            Mouse mouse = Mouse.current;
            return (mouse != null && mouse.rightButton.isPressed) || controls.IsUsingFreeLook;
        }

        internal static void Orbit(Vector2 delta)
        {
            _yaw = Mathf.Repeat(_yaw + delta.x * OrbitSpeed, 360f);
            _pitch = Mathf.Clamp(_pitch - delta.y * OrbitSpeed, -MaxPitch, MaxPitch);
        }

        internal static void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.vKey.wasPressedThisFrame || Diagnostics.DevTools.IsTyping() || Time.frameCount - _lastPilotFrame > 30)
            {
                return;
            }
            Enabled = !Enabled;
            _yaw = 0f;
            _pitch = 0f;
            MiningMod.ShowMessage(Enabled ? "Camera: behind the ship (V) - hold the right mouse button to turn it" : "Camera: cockpit (V)", 2f);
        }

        private static bool Prefix(PlayerState localState, PlayerCamera ____camera, PlayerDistantCamera ____distantCamera,
            IShipsClientProvider ____shipsProvider)
        {
            if (_failed)
            {
                return true;
            }
            try
            {
                _lastPilotFrame = Time.frameCount;
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
                Quaternion look = seat.rotation * Quaternion.Euler(_pitch, _yaw, 0f);
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

    [HarmonyPatch(typeof(PlayerPilotControlsCapture), "UpdateDirectionInput")]
    internal static class ChaseCameraOrbitPatch
    {
        private static readonly System.Reflection.MethodInfo SetRawDirection = AccessTools.PropertySetter(typeof(PlayerPilotControlsCapture), nameof(PlayerPilotControlsCapture.RawDirectionInput));

        private static bool Prefix(PlayerPilotControlsCapture __instance, Vector2 direction, ref Vector2 ____linearDirectionInput)
        {
            if (!ChaseCameraPatch.IsOrbiting(__instance))
            {
                return true;
            }
            ChaseCameraPatch.Orbit(direction);
            SetRawDirection.Invoke(__instance, new object[] { Vector2.zero });
            ____linearDirectionInput = Vector2.zero;
            return false;
        }
    }
}
