using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;

namespace Odyssey.Diagnostics
{
    internal static class FreezeWatchdog
    {
        private const int StallSeconds = 8;
        private const int MaxDepth = 24;
        private static readonly string[] Stack = new string[MaxDepth];
        private static readonly long[] Since = new long[MaxDepth];
        private static volatile int _depth;
        private static volatile int _beat;
        private static Thread _thread;

        internal static void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Watch) { IsBackground = true, Name = "Mining freeze watchdog" };
            _thread.Start();
        }

        internal static void Beat() => _beat++;

        internal static void Enter(string name)
        {
            int d = _depth;
            if (d < MaxDepth)
            {
                Stack[d] = name;
                Since[d] = DateTime.UtcNow.Ticks;
            }
            _depth = d + 1;
        }

        internal static void Exit()
        {
            if (_depth > 0) _depth--;
        }

        private static void Watch()
        {
            int last = _beat, reported = -1;
            DateTime changed = DateTime.UtcNow;
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    int beat = _beat;
                    if (beat != last)
                    {
                        last = beat;
                        changed = DateTime.UtcNow;
                        continue;
                    }
                    if (reported == beat || (DateTime.UtcNow - changed).TotalSeconds < StallSeconds) continue;
                    reported = beat;
                    Report((DateTime.UtcNow - changed).TotalSeconds);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void Report(double seconds)
        {
            var sb = new StringBuilder();
            sb.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} game frozen for {seconds:0} s, inside: ");
            int depth = Math.Min(_depth, MaxDepth);
            if (depth == 0) sb.Append("(no watched step - game or Unity code outside the watched methods)");
            long now = DateTime.UtcNow.Ticks;
            for (int i = 0; i < depth; i++)
            {
                if (i > 0) sb.Append(" > ");
                sb.Append($"{Stack[i]} ({TimeSpan.FromTicks(now - Since[i]).TotalSeconds:0.0} s)");
            }
            string line = sb.ToString();
            File.AppendAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "freeze.txt"), line + Environment.NewLine);
            Melon<CoreMod>.Logger.Warning("[freeze] " + line);
        }
    }

    [HarmonyPatch]
    internal static class FreezeWatchdogPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] names =
            {
                "Managers.Server.WorldPhysicsServerUpdater:PrePhysics", "Managers.Server.WorldPhysicsServerUpdater:PostPhysics",
                "Managers.Server.WorldPhysicsServerUpdater:UpdateShipParts", "Managers.Server.WorldPhysicsServerUpdater:UpdateShipsPhysics",
                "Managers.Server.WorldPhysicsServerUpdater:UpdateLoadedShips", "Managers.Server.WorldPhysicsServerUpdater:UpdatePlayersPhysics",
                "Managers.Server.PhysicsScenesServerUpdater:SimulatePhysics", "Managers.Client.ClientVisualsUpdater:Update",
                "Managers.Client.WorldPhysicsClientUpdater:Update", "Ships.TrackedShipServer:UpdateParts", "Ships.TrackedShipClient:UpdatePhysics",
                "Ships.Rooms.ShipRoomsVoxelUpdater:UpdateRooms", "Ships.Physics.Collision.ShipHullColliders:UpdateColliders",
                "Ships.Visuals.ShipHullVisuals:UpdateHull", "Ships.Cables.SignalConnectionsServerUpdater:UpdateSignalValues",
                "Players.Physics.PlayerPhysics:UpdatePhysics",
            };
            foreach (string name in names)
            {
                MethodBase m = AccessTools.Method(name);
                if (m != null) yield return m;
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(MethodBase __originalMethod) => FreezeWatchdog.Enter(__originalMethod.DeclaringType?.Name + "." + __originalMethod.Name);

        [HarmonyPriority(Priority.Last)]
        private static void Finalizer() => FreezeWatchdog.Exit();
    }
}
