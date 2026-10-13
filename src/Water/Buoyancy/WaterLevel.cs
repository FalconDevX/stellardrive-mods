using Core.Values;
using Planets.Network;
using UnityEngine;
using Water.Terrain;

namespace Water.Buoyancy
{
    internal struct Immersion
    {
        public float Fraction;
        public float Height;
        public Vector3 LocalCenter;
        public Vector3Double HullCenter;
        public Vector3Double SurfacePoint;
        public Vector3 Up;
        public float Radius;
    }

    internal sealed class LakeCache
    {
        public Vector3 Direction;
        public bool Valid;
        public bool Lake;
    }

    internal static class WaterLevel
    {
        internal const ulong PlanetId = 4;

        private static TrackedPlanet _tracked;
        private static float _nextFind;

        internal static void Clear()
        {
            _tracked = null;
            _nextFind = 0f;
        }

        internal static bool Ready()
        {
            if (_tracked != null && _tracked.Planet != null && _tracked.PointsProvider != null)
            {
                return true;
            }
            if (Time.unscaledTime < _nextFind)
            {
                return false;
            }
            _nextFind = Time.unscaledTime + 2f;
            _tracked = Surface.WaterSurface.FindTracked();
            return _tracked != null && _tracked.Planet != null && _tracked.PointsProvider != null;
        }

        internal static float PlanetRadius => _tracked.Planet.Radius;

        internal static float SeaRadius => _tracked.Planet.Radius + LakeTerrainPatch.SeaLevel;

        internal static bool IsLake(Vector3Double planetPos, LakeCache cache)
        {
            Vector3 dir = (Vector3)planetPos.Normalized;
            float moved = cache.Valid ? (dir - cache.Direction).magnitude * SeaRadius : float.MaxValue;
            if (moved > 3f)
            {
                cache.Direction = dir;
                cache.Valid = true;
                cache.Lake = _tracked.PointsProvider.GetElevationAtPoint(dir) - PlanetRadius < LakeTerrainPatch.SeaLevel - 0.3f;
            }
            return cache.Lake;
        }

        internal static Immersion Measure(Vector3Double origin, Quaternion rotation, Vector3 center, Vector3 extents)
        {
            float sea = SeaRadius;
            float low = float.MaxValue, high = float.MinValue;
            var depths = new float[8];
            var corners = new Vector3[8];
            int n = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 local = center + Vector3.Scale(extents, new Vector3(x, y, z));
                float altitude = (float)((origin + (Vector3Double)(rotation * local)).Magnitude - sea);
                corners[n] = local;
                depths[n] = -altitude;
                low = Mathf.Min(low, altitude);
                high = Mathf.Max(high, altitude);
                n++;
            }
            float height = Mathf.Max(0.5f, high - low);
            var result = new Immersion
            {
                Height = height,
                Fraction = Mathf.Clamp01(-low / height),
                LocalCenter = center,
                Radius = Mathf.Max(1f, new Vector2(extents.x, extents.z).magnitude * 0.8f),
            };
            Vector3 weighted = Vector3.zero;
            float total = 0f;
            for (int i = 0; i < 8; i++)
            {
                float w = Mathf.Clamp(depths[i], 0f, height);
                weighted += corners[i] * w;
                total += w;
            }
            if (total > 0.001f)
            {
                result.LocalCenter = weighted / total;
            }
            result.HullCenter = origin + (Vector3Double)(rotation * center);
            Vector3Double up = result.HullCenter.Normalized;
            result.Up = (Vector3)up;
            result.SurfacePoint = up * sea;
            return result;
        }
    }
}
