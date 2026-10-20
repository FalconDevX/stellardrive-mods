using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Ships;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Diagnostics
{
    internal static class CostBreakdown
    {
        private static readonly string[] ShipMethods = { "UpdateParts", "PrepareConsumers", "PreparePowerProducers", "UpdateRooms", "UpdateMass", "UpdateSignalEmitters", "UpdateRootPhysics", "UpdateGyroscopes", "PrepareGyroscopesPowerConsumption" };
        private static readonly Dictionary<uint, long> ShipTicks = new Dictionary<uint, long>();
        private static readonly Dictionary<uint, TrackedShipServer> Ships = new Dictionary<uint, TrackedShipServer>();
        private static readonly Dictionary<Scene, long> SceneTicks = new Dictionary<Scene, long>();
        private static readonly Dictionary<Scene, uint> SceneSpaces = new Dictionary<Scene, uint>();
        private static PropertyInfo _sceneProperty;
        private static FieldInfo _spaceField;

        internal static int Patch(HarmonyLib.Harmony harmony)
        {
            int count = 0;
            var prefix = new HarmonyMethod(typeof(CostBreakdown), nameof(Prefix));
            foreach (string name in ShipMethods)
            {
                MethodInfo m = AccessTools.Method(typeof(TrackedShipServer), name);
                if (m == null) continue;
                try
                {
                    harmony.Patch(m, prefix, new HarmonyMethod(typeof(CostBreakdown), nameof(ShipPostfix)));
                    count++;
                }
                catch (Exception)
                {
                }
            }
            Type scene = AccessTools.TypeByName("Managers.Server.Updaters.TrackedScene");
            MethodInfo simulate = scene == null ? null : AccessTools.Method(scene, "Simulate");
            if (simulate != null)
            {
                _sceneProperty = AccessTools.Property(scene, "Scene");
                _spaceField = AccessTools.Field(scene, "CurrentSpaceId");
                try
                {
                    harmony.Patch(simulate, prefix, new HarmonyMethod(typeof(CostBreakdown), nameof(ScenePostfix)));
                    count++;
                }
                catch (Exception)
                {
                }
            }
            return count;
        }

        internal static void Reset()
        {
            ShipTicks.Clear();
            Ships.Clear();
            SceneTicks.Clear();
            SceneSpaces.Clear();
        }

        private static void Prefix(out long __state) => __state = FrameProfiler.Measuring ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

        private static void ShipPostfix(long __state, TrackedShipServer __instance)
        {
            if (__state == 0 || !FrameProfiler.Measuring || __instance == null) return;
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
            uint id = __instance.Id;
            ShipTicks.TryGetValue(id, out long t);
            ShipTicks[id] = t + dt;
            Ships[id] = __instance;
        }

        private static void ScenePostfix(long __state, object __instance)
        {
            if (__state == 0 || !FrameProfiler.Measuring || _sceneProperty == null) return;
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
            var scene = (Scene)_sceneProperty.GetValue(__instance);
            SceneTicks.TryGetValue(scene, out long t);
            SceneTicks[scene] = t + dt;
            if (_spaceField != null) SceneSpaces[scene] = (uint)_spaceField.GetValue(__instance);
        }

        internal static void Write(StringBuilder sb, IReadOnlyDictionary<string, long> ticks, IReadOnlyDictionary<string, int> calls, int frames, float frameMs)
        {
            double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frames);
            double Get(string key) => ticks.TryGetValue(key, out long v) ? v * toMs : 0.0;
            double server = Get("game FishNet.Runtime: NetworkReaderLoop.Update");
            double pre = Get("game step WorldPhysicsServerUpdater.PrePhysics");
            double physx = Get("game step PhysicsScenesServerUpdater.SimulatePhysics");
            double parts = Get("game step WorldPhysicsServerUpdater.UpdateShipParts");
            double shipPhysics = Get("game step WorldPhysicsServerUpdater.UpdateShipsPhysics");
            double prepare = Get("game step PhysicsScenesServerUpdater.PreparePhysicsScenes");
            double loading = Get("game step WorldPhysicsServerUpdater.UpdateLoadedShips");
            double visuals = Get("game Assembly-CSharp: ClientVisualsUpdater.Update");
            double modsTop = ticks.Where(kv => !kv.Key.StartsWith("game ") && !kv.Key.Contains(" hook: ")).Sum(kv => kv.Value) * toMs;
            int steps = calls.TryGetValue("game step WorldPhysicsServerUpdater.PrePhysics", out int c) ? c / Math.Max(1, frames) : 0;
            double rest = Math.Max(0.0, frameMs - server - visuals - modsTop);

            sb.AppendLine($"Where the frame goes ({frameMs:0.0} ms with hooks = 100%, nested lines are part of the line above):");
            void Line(int depth, string label, double ms) => sb.AppendLine($"  {new string(' ', depth * 2)}{ms / Math.Max(0.001f, frameMs) * 100.0,5:0}%  {ms,6:0.0} ms  {label}");
            Line(0, "server tick (game simulation)", server);
            Line(1, $"physics steps ({steps} per frame, {(steps > 0 ? pre / steps : 0):0.0} ms per step, budget {Time.fixedDeltaTime * 1000f:0} ms)", pre);
            Line(2, "PhysX simulation (collisions, contacts, bodies)", physx);
            Line(2, "ship parts (power, fluids, cables, rooms, thrusters)", parts);
            Line(2, "ship forces and joints", shipPhysics);
            Line(2, "physics scenes and ship loading", prepare + loading);
            Line(2, "other step work", Math.Max(0.0, pre - physx - parts - shipPhysics - prepare - loading));
            Line(1, "server work outside the physics step (network, sync, other updaters)", Math.Max(0.0, server - pre));
            Line(0, "client visuals (cables, ship visuals)", visuals);
            Line(0, "mod updates outside the server tick", modsTop);
            Line(0, "rendering, Unity and everything not hooked", rest);
            sb.AppendLine();

            if (SceneTicks.Count > 0)
            {
                var bodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
                var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(col => col.enabled && col.gameObject.activeInHierarchy).ToList();
                sb.AppendLine("PhysX per physics scene (ms per frame):");
                foreach (var kv in SceneTicks.OrderByDescending(kv => kv.Value))
                {
                    Scene scene = kv.Key;
                    var inScene = bodies.Where(b => b.gameObject.scene == scene).ToList();
                    int awake = inScene.Count(b => !b.isKinematic && !b.IsSleeping());
                    int kinematic = inScene.Count(b => b.isKinematic);
                    int cols = colliders.Count(col => col.gameObject.scene == scene);
                    string space = SceneSpaces.TryGetValue(scene, out uint s) ? $"space {s}" : "space ?";
                    sb.AppendLine($"  {kv.Value * toMs,6:0.00} ms  {scene.name} ({space}): bodies {inScene.Count} (awake {awake}, kinematic {kinematic}), colliders {cols}");
                }
                sb.AppendLine();
            }

            if (ShipTicks.Count > 0)
            {
                double total = ShipTicks.Values.Sum() * toMs;
                sb.AppendLine($"Ship part and force updates per ship ({Ships.Count} ships, {total:0.0} ms per frame in total, most expensive first):");
                foreach (var kv in ShipTicks.OrderByDescending(kv => kv.Value).Take(12))
                {
                    TrackedShipServer ship = Ships[kv.Key];
                    double ms = kv.Value * toMs;
                    string state = "no body";
                    Rigidbody body = ship.RigidbodyObject != null ? ship.RigidbodyObject.GetComponent<Rigidbody>() : null;
                    if (body != null) state = body.isKinematic ? "kinematic" : body.IsSleeping() ? "asleep" : $"awake {body.linearVelocity.magnitude:0.00} m/s";
                    int hull = 0;
                    try { hull = ship.GetHullParts().Count; }
                    catch (Exception) { }
                    sb.AppendLine($"  {ms,6:0.00} ms  {ms / Math.Max(0.001, total) * 100.0,3:0}%  ship {kv.Key}: {ship.PartCount} parts, {hull} hull, {state}");
                }
                sb.AppendLine();
            }
        }
    }
}
