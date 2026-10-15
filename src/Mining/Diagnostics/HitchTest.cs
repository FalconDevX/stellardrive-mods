using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Services;
using MelonLoader;
using Players.Network.Model;
using Ships;
using Ships.Services;
using UnityEngine;

namespace Mining.Diagnostics
{
    internal static class HitchTest
    {
        internal static IEnumerator Run()
        {
            var log = Melon<MiningMod>.Logger;
            yield return new WaitForSeconds(8f);
            var players = ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            Players.TrackedPlayerServer player = players?.ActivePlayers.FirstOrDefault();
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            if (player == null || ships == null)
            {
                log.Msg("[hitch] no player or ships");
                yield break;
            }
            var net = HarmonyLib.Traverse.Create(player).Property("NetworkState").GetValue<Players.Network.PlayerNetworkState>();
            PlayerPhysicsState start = player.PhysicsState;
            var transpose = ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeServer>();
            var targets = new List<(TrackedShipServer ship, Vector3 pos)>();
            foreach (TrackedShipServer s in ships.Ships.OfType<TrackedShipServer>().OrderByDescending(s => s.GetHullParts().Count))
            {
                Vector3 pos;
                try
                {
                    pos = transpose.Position(s.PhysicsState.SpaceId, start.SpaceId, s.PhysicsState.Position);
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (Vector3.Distance(pos, start.Position) < 3000f && s.GetHullParts().Count > 20) targets.Add((s, pos));
                if (targets.Count >= 5) break;
            }
            log.Msg($"[hitch] player space {start.SpaceId} at {start.Position}, route over ships {string.Join(", ", targets.Select(t => $"{t.ship.Id} space {t.ship.PhysicsState.SpaceId} ({t.ship.GetHullParts().Count} hull, {Vector3.Distance(t.pos, start.Position):0} m)"))}");

            FrameProfiler.StartWatch(30f);
            FrameProfiler.ResetTotals();
            int frameStart = Time.frameCount;
            yield return new WaitForSeconds(30f);
            log.Msg($"[hitch] idle 30 s: {FrameProfiler.Hitches.Count} long frames");
            int idle = FrameProfiler.Hitches.Count;

            var route = new List<Vector3> { start.Position };
            Vector3 worldUp = start.Rotation * Vector3.up;
            foreach (var target in targets)
            {
                route.Add(target.pos + worldUp * 30f);
            }
            route.Add(start.Position);
            const float speed = 40f;
            float deadline = Time.unscaledTime + 120f;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                Vector3 a = route[i], b = route[i + 1];
                float length = Vector3.Distance(a, b);
                for (float t = 0f; t < length && Time.unscaledTime < deadline; t += speed * Time.deltaTime)
                {
                    PlayerPhysicsState state = player.PhysicsState;
                    state.SpaceId = start.SpaceId;
                    state.Position = Vector3.Lerp(a, b, t / Mathf.Max(0.01f, length));
                    state.Velocity = Vector3.zero;
                    net.ServerSetPhysicsState(state);
                    yield return null;
                }
                log.Msg($"[hitch] leg {i + 1}/{route.Count - 1} done, long frames so far {FrameProfiler.Hitches.Count}, player at {player.PhysicsState.Position} space {player.PhysicsState.SpaceId}");
            }
            FrameProfiler.StopWatch();
            yield return null;
            int frames = Time.frameCount - frameStart;
            File.WriteAllLines(Path.Combine(DevTools.OutDir, "test_hitch.txt"), new[] { $"idle long frames: {idle}", $"frames: {frames}", "" }.Concat(FrameProfiler.Totals(frames)).Concat(new[] { "" }).Concat(FrameProfiler.Hitches));
            log.Msg($"[hitch] done: {FrameProfiler.Hitches.Count} long frames ({idle} while idle)");
        }
    }
}
