using System.Runtime.CompilerServices;
using HarmonyLib;
using Ships.Rooms;
using UnityEngine;

namespace Mining.Patches
{
    // Lagi przy szybkim budowaniu/usuwaniu: po każdej zmianie kadłuba gra przelicza pomieszczenia (tlen) algorytmem
    // zalewania, który przechodzi całą pustą przestrzeń wokół statku - na dużym statku ~50-60 ms na KAŻDY blok
    // (zmierzone dziennikiem [perf]). Zmiany i tak się kumulują, więc przeliczamy dopiero, gdy budowanie ucichnie
    // na chwilę (QuietSeconds), a przy ciągłym budowaniu najrzadziej co MaxDelaySeconds. Wynik jest ten sam,
    // tylko pomieszczenie "zamyka się" ułamek sekundy później.
    [HarmonyPatch(typeof(ShipRoomsVoxelUpdater))]
    internal static class RoomsDebouncePatch
    {
        private const float QuietSeconds = 0.4f;
        private const float MaxDelaySeconds = 3f;

        private sealed class Pending
        {
            public float First = -1f;
            public float Last;
        }

        private static readonly ConditionalWeakTable<ShipRoomsVoxelUpdater, Pending> State = new ConditionalWeakTable<ShipRoomsVoxelUpdater, Pending>();
        internal static bool Force; // wczytywanie stanu pomieszczeń wymaga natychmiastowego przeliczenia

        private static void Changed(ShipRoomsVoxelUpdater updater)
        {
            Pending p = State.GetOrCreateValue(updater);
            float now = Time.unscaledTime;
            if (p.First < 0f)
            {
                p.First = now;
            }
            p.Last = now;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipRoomsVoxelUpdater.AddHullVoxel))]
        private static void AfterAdd(ShipRoomsVoxelUpdater __instance) => Changed(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipRoomsVoxelUpdater.RemoveHullVoxel))]
        private static void AfterRemove(ShipRoomsVoxelUpdater __instance) => Changed(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipRoomsVoxelUpdater.UpdateRooms))]
        private static bool BeforeUpdate(ShipRoomsVoxelUpdater __instance)
        {
            if (!State.TryGetValue(__instance, out Pending p) || p.First < 0f)
            {
                return true;
            }
            if (Force)
            {
                p.First = -1f;
                return true;
            }
            float now = Time.unscaledTime;
            if (now - p.Last < QuietSeconds && now - p.First < MaxDelaySeconds)
            {
                return false; // jeszcze budujesz - poczekaj
            }
            p.First = -1f;
            return true;
        }
    }

    [HarmonyPatch(typeof(ShipRooms), nameof(ShipRooms.LoadRoomStates))]
    internal static class RoomsLoadForcePatch
    {
        private static void Prefix() => RoomsDebouncePatch.Force = true;

        private static System.Exception Finalizer(System.Exception __exception)
        {
            RoomsDebouncePatch.Force = false;
            return __exception;
        }
    }
}
