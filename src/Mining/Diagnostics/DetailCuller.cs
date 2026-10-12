using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mining.Diagnostics
{
    internal static class DetailCuller
    {
        private const float RefreshSeconds = 4f, CheckSeconds = 0.2f, SmallSize = 0.7f;

        private static readonly HashSet<string> CableParts = new HashSet<string> { "Plug", "CordBase", "CordScroll" };
        private static readonly List<Renderer> Tracked = new List<Renderer>();
        private static readonly HashSet<int> Known = new HashSet<int>();
        private static float _nextRefresh, _nextCheck;
        private static int _cursor;

        internal static void Update()
        {
            float distance = MiningMod.DetailDistance.Value;
            if (distance <= 0f)
            {
                if (Tracked.Count > 0) Reset();
                return;
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            float now = Time.unscaledTime;
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + RefreshSeconds;
                Refresh();
            }
            if (now < _nextCheck || Tracked.Count == 0) return;
            _nextCheck = now + CheckSeconds;
            Vector3 eye = cam.transform.position;
            float limit = distance * distance;
            int batch = Mathf.Max(200, Tracked.Count / 2);
            for (int k = 0; k < batch && Tracked.Count > 0; k++)
            {
                if (_cursor >= Tracked.Count) _cursor = 0;
                Renderer r = Tracked[_cursor];
                if (r == null)
                {
                    Tracked.RemoveAt(_cursor);
                    continue;
                }
                bool far = r.bounds.SqrDistance(eye) > limit;
                if (r.forceRenderingOff != far) r.forceRenderingOff = far;
                _cursor++;
            }
        }

        private static void Refresh()
        {
            bool noShadows = !MiningMod.DetailShadows.Value;
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                if (r is ParticleSystemRenderer || Known.Contains(r.GetInstanceID())) continue;
                Known.Add(r.GetInstanceID());
                string root = r.transform.root.name;
                if (!root.StartsWith("ship", System.StringComparison.OrdinalIgnoreCase)) continue;
                bool cable = CableParts.Contains(r.name);
                bool small = r.bounds.size.sqrMagnitude < SmallSize * SmallSize;
                if (!cable && !small) continue;
                if (noShadows && r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
                Tracked.Add(r);
            }
        }

        private static void Reset()
        {
            foreach (Renderer r in Tracked)
            {
                if (r != null) r.forceRenderingOff = false;
            }
            Tracked.Clear();
            Known.Clear();
        }
    }
}
