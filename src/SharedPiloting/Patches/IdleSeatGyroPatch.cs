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
    // W grze każdy zajęty fotel pilota ma wbudowany żyroskop, który co klatkę dąży do obrotu chcianego przez
    // swojego gracza. Gracz, który tylko siedzi, chce "zero obrotu", więc jego fotel hamuje statek - a ponieważ
    // fotele liczone są po kolei, wygrywa ten liczony później (raz jeden gracz steruje, raz drugi).
    //
    // Tutaj: jeśli któryś fotel dostaje ruch myszy, fotele bez ruchu są na czas liczenia żyroskopów traktowane
    // jak puste (ich żyroskop nic nie robi). Gdy nikt nie obraca - wszystkie stabilizują jak w oryginale.
    // Liczy to serwer (host), więc wystarczy, że host ma moda.
    [HarmonyPatch(typeof(GyroscopesUpdater), "CalculateShipGyros")]
    internal static class IdleSeatGyroPatch
    {
        private const float InputDeadzone = 0.001f;

        private static void Prefix(IShipStateWrite ship, out List<KeyValuePair<StatefulPart, PilotSeatState>> __state)
        {
            __state = null;
            List<StatefulPart> seats = ship.GetAllStatefulParts<PilotSeatState>()
                .Where(p => ((PilotSeatState)p.State).IsInUse)
                .ToList();
            if (seats.Count < 2 || !seats.Any(IsSteering))
            {
                return;
            }

            __state = new List<KeyValuePair<StatefulPart, PilotSeatState>>();
            foreach (StatefulPart seat in seats.Where(s => !IsSteering(s)))
            {
                var original = (PilotSeatState)seat.State;
                __state.Add(new KeyValuePair<StatefulPart, PilotSeatState>(seat, original));
                seat.State = original with { IsInUse = false };
            }
        }

        // Przywracamy prawdziwy stan foteli zaraz po obliczeniu żyroskopów (zanim gra zapisze ich nowy stan).
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
