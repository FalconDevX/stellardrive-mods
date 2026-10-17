using System;
using System.Collections.Generic;
using HarmonyLib;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Parts.Common.Model.Reactions;
using Ships.Services;
using Ships.Visuals;
using UnityEngine;

namespace Odyssey.Diagnostics
{
    internal static class RoomCuller
    {
        private const float RefreshSeconds = 0.25f;
        private static readonly HashSet<ushort> GlassParts = new HashSet<ushort> { 7, 8, 31, 32, 33, 34, 35, 36, 37 };
        private static readonly Vector3[] Around =
        {
            Vector3.right * 0.5f, Vector3.left * 0.5f, Vector3.up * 0.5f, Vector3.down * 0.5f, Vector3.forward * 0.5f, Vector3.back * 0.5f,
        };

        private static readonly AccessTools.FieldRef<ShipPartVisuals, Dictionary<ushort, IShipPartVisual>> Parts =
            AccessTools.FieldRefAccess<ShipPartVisuals, Dictionary<ushort, IShipPartVisual>>("_floatingParts");

        private sealed class ShipEntry
        {
            public int HullSignature = -1;
            public readonly HashSet<ushort> GlazedRooms = new HashSet<ushort>();
            public readonly Dictionary<ushort, GameObject> Hidden = new Dictionary<ushort, GameObject>();
            public readonly Dictionary<ushort, (GameObject go, Vector3[] corners)> Shapes = new Dictionary<ushort, (GameObject, Vector3[])>();
            public int Seen;
        }

        private static readonly HashSet<Type> VisualOnly = new HashSet<Type>
        {
            typeof(global::Ships.Parts.Electrolyzer.ElectrolyzerVisuals),
            typeof(global::Ships.Parts.EthanolDistiller.EthanolDistillerVisuals),
            typeof(global::Ships.Parts.FluidPump.FluidPumpVisuals),
            typeof(global::Ships.Parts.FuelTank.FluidTankVisuals),
            typeof(global::Ships.Parts.Gyroscope.GyroscopeVisuals),
            typeof(global::Ships.Parts.WoodPowerGenerator.PowerGeneratorVisuals),
            typeof(global::Ships.Parts.WarpExhaust.WarpExhaustVisuals),
        };

        internal static readonly HashSet<Renderer> HiddenRenderers = new HashSet<Renderer>();
        private static readonly HashSet<Behaviour> Paused = new HashSet<Behaviour>();
        private static readonly List<Behaviour> Behaviours = new List<Behaviour>();
        private static readonly Dictionary<uint, ShipEntry> Ships = new Dictionary<uint, ShipEntry>();
        private static readonly List<Renderer> Buffer = new List<Renderer>();
        private static readonly List<ushort> Shown = new List<ushort>();
        private static readonly List<uint> Gone = new List<uint>();
        private static float _next;
        private static int _pass;

        internal static void Update()
        {
            if (!TweaksMod.HideSealedRooms.Value)
            {
                if (Ships.Count > 0) Reset();
                return;
            }
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + RefreshSeconds;
            Camera cam = Camera.main;
            var tracker = ServiceCache.Get<ShipsClientTracker>();
            if (cam == null || tracker == null) return;
            _pass++;
            Vector3 eye = cam.transform.position;
            foreach (TrackedShipClient ship in tracker.VisuallyLoadedShip)
            {
                if (ship == null || ship.VisualObject == null) continue;
                if (!Ships.TryGetValue(ship.Id, out ShipEntry entry))
                {
                    Ships[ship.Id] = entry = new ShipEntry();
                }
                entry.Seen = _pass;
                UpdateShip(ship, entry, eye);
            }
            foreach (var pair in Ships)
            {
                if (pair.Value.Seen != _pass) Gone.Add(pair.Key);
            }
            foreach (uint id in Gone)
            {
                ShowAll(Ships[id]);
                Ships.Remove(id);
            }
            Gone.Clear();
        }

