using System.Collections;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Odyssey.Server;
using Ships;
using Ships.Services;
using UnityEngine;

namespace Odyssey.Diagnostics
{
    internal static class PhotoTest
    {
        internal static IEnumerator Run()
        {
            var log = Melon<DevMod>.Logger;
            MelonBase photo = MelonBase.RegisteredMelons.FirstOrDefault(m => m.Info.Name == "Photo Mode");
            var ships = ServiceCache.Get<ShipsServerTracker>();
            TrackedShipServer ship = ships?.Ships.OrderByDescending(s => s.PartCount).FirstOrDefault();
            if (photo == null || ship == null)
            {
                log.Msg($"[photo] photo mode {(photo != null ? "found" : "MISSING")}, ship {(ship != null ? "found" : "MISSING")}");
                yield break;
            }
            HydrogenThrusterServer.ForcedStrength = 1f;
            yield return new WaitForSecondsRealtime(3f);
            Traverse t = Traverse.Create(photo);
            t.Method("Enter").GetValue();
            log.Msg($"[photo] entered, active {t.Field("_active").GetValue()}, frozen {t.Field("Frozen").GetValue()}, timeScale {Time.timeScale}");
            Vector3 before = ship.PhysicsState.Position;
            yield return new WaitForSecondsRealtime(2f);
            log.Msg($"[photo] ship moved {Vector3.Distance(before, ship.PhysicsState.Position):0.00} m in 2 s with time stopped");
            for (int i = 0; i < 3; i++)
            {
                t.Field("_yaw").SetValue((float)t.Field("_yaw").GetValue() + 70f);
                t.Field("_distance").SetValue((float)t.Field("_distance").GetValue() * 1.6f + 20f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return (IEnumerator)t.Method("Capture").GetValue();
                yield return new WaitForSecondsRealtime(1f);
            }
            t.Method("Exit").GetValue();
            before = ship.PhysicsState.Position;
            yield return new WaitForSecondsRealtime(2f);
            log.Msg($"[photo] left, ship moved {Vector3.Distance(before, ship.PhysicsState.Position):0.00} m in 2 s, timeScale {Time.timeScale}");
            HydrogenThrusterServer.ForcedStrength = -1f;
        }
    }
}
