using System.Collections.Generic;
using Core.Services;
using Ships;
using Ships.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mining.Diagnostics
{
    internal static class DetailCuller
    {
        private const float RefreshSeconds = 1f, CheckSeconds = 0.2f, SmallSize = 0.7f;
        private const int ScansPerRefresh = 2;

        private sealed class ShipStamp
        {
            public int Scanned = int.MinValue;
            public int Seen = int.MinValue;
        }

        private static readonly HashSet<string> CableParts = new HashSet<string> { "Plug", "CordBase", "CordScroll" };
        private static readonly List<Renderer> Tracked = new List<Renderer>();
        private static readonly HashSet<Renderer> TrackedSet = new HashSet<Renderer>();
        private static readonly Dictionary<Transform, ShipStamp> Stamps = new Dictionary<Transform, ShipStamp>();
        private static readonly List<Renderer> Buffer = new List<Renderer>();
        private static readonly List<Transform> Stale = new List<Transform>();
        private static float _nextRefresh, _nextCheck;
        private static int _cursor;

        internal static void Update()
        {
            float distance = MiningMod.DetailDistance.Value;
            if (distance <= 0f)
            {
                if (Tracked.Count > 0 || Stamps.Count > 0) Reset();
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
                    TrackedSet.Remove(r);
                    Tracked[_cursor] = Tracked[Tracked.Count - 1];
                    Tracked.RemoveAt(Tracked.Count - 1);
                    continue;
                }
                bool far = r.bounds.SqrDistance(eye) > limit;
                if (r.forceRenderingOff != far) r.forceRenderingOff = far;
                _cursor++;
            }
        }

        private static void Refresh()
        {
            var tracker = ServiceCache.Get<ShipsClientTracker>();
            if (tracker == null)
            {
                return;
            }
            int scans = 0;
            foreach (TrackedShipClient ship in tracker.VisuallyLoadedShip)
            {
                if (ship == null || ship.VisualObject == null)
                {
                    continue;
                }
                Transform root = ship.VisualObject.transform;
                int signature = ship.PartCount * 7919 + root.childCount;
                if (!Stamps.TryGetValue(root, out ShipStamp stamp))
                {
                    Stamps[root] = stamp = new ShipStamp();
                }
                bool settled = stamp.Seen == signature;
                stamp.Seen = signature;
                if (!settled || stamp.Scanned == signature || scans >= ScansPerRefresh)
                {
                    continue;
                }
                stamp.Scanned = signature;
                scans++;
                Scan(root);
            }
            foreach (Transform root in Stamps.Keys)
            {
                if (root == null) Stale.Add(root);
            }
            foreach (Transform root in Stale)
            {
                Stamps.Remove(root);
            }
            Stale.Clear();
        }

        private static void Scan(Transform root)
        {
            bool noShadows = !MiningMod.DetailShadows.Value;
            root.GetComponentsInChildren(true, Buffer);
            foreach (Renderer r in Buffer)
            {
                if (r is ParticleSystemRenderer || TrackedSet.Contains(r)) continue;
                bool cable = CableParts.Contains(r.name);
                bool small = r.bounds.size.sqrMagnitude < SmallSize * SmallSize;
                if (!cable && !small) continue;
                if (noShadows && r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
                Tracked.Add(r);
                TrackedSet.Add(r);
            }
            Buffer.Clear();
        }

        private static void Reset()
        {
            foreach (Renderer r in Tracked)
            {
                if (r != null) r.forceRenderingOff = false;
            }
            Tracked.Clear();
            TrackedSet.Clear();
            Stamps.Clear();
        }
    }
}
