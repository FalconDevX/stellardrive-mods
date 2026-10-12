using HarmonyLib;
using MelonLoader;
using Ships.Physics;
using Ships.Physics.Collision;
using UnityEngine;

namespace ShipSafety.Patches
{
    // Statek w hangarze innego statku. Gra przy każdym kontakcie liczy niszczenie: część pęka, gdy uderzenie
    // przekroczy twardość x masę statku (szkło już przy ~1 m/s, drewno ~16 m/s, urządzenia zależnie od rozmiaru).
    // Po zniszczeniu przywraca prędkość sprzed zderzenia, więc statek dalej wjeżdża w drugi i niszczy kolejne
    // części - stąd rozwalanie i telepanie przy lądowaniu w hangarze. Kontakty z innym statkiem przy małej
    // prędkości względnej pomijamy w niszczeniu (fizyka nadal je odpycha). Liczy serwer (host).
    [HarmonyPatch(typeof(ShipCollisionDestruction), "OnCollisionEnter")]
    internal static class ShipContactDestructionPatch
    {
        private static float _nextLogTime;

        private static bool Prefix(ShipCollisionDestruction __instance, Collision collision)
        {
            if (!ShipSafetyMod.Enabled.Value || collision.rigidbody == null || collision.rigidbody.GetComponent<ShipRigidbody>() == null)
            {
                return true;
            }
            float speed = collision.relativeVelocity.magnitude;
            if (speed >= ShipSafetyMod.ShipCrashSpeed.Value)
            {
                return true;
            }
            if (Time.realtimeSinceStartup >= _nextLogTime)
            {
                _nextLogTime = Time.realtimeSinceStartup + 5f;
                Melon<ShipSafetyMod>.Logger.Msg($"Kontakt statków ({__instance.name} z {collision.rigidbody.name}) przy {speed:0.0} m/s - bez zniszczeń");
            }
            return false;
        }
    }

    // Spokojniejsza fizyka stykających się statków: więcej iteracji solvera (mniej drgań statku stojącego na innym
    // statku) i wolniejsze wypychanie z nakładania się (statek, który wjechał w drugi, jest odsuwany łagodnie,
    // zamiast być wystrzelony).
    [HarmonyPatch(typeof(ShipRigidbody), nameof(ShipRigidbody.SetRootPhysicsStateServer))]
    internal static class ShipSolverPatch
    {
        private static void Postfix(ShipRigidbody __instance)
        {
            if (!ShipSafetyMod.Enabled.Value)
            {
                return;
            }
            Rigidbody rigidbody = ShipPhysicsAccess.Rigidbody(__instance);
            if (rigidbody == null)
            {
                return;
            }
            int iterations = ShipSafetyMod.ShipSolverIterations.Value;
            if (rigidbody.solverIterations < iterations)
            {
                rigidbody.solverIterations = iterations;
                rigidbody.solverVelocityIterations = Mathf.Max(rigidbody.solverVelocityIterations, iterations / 3);
            }
            float depenetration = ShipSafetyMod.ShipDepenetrationSpeed.Value;
            if (depenetration > 0f && !Mathf.Approximately(rigidbody.maxDepenetrationVelocity, depenetration))
            {
                rigidbody.maxDepenetrationVelocity = depenetration;
            }
        }
    }
}
