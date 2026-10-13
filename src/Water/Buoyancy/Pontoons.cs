using System.Collections.Generic;
using Core.Values;
using MelonLoader;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using UnityEngine;

namespace Water.Buoyancy
{
    internal sealed class PontoonSet
    {
        public readonly List<(ushort part, Vector3 local)> Points = new List<(ushort, Vector3)>();
        public readonly Dictionary<ushort, float> Inflation = new Dictionary<ushort, float>();
        public int Count;
        public int PartCount = -1;
        public float NextScan;
        public bool Floating;
    }

    internal static class Pontoons
    {
        internal const ushort PartId = 222;
        internal const float InflateSeconds = 6f;

        internal const float FloatY = 0.62f, Radius = 0.38f, HalfLength = 0.95f;
        private const float Damping = 0.6f;
        private static readonly float[] Ends = { -HalfLength, HalfLength };

        internal static void Refresh(TrackedShip ship, PontoonSet set)
        {
            if (ship.PartCount == set.PartCount && Time.time < set.NextScan)
            {
                return;
            }
            set.PartCount = ship.PartCount;
            set.NextScan = Time.time + 2f;
            set.Points.Clear();
            set.Count = 0;
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part?.Settings == null || part.Settings.id != PartId)
                {
                    continue;
                }
                set.Count++;
                foreach (float z in Ends)
                {
                    set.Points.Add((part.Id, part.LocalPosition + part.LocalRotation * new Vector3(0f, FloatY, z)));
                }
            }
        }

        internal static void UpdateInflation(TrackedShip ship, PontoonSet set, float dt)
        {
            foreach (var (id, _) in set.Points)
            {
                bool open = ship.State.TryGetPart(id, out var part) && part.State is DoorState door && door.IsOpen;
                float target = open ? 1f : 0f;
                set.Inflation[id] = set.Inflation.TryGetValue(id, out float current) ? Mathf.MoveTowards(current, target, dt / InflateSeconds) : target;
            }
        }

        internal static float InflationOf(PontoonSet set, ushort id) => set.Inflation.TryGetValue(id, out float v) ? v : 0f;

        internal static bool Apply(Rigidbody rb, Vector3Double spaceCenter, PontoonSet set, float gravity, uint shipId)
        {
            if (set.Points.Count == 0)
            {
                return false;
            }
            float sea = WaterLevel.SeaRadius;
            float liftPerPoint = Mathf.Max(0f, WaterMod.PontoonLift.Value) * 0.5f;
            float mass = Mathf.Max(1f, rb.mass);
            float stiffness = gravity * liftPerPoint * set.Points.Count / (2f * Radius);
            float damping = 2f * Damping * Mathf.Sqrt(stiffness * mass) / set.Points.Count;
            float drag = Mathf.Max(0f, WaterMod.WaterDrag.Value) * mass / set.Points.Count;
            float submerged = 0f, inflated = 0f;

            foreach (var (id, local) in set.Points)
            {
                float inflation = InflationOf(set, id);
                inflated += inflation;
                Vector3 point = rb.position + rb.rotation * local;
                Vector3Double planet = spaceCenter + (Vector3Double)point;
                float altitude = (float)(planet.Magnitude - sea);
                float s = Mathf.Clamp01((Radius - altitude) / (2f * Radius));
                if (s <= 0f)
                {
                    continue;
                }
                submerged += s * inflation;
                Vector3 up = (Vector3)planet.Normalized;
                Vector3 v = rb.GetPointVelocity(point);
                float vUp = Vector3.Dot(v, up);
                Vector3 vh = v - up * vUp;
                float wet = Mathf.Min(1f, s * 3f);
                float lift = gravity * liftPerPoint * s * inflation;
                Vector3 force = up * (lift - damping * wet * inflation * vUp) - vh * (drag * wet * (0.25f + 0.03f * vh.magnitude));
                rb.AddForceAtPosition(force, point);
            }

            bool floating = submerged > 0f;
            if (floating && !set.Floating)
            {
                float capacity = Mathf.Max(0f, WaterMod.PontoonLift.Value) * inflated * 0.5f;
                Melon<WaterMod>.Logger.Msg($"Ship {shipId} on the water with {set.Count} pontoon(s): lift up to {capacity:0} kg, ship mass {mass:0} kg" +
                                           (capacity < mass ? " - too heavy, add or inflate pontoons" : ""));
            }
            set.Floating = floating;
            return floating;
        }
    }
}
