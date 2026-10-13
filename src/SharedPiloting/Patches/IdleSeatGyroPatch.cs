using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Gyroscope;
using UnityEngine;

namespace SharedPiloting.Patches
{
    [HarmonyPatch(typeof(GyroscopesUpdater), "CalculateShipGyros")]
    internal static class IdleSeatGyroPatch
    {
        private const float InputDeadzone = 0.001f;
        private static readonly List<StatefulPart> Seats = new List<StatefulPart>();

        private static void Prefix(IShipStateWrite ship, out List<KeyValuePair<StatefulPart, PilotSeatState>> __state)
        {
            __state = null;
            List<StatefulPart> seats = Seats;
            seats.Clear();
            bool steering = false;
            foreach (StatefulPart part in ship.GetAllStatefulParts<PilotSeatState>())
            {
                if (((PilotSeatState)part.State).IsInUse)
                {
                    seats.Add(part);
                    steering |= IsSteering(part);
                }
            }
            if (seats.Count < 2 || !steering)
            {
                seats.Clear();
                return;
            }

            __state = new List<KeyValuePair<StatefulPart, PilotSeatState>>();
            foreach (StatefulPart seat in seats.Where(s => !IsSteering(s)))
            {
                var original = (PilotSeatState)seat.State;
                __state.Add(new KeyValuePair<StatefulPart, PilotSeatState>(seat, original));
                seat.State = original with { IsInUse = false };
            }
            seats.Clear();
        }

        private static void Finalizer(List<KeyValuePair<StatefulPart, PilotSeatState>> __state)
        {
            if (__state == null)
            {
                return;
            }
            foreach (var pair in __state)
            {
                var current = pair.Key.State as PilotSeatState;
                pair.Key.State = current != null ? current with { IsInUse = pair.Value.IsInUse } : pair.Value;
            }
        }

        private static bool IsSteering(StatefulPart seat)
        {
            Vector3 input = ((PilotSeatState)seat.State).RotationInput;
            return input.sqrMagnitude > InputDeadzone * InputDeadzone;
        }
    }
}
