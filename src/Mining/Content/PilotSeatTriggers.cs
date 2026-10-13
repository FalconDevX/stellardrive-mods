using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Managers.Interface.Model;
using MelonLoader;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Settings;
using Ships.Interface.Services;
using Ships.Parts.MainControlAdvanced;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class PilotSeatTriggers
    {
        internal const byte LeftSocket = 6, RightSocket = 7;
        internal const byte LeftBit = 1, RightBit = 2;
        private const float Encoding = 4f;

        internal static readonly byte[] EmissionSockets = { 0, 1, 2, 3, 4, 5, LeftSocket, RightSocket };
        internal static readonly Dictionary<(uint ship, ushort part), byte> Buttons = new Dictionary<(uint, ushort), byte>();

        private static bool _extended;

        internal static void Extend(IEnumerable<PartSettings> parts)
        {
            if (_extended)
            {
                return;
            }
            _extended = true;
            try
            {
                PartSettings seat = parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<AdvPilotSeatVisuals>(true) != null);
                if (seat == null)
                {
                    Melon<MiningMod>.Logger.Warning("Advanced pilot seat not found - no L/P signals");
                    return;
                }
                AddSockets(seat);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Could not add L/P sockets to the pilot seat: {e}");
            }
        }

        private static void AddSockets(PartSettings seat)
        {
            Transform root = seat.part.transform;
            List<SocketObject> signals = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Signal).OrderBy(s => s.Id).ToList();
            if (signals.Any(s => s.Id == LeftSocket || s.Id == RightSocket) || signals.Count < 2)
            {
                return;
            }
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 center = bounds != null ? bounds.center : Vector3.zero;
            var log = Melon<MiningMod>.Logger;
            log.Msg($"Pilot seat '{seat.fullLabel}' (id {seat.id}), bounds {center} {bounds?.bounds}");
            foreach (SocketObject s in signals)
            {
                Vector3 p = root.InverseTransformPoint(s.transform.position);
                log.Msg($"  signal socket {s.Id}: {p}, rot {(Quaternion.Inverse(root.rotation) * s.transform.rotation).eulerAngles}, parent {s.transform.parent?.name}, " +
                        $"meshes: {string.Join(", ", s.GetComponentsInChildren<MeshFilter>(true).Select(m => m.name + "/" + m.sharedMesh?.name))}");
            }
            foreach (MeshFilter m in root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.GetComponentInParent<SocketObject>() == null))
            {
                log.Msg($"  mesh {m.name}/{m.sharedMesh?.name} at {root.InverseTransformPoint(m.transform.position)}");
            }

            Vector3 average = signals.Aggregate(Vector3.zero, (a, s) => a + root.InverseTransformPoint(s.transform.position)) / signals.Count;
            Vector3 offset = average - center;
            bool alongX = Mathf.Abs(offset.x) >= Mathf.Abs(offset.z);
            (byte id, SocketObject source, char letter)[] plan = { (LeftSocket, signals[0], 'L'), (RightSocket, signals[1], 'P') };
            foreach (var (id, source, letter) in plan)
            {
                Vector3 local = root.InverseTransformPoint(source.transform.position);
                Vector3 mirrored = alongX ? new Vector3(2f * center.x - local.x, local.y, local.z) : new Vector3(local.x, local.y, 2f * center.z - local.z);
                Quaternion rotation = Quaternion.Inverse(root.rotation) * source.transform.rotation;
                Quaternion flipped = Quaternion.Euler(0f, 180f, 0f) * rotation;
                SocketObject copy = Object.Instantiate(source.gameObject, source.transform.parent).GetComponent<SocketObject>();
                copy.name = $"SignalSocket{id}";
                copy.transform.position = root.TransformPoint(mirrored);
                copy.transform.rotation = root.rotation * flipped;
                Traverse.Create(copy).Field("id").SetValue(id);
                Traverse.Create(copy).Field("relayedSockets").SetValue(new SocketObject[0]);
                ReplaceArrow(copy, letter);
                log.Msg($"  added signal socket {id} ({letter}) at {mirrored}");
            }
        }

        private static void ReplaceArrow(SocketObject socket, char letter)
        {
            Mesh mesh = MeshBuilder.Letter(letter);
            MeshFilter arrow = socket.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(m => (m.name + m.sharedMesh?.name).IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0);
            if (arrow != null)
            {
                arrow.sharedMesh = mesh;
                return;
            }
            MeshRenderer look = socket.GetComponentInChildren<MeshRenderer>(true);
            var go = new GameObject("Letter" + letter);
            go.layer = look != null ? look.gameObject.layer : socket.gameObject.layer;
            go.transform.SetParent(socket.visualObject != null ? socket.visualObject.transform : socket.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            if (look != null)
            {
                renderer.sharedMaterial = look.sharedMaterial;
                renderer.renderingLayerMask = look.renderingLayerMask;
            }
        }

        internal static byte ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return 0;
            }
            byte bits = 0;
            if (mouse.leftButton.isPressed) bits |= LeftBit;
            if (mouse.rightButton.isPressed) bits |= RightBit;
            return bits;
        }

        internal static void Encode(ref FlightControlsNetworkInputs inputs, byte bits)
        {
            inputs.MoveInput.x = Mathf.Clamp(inputs.MoveInput.x, -1f, 1f) + Encoding * bits;
        }

        internal static byte Decode(ref FlightControlsNetworkInputs inputs)
        {
            int bits = Mathf.Clamp(Mathf.RoundToInt(inputs.MoveInput.x / Encoding), 0, 3);
            inputs.MoveInput.x -= Encoding * bits;
            return (byte)bits;
        }
    }

    [HarmonyPatch(typeof(PilotSeatAdvState), nameof(PilotSeatAdvState.EmissionSockets), MethodType.Getter)]
    internal static class PilotSeatEmissionPatch
    {
        private static void Postfix(ref byte[] __result) => __result = PilotSeatTriggers.EmissionSockets;
    }

    [HarmonyPatch(typeof(Managers.Server.Services.ShipInteractionService), nameof(Managers.Server.Services.ShipInteractionService.UpdateFlightControls))]
    internal static class PilotMouseSendPatch
    {
        private static void Prefix(ref FlightControlsNetworkInputs __0)
        {
            PilotSeatTriggers.Encode(ref __0, PilotSeatTriggers.ReadMouse());
        }
    }

    [HarmonyPatch(typeof(Managers.Server.Services.ShipInteractionService), "RpcLogic___UpdateFlightControls_ServerRPC___3161416950")]
    internal static class PilotMouseReceivePatch
    {
        internal static byte Pending;

        private static void Prefix(ref FlightControlsNetworkInputs __0) => Pending = PilotSeatTriggers.Decode(ref __0);

        private static void Postfix() => Pending = 0;
    }

    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.SetFlightInputs))]
    internal static class PilotMouseStatePatch
    {
        private static void Prefix(TrackedShipServer __instance, ushort controlPart)
        {
            if (__instance.State.TryGetPart(controlPart, out var part) && part.State is PilotSeatAdvState)
            {
                PilotSeatTriggers.Buttons[(__instance.Id, controlPart)] = PilotMouseReceivePatch.Pending;
            }
        }
    }

    [HarmonyPatch(typeof(SignalConnectionGroupValues), "GetSocketSignal")]
    internal static class PilotMouseSignalPatch
    {
        private static readonly AccessTools.FieldRef<SignalConnectionGroupValues, IShipsProvider> ShipsRef =
            AccessTools.FieldRefAccess<SignalConnectionGroupValues, IShipsProvider>("_ships");

        private static void Postfix(SignalConnectionGroupValues __instance, EntityPartSocket entityPartSocket, ref double __result)
        {
            byte socket = entityPartSocket.Socket.SocketId;
            if (socket != PilotSeatTriggers.LeftSocket && socket != PilotSeatTriggers.RightSocket)
            {
                return;
            }
            ushort partId = entityPartSocket.Socket.PartId;
            if (!PilotSeatTriggers.Buttons.TryGetValue((entityPartSocket.EntityId, partId), out byte bits))
            {
                return;
            }
            IShipsProvider ships = ShipsRef(__instance);
            if (ships == null || !ships.TryGetShip(entityPartSocket.EntityId, out var ship) || !ship.TryGetStatefulPart(partId, out var part) || !(part.State is PilotSeatAdvState seat))
            {
                return;
            }
            byte bit = socket == PilotSeatTriggers.LeftSocket ? PilotSeatTriggers.LeftBit : PilotSeatTriggers.RightBit;
            __result = seat.IsInUse && (bits & bit) != 0 ? 1.0 : 0.0;
        }
    }
}
