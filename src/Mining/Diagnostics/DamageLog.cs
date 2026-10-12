using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Players;
using Players.Network;
using Players.Physics.Model;
using Players.Values;
using UnityEngine;

namespace Mining.Diagnostics
{
    // Dziennik obrażeń gracza (gra nie zapisuje, od czego się zginęło). Gracz traci życie tylko od uderzeń (szybkie
    // zderzenie z czymś bez fizyki - np. kadłubem statku) i od braku tlenu. Zapisujemy ostatnie zdarzenia, a przy śmierci
    // całość trafia do UserData/Mining/death_log.txt i do logu MelonLoadera.
    internal static class DamageLog
    {
        private static readonly LinkedList<string> Recent = new LinkedList<string>();
        private static float _lastOxygenLog;

        internal static void Add(string line)
        {
            string entry = $"{DateTime.Now:HH:mm:ss.f} {line}";
            Recent.AddLast(entry);
            while (Recent.Count > 15) Recent.RemoveFirst();
            Melon<MiningMod>.Logger.Msg("[obrażenia] " + line);
        }

        internal static void OnDeath()
        {
            Add("ŚMIERĆ");
            try
            {
                string path = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "death_log.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" + string.Join("\n", Recent) + "\n\n");
            }
            catch (Exception)
            {
                // zapis dziennika nie może psuć gry
            }
        }

        internal static bool OxygenLogDue()
        {
            if (Time.unscaledTime - _lastOxygenLog < 3f) return false;
            _lastOxygenLog = Time.unscaledTime;
            return true;
        }

        internal static string PathOf(Transform t)
        {
            string path = t.name;
            for (int i = 0; i < 4 && t.parent != null; i++)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }

    [HarmonyPatch(typeof(TrackedPlayerServer), "ReactToCollision")]
    internal static class DamageLogCollisionPatch
    {
        private static void Prefix(PlayerPhysicsCollision collision, PlayerProperties ____properties)
        {
            try
            {
                if (____properties == null || collision.Collider == null || collision.Collider.GetComponentInParent<Rigidbody>() != null) return;
                float damage = ____properties.CalculateCollisionDamage(collision.Impact);
                if (damage <= 0f) return;
                DamageLog.Add($"uderzenie {collision.Impact.magnitude:0.0} m/s w '{DamageLog.PathOf(collision.Collider.transform)}' (warstwa {LayerMask.LayerToName(collision.Collider.gameObject.layer)}): -{damage:0.0} życia");
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStatusUpdater), "TakeDamage")]
    internal static class DamageLogTakeDamagePatch
    {
        private static void Prefix(float damage, PlayerNetworkState ____state, out float __state)
        {
            __state = ____state != null ? ____state.Health : -1f;
            if (____state != null && !____state.HasOxygen && ____state.OxygenLevel <= 0f && DamageLog.OxygenLogDue())
            {
                DamageLog.Add($"brak tlenu (butla pusta, nie ma czym oddychać w tym miejscu): życie {____state.Health:0}");
            }
        }

        private static void Postfix(PlayerNetworkState ____state, float __state)
        {
            if (____state != null && __state > 0f && ____state.Health <= 0f)
            {
                DamageLog.OnDeath();
            }
        }
    }
}
