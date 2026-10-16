using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using Planets.Network;
using Planets.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Sites
{
    internal sealed class ResourceKind
    {
        internal string Key;
        internal string Name;
        internal uint ItemId;
        internal GameObject RockPrefab;
        internal bool Glowing;
    }

    internal sealed class MiningSite
    {
        internal ulong PlanetId;
        internal long Key;
        internal Vector3 Direction;
        internal Vector3 Center;
        internal Quaternion Rotation;
        internal float Radius;
        internal ResourceKind Kind;
        internal int Seed;
    }

    internal static class MiningSites
    {
        private static readonly (string key, string name, uint item)[] KnownKinds =
        {
            ("Iron", "iron", 101), ("Copper", "copper", 112), ("Aluminum", "aluminum", 104), ("Glass", "glass", 102), ("Ice", "ice", 109),
        };

        private sealed class PlanetInfo
        {
            internal TrackedPlanet Tracked;
            internal float Radius;
            internal int CellsPerFace;
            internal ResourceKind[] Kinds;
            internal PlanetDensePlacementConfig Forest;
            internal float ForestThreshold;
            internal ResourceKind Uranium;
        }

        private static readonly Dictionary<ulong, PlanetInfo> Planets = new Dictionary<ulong, PlanetInfo>();
        private static readonly Dictionary<long, MiningSite> Cache = new Dictionary<long, MiningSite>();
        private static readonly HashSet<long> Empty = new HashSet<long>();

        internal static void Reset()
        {
            Planets.Clear();
            Cache.Clear();
            Empty.Clear();
        }

        internal static MiningSite FindAt(ulong planetId, Vector3 planetPos, float extraRadius, float maxHeight)
        {
            MiningSite best = null;
            float bestDist = float.MaxValue;
            foreach (MiningSite site in Near(planetId, planetPos, 1))
            {
                Vector3 offset = planetPos - site.Center;
                float height = Vector3.Dot(offset, site.Direction);
                float horizontal = (offset - site.Direction * height).magnitude;
                if (horizontal <= site.Radius + extraRadius && height > -3f && height < maxHeight && horizontal < bestDist)
                {
                    best = site;
                    bestDist = horizontal;
                }
            }
            return best;
        }

        internal static IEnumerable<MiningSite> Near(ulong planetId, Vector3 planetPos, int range)
        {
            PlanetInfo info = GetPlanet(planetId);
            if (info == null || info.Kinds.Length == 0 || planetPos.sqrMagnitude < 1f)
            {
                yield break;
            }
            Vector3 dir = planetPos.normalized;
            ToCell(dir, info.CellsPerFace, out int face, out int ci, out int cj);
            for (int di = -range; di <= range; di++)
            {
                for (int dj = -range; dj <= range; dj++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= info.CellsPerFace || j >= info.CellsPerFace)
                    {
                        continue;
                    }
                    MiningSite site = GetSite(planetId, info, face, i, j);
                    if (site != null)
                    {
                        yield return site;
                    }
                }
            }
        }

        internal static float CellSize(ulong planetId)
        {
            PlanetInfo info = GetPlanet(planetId);
            return info == null ? 0f : info.Radius * Mathf.PI * 0.5f / info.CellsPerFace;
        }

        private static MiningSite GetSite(ulong planetId, PlanetInfo info, int face, int i, int j)
        {
            long key = ((long)(planetId & 0xFFFF) << 44) | ((long)face << 40) | ((long)i << 20) | (long)j;
            if (Cache.TryGetValue(key, out MiningSite cached))
            {
                return cached;
            }
            if (Empty.Contains(key))
            {
                return null;
            }
            MiningSite site = Generate(planetId, info, face, i, j, key);
            if (site != null)
            {
                Cache[key] = site;
            }
            else
            {
                Empty.Add(key);
            }
            return site;
        }

        private static MiningSite Generate(ulong planetId, PlanetInfo info, int face, int i, int j, long key)
        {
            MiningSite site = GenerateRegular(planetId, info, face, i, j, key);
            if (site == null && planetId == Content.WaterPump.LakePlanetId)
            {
                site = GenerateBeachGlass(planetId, info, face, i, j, key);
            }
            return site;
        }

        private const float BeachGlassChance = 0.7f;

        private static MiningSite GenerateBeachGlass(ulong planetId, PlanetInfo info, int face, int i, int j, long key)
        {
            ResourceKind glass = info.Kinds.FirstOrDefault(k => k.ItemId == Items.Model.ItemsConstants.GlassItemId);
            var rng = new System.Random(unchecked((int)Hash((ulong)key ^ 0xB5AC4651A55UL ^ (planetId * 0x9E3779B97F4A7C15UL))));
            if (glass == null || rng.NextDouble() > BeachGlassChance)
            {
                return null;
            }
            Vector3 dir = Vector3.up;
            if (!TryFindBeach(info, face, i, j, rng, ref dir))
            {
                return null;
            }
            float radius = MiningMod.SiteRadius.Value * (0.75f + 0.25f * (float)rng.NextDouble());
            return Place(planetId, info, key, dir, glass, radius, rng, 0.8f);
        }

        private static MiningSite GenerateRegular(ulong planetId, PlanetInfo info, int face, int i, int j, long key)
        {
            var rng = new System.Random(unchecked((int)Hash((ulong)key ^ (planetId * 0x9E3779B97F4A7C15UL))));
            if (rng.NextDouble() > MiningMod.SiteChance.Value)
            {
                return null;
            }
            float u = (i + 0.2f + 0.6f * (float)rng.NextDouble()) / info.CellsPerFace * 2f - 1f;
            float v = (j + 0.2f + 0.6f * (float)rng.NextDouble()) / info.CellsPerFace * 2f - 1f;
            Vector3 dir = FromCell(face, u, v);
            ResourceKind kind = info.Kinds[rng.Next(info.Kinds.Length)];
            float radius = MiningMod.SiteRadius.Value * (0.85f + 0.3f * (float)rng.NextDouble());
            if (info.Uranium != null && Hash((ulong)key ^ 0x55AA55AA55UL) % 1000 < (ulong)(MiningMod.UraniumChance.Value * 1000f))
            {
                kind = info.Uranium;
            }

            if (planetId == Content.WaterPump.LakePlanetId)
            {
                if (kind != info.Uranium && kind.ItemId == Items.Model.ItemsConstants.GlassItemId && !TryFindBeach(info, face, i, j, rng, ref dir))
                {
                    ResourceKind[] others = info.Kinds.Where(k => k.ItemId != Items.Model.ItemsConstants.GlassItemId).ToArray();
                    if (others.Length == 0)
                    {
                        return null;
                    }
                    kind = others[rng.Next(others.Length)];
                }
                if (info.Tracked.PointsProvider.GetElevationAtPoint(dir) - info.Radius < Content.WaterPump.SeaLevel + 0.3f)
                {
                    return null;
                }
            }

            return Place(planetId, info, key, dir, kind, radius, rng, 0.45f);
        }

        private static MiningSite Place(ulong planetId, PlanetInfo info, long key, Vector3 dir, ResourceKind kind, float radius, System.Random rng, float maxSlope)
        {
            if (info.Forest != null && info.Forest.EvaluateNoiseAtPosition(dir) >= info.ForestThreshold)
            {
                return null;
            }
            float elevation = info.Tracked.PointsProvider.GetElevationAtPoint(dir);
            Vector3 center = dir * elevation;
            Vector3 tangent = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(dir, tangent);
            float maxDiff = 0f;
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                Vector3 p = center + (tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a)) * radius * 1.3f;
                Vector3 pd = p.normalized;
                float h = info.Tracked.PointsProvider.GetElevationAtPoint(pd) - elevation;
                maxDiff = Mathf.Max(maxDiff, Mathf.Abs(h));
            }
            if (maxDiff > radius * maxSlope)
            {
                return null;
            }
            return new MiningSite
            {
                PlanetId = planetId,
                Key = key,
                Direction = dir,
                Center = center,
                Rotation = Quaternion.LookRotation(bitangent, dir),
                Radius = radius,
                Kind = kind,
                Seed = rng.Next(),
            };
        }

        private static bool TryFindBeach(PlanetInfo info, int face, int i, int j, System.Random rng, ref Vector3 dir)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float u = (i + 0.1f + 0.8f * (float)rng.NextDouble()) / info.CellsPerFace * 2f - 1f;
                float v = (j + 0.1f + 0.8f * (float)rng.NextDouble()) / info.CellsPerFace * 2f - 1f;
                Vector3 candidate = FromCell(face, u, v);
                float height = info.Tracked.PointsProvider.GetElevationAtPoint(candidate) - info.Radius;
                if (height > Content.WaterPump.SeaLevel + 0.5f && height < 69.5f)
                {
                    dir = candidate;
                    return true;
                }
            }
            return false;
        }

        private static PlanetInfo GetPlanet(ulong planetId)
        {
            if (Planets.TryGetValue(planetId, out PlanetInfo info))
            {
                return info;
            }
            TrackedPlanet tracked = FindTrackedPlanet(planetId);
            if (tracked == null)
            {
                return null;
            }
            Planet planet = tracked.Planet;
            float spacing = Mathf.Max(50f, MiningMod.SiteSpacing.Value);
            info = new PlanetInfo
            {
                Tracked = tracked,
                Radius = planet.Radius,
                CellsPerFace = Mathf.Clamp(Mathf.RoundToInt(planet.Radius * Mathf.PI * 0.5f / spacing), 1, 1 << 19),
                Kinds = FindKinds(planet),
            };
            if (planet.Id == 4 || planet.name.Contains("Otrera b"))
            {
                GameObject rock = info.Kinds.FirstOrDefault(k => k.Key == "Iron")?.RockPrefab ?? info.Kinds.FirstOrDefault()?.RockPrefab;
                if (rock != null)
                {
                    info.Uranium = new ResourceKind { Key = "Uranium", Name = "uranium", ItemId = Content.Uranium.ItemId, RockPrefab = rock, Glowing = true };
                }
            }

            var dense = planet.GetComponent<PlanetDenseEnvironment>();
            var hotspots = planet.GetComponent<PlanetHotspotsEnvironment>();
            PlanetHotspotsPlacementConfig avoid = hotspots?.hotspotsPlacement?.FirstOrDefault(c => c != null && c.avoidDenseEnvConstraint);
            if (dense?.configs != null && avoid != null && avoid.denseEnvConfigId < dense.configs.Length)
            {
                info.Forest = dense.configs[avoid.denseEnvConfigId];
                info.ForestThreshold = avoid.denseEnvThreshold;
            }
            Planets[planetId] = info;
            Melon<MiningMod>.Logger.Msg($"Deposits on {planet.name}: {string.Join(", ", info.Kinds.Select(k => k.Name))}{(info.Uranium != null ? ", uranium" : "")} " +
                                        $"(about every {spacing:0} m, chance {MiningMod.SiteChance.Value:0.##}, forest: {(info.Forest != null ? "avoided" : "none")})");
            return info;
        }

        private static ResourceKind[] FindKinds(Planet planet)
        {
            var hotspots = planet.GetComponent<PlanetHotspotsEnvironment>();
            var prefabs = (hotspots?.hotspotsPlacement ?? new List<PlanetHotspotsPlacementConfig>())
                .Where(c => c?.distributionSteps != null)
                .SelectMany(c => c.distributionSteps)
                .Where(s => s?.objectPrefab != null)
                .Select(s => s.objectPrefab)
                .Distinct()
                .ToList();
            var kinds = new List<ResourceKind>();
            foreach (var (key, name, item) in KnownKinds)
            {
                GameObject rock = prefabs.Where(p => p.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 && p.GetComponentInChildren<MeshFilter>(true) != null)
                    .OrderBy(p => p.name.IndexOf("Small", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : p.name.IndexOf("Medium", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2)
                    .FirstOrDefault();
                if (rock != null)
                {
                    kinds.Add(new ResourceKind { Key = key, Name = name, ItemId = item, RockPrefab = rock });
                }
            }
            return kinds.ToArray();
        }

        private static PlanetsServerTracker _serverTracker;
        private static PlanetsClientTracker _clientTracker;
        private static float _nextTrackerSearch;

        internal static TrackedPlanet FindTrackedPlanet(ulong planetId)
        {
            if ((_serverTracker == null || _clientTracker == null) && Time.unscaledTime >= _nextTrackerSearch)
            {
                _nextTrackerSearch = Time.unscaledTime + 2f;
                if (_serverTracker == null) _serverTracker = Object.FindAnyObjectByType<PlanetsServerTracker>();
                if (_clientTracker == null) _clientTracker = Object.FindAnyObjectByType<PlanetsClientTracker>();
            }
            foreach (MonoBehaviour tracker in new MonoBehaviour[] { _serverTracker, _clientTracker })
            {
                if (tracker != null && Traverse.Create(tracker).Field("_planets").GetValue() is IDictionary planets)
                {
                    foreach (DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet planet && planet.Id == planetId)
                        {
                            return planet;
                        }
                    }
                }
            }
            return null;
        }

        private static void ToCell(Vector3 dir, int cells, out int face, out int i, out int j)
        {
            Vector3 a = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
            float u, v;
            if (a.x >= a.y && a.x >= a.z) { face = dir.x > 0 ? 0 : 1; u = dir.y / a.x; v = dir.z / a.x; }
            else if (a.y >= a.z) { face = dir.y > 0 ? 2 : 3; u = dir.x / a.y; v = dir.z / a.y; }
            else { face = dir.z > 0 ? 4 : 5; u = dir.x / a.z; v = dir.y / a.z; }
            i = Mathf.Clamp(Mathf.FloorToInt((u + 1f) * 0.5f * cells), 0, cells - 1);
            j = Mathf.Clamp(Mathf.FloorToInt((v + 1f) * 0.5f * cells), 0, cells - 1);
        }

        private static Vector3 FromCell(int face, float u, float v)
        {
            switch (face)
            {
                case 0: return new Vector3(1f, u, v).normalized;
                case 1: return new Vector3(-1f, u, v).normalized;
                case 2: return new Vector3(u, 1f, v).normalized;
                case 3: return new Vector3(u, -1f, v).normalized;
                case 4: return new Vector3(u, v, 1f).normalized;
                default: return new Vector3(u, v, -1f).normalized;
            }
        }

        private static ulong Hash(ulong x)
        {
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return x;
        }
    }
}