        private static void UpdateShip(TrackedShipClient ship, ShipEntry entry, Vector3 eye)
        {
            ShipPartVisuals visuals = ship.VisualObject;
            IReadOnlyDictionary<HullPartPosition, HullPartNetworkedState> hull = ship.State.GetHullParts();
            int signature = hull.Count * 31 + ship.PartCount;
            if (signature != entry.HullSignature)
            {
                entry.HullSignature = signature;
                FindGlazedRooms(ship, hull, entry.GlazedRooms);
            }
            RoomSampleResult camera = ship.SampleRoomAtPosition(visuals.transform.InverseTransformPoint(eye));
            ushort cameraRoom = camera.IsRoom && !camera.IsWall ? camera.RoomId : (ushort)0;
            foreach (var pair in Parts(visuals))
            {
                GameObject go = pair.Value?.GameObject;
                if (go == null) continue;
                RoomSampleResult room = ship.SampleRoomAtPosition(go.transform.localPosition);
                bool hide = room.IsRoom && !room.IsWall && room.RoomId != cameraRoom && !entry.GlazedRooms.Contains(room.RoomId)
                    && InsideRoom(ship, entry, visuals.transform, pair.Key, go, room.RoomId);
                bool hidden = entry.Hidden.TryGetValue(pair.Key, out GameObject was) && was == go;
                if (hide && !hidden)
                {
                    SetHidden(go, true);
                    entry.Hidden[pair.Key] = go;
                }
                else if (!hide && hidden)
                {
                    SetHidden(go, false);
                    entry.Hidden.Remove(pair.Key);
                }
            }
            foreach (var pair in entry.Hidden)
            {
                if (pair.Value == null || !Parts(visuals).TryGetValue(pair.Key, out IShipPartVisual v) || v?.GameObject != pair.Value) Shown.Add(pair.Key);
            }
            foreach (ushort id in Shown)
            {
                if (entry.Hidden[id] != null) SetHidden(entry.Hidden[id], false);
                entry.Hidden.Remove(id);
            }
            Shown.Clear();
        }

        private static bool InsideRoom(TrackedShipClient ship, ShipEntry entry, Transform shipRoot, ushort id, GameObject go, ushort roomId)
        {
            if (!entry.Shapes.TryGetValue(id, out var shape) || shape.go != go)
            {
                shape = (go, Corners(shipRoot, go));
                entry.Shapes[id] = shape;
            }
            if (shape.corners == null) return false;
            foreach (Vector3 corner in shape.corners)
            {
                RoomSampleResult room = ship.SampleRoomAtPosition(corner);
                if (room.RoomId != roomId) return false;
            }
            return true;
        }

        private static Vector3[] Corners(Transform shipRoot, GameObject go)
        {
            go.GetComponentsInChildren(true, Buffer);
            bool any = false;
            var bounds = new Bounds();
            foreach (Renderer r in Buffer)
            {
                if (r is ParticleSystemRenderer) continue;
                if (any) bounds.Encapsulate(r.bounds);
                else { bounds = r.bounds; any = true; }
            }
            Buffer.Clear();
            if (!any) return null;
            var corners = new Vector3[8];
            Vector3 e = bounds.extents * 0.9f;
            for (int i = 0; i < 8; i++)
            {
                var world = bounds.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                corners[i] = shipRoot.InverseTransformPoint(world);
            }
            return corners;
        }

        private static void FindGlazedRooms(TrackedShipClient ship, IReadOnlyDictionary<HullPartPosition, HullPartNetworkedState> hull, HashSet<ushort> rooms)
        {
            rooms.Clear();
            foreach (var pair in hull)
            {
                if (!GlassParts.Contains(pair.Value.PartId)) continue;
                Vector3 at = PartPositionHelper.GetHullPartInteriorPos(pair.Key);
                foreach (Vector3 offset in Around)
                {
                    RoomSampleResult room = ship.SampleRoomAtPosition(at + offset);
                    if (room.IsRoom) rooms.Add(room.RoomId);
                }
            }
        }

        private static void SetHidden(GameObject go, bool hidden)
        {
            go.GetComponentsInChildren(true, Buffer);
            foreach (Renderer r in Buffer)
            {
                if (r is ParticleSystemRenderer) continue;
                if (hidden) HiddenRenderers.Add(r);
                else HiddenRenderers.Remove(r);
                r.forceRenderingOff = hidden;
            }
            Buffer.Clear();
            go.GetComponentsInChildren(true, Behaviours);
            foreach (Behaviour b in Behaviours)
            {
                if (b == null || !VisualOnly.Contains(b.GetType())) continue;
                if (hidden && b.enabled)
                {
                    b.enabled = false;
                    Paused.Add(b);
                }
                else if (!hidden && Paused.Remove(b))
                {
                    b.enabled = true;
                }
            }
            Behaviours.Clear();
        }

        private static void ShowAll(ShipEntry entry)
        {
            foreach (GameObject go in entry.Hidden.Values)
            {
                if (go != null) SetHidden(go, false);
            }
            entry.Hidden.Clear();
            entry.Shapes.Clear();
        }

        private static void Reset()
        {
            foreach (ShipEntry entry in Ships.Values) ShowAll(entry);
            Ships.Clear();
            HiddenRenderers.Clear();
            Paused.Clear();
        }
    }
}
