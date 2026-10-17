using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mining.Content
{
    internal static class ZFight
    {
        private const float Step = 0.0015f;

        internal static void Separate(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var buckets = new Dictionary<(int, int, int, int), List<int>>();
            for (int tri = 0; tri < t.Count / 3; tri++)
            {
                Vector3 a = v[t[3 * tri]], b = v[t[3 * tri + 1]], c = v[t[3 * tri + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    continue;
                }
                normal.Normalize();
                var key = (Mathf.RoundToInt(normal.x * 1000f), Mathf.RoundToInt(normal.y * 1000f), Mathf.RoundToInt(normal.z * 1000f), Mathf.RoundToInt(Vector3.Dot(normal, a) * 10000f));
                if (!buckets.TryGetValue(key, out List<int> list))
                {
                    buckets[key] = list = new List<int>();
                }
                list.Add(tri);
            }
            var moves = new Dictionary<int, Vector3>();
            foreach (var pair in buckets)
            {
                List<int> members = pair.Value;
                if (members.Count < 2)
                {
                    continue;
                }
                Vector3 normal = new Vector3(pair.Key.Item1, pair.Key.Item2, pair.Key.Item3).normalized;
                Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.x) < 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 w = Vector3.Cross(normal, u);
                var parent = members.ToDictionary(m => m, m => m);
                int Find(int x)
                {
                    while (parent[x] != x)
                    {
                        parent[x] = parent[parent[x]];
                        x = parent[x];
                    }
                    return x;
                }
                var owner = new Dictionary<int, int>();
                foreach (int tri in members)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = t[3 * tri + k];
                        if (owner.TryGetValue(vi, out int other)) parent[Find(tri)] = Find(other);
                        else owner[vi] = tri;
                    }
                }
                var islands = members.GroupBy(Find).Select(g => g.ToList()).OrderBy(g => g.Min()).ToList();
                if (islands.Count < 2)
                {
                    continue;
                }
                var flat = islands.Select(g => g.Select(tri => new[]
                {
                    Flat(v[t[3 * tri]], u, w), Flat(v[t[3 * tri + 1]], u, w), Flat(v[t[3 * tri + 2]], u, w),
                }).ToList()).ToList();
                var level = new int[islands.Count];
                for (int i = 1; i < islands.Count; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (level[j] + 1 > level[i] && flat[i].Any(p => flat[j].Any(q => Overlap(p, q))))
                        {
                            level[i] = level[j] + 1;
                        }
                    }
                    if (level[i] > 0)
                    {
                        foreach (int tri in islands[i]) moves[tri] = normal * (level[i] * Step);
                    }
                }
            }
            foreach (var move in moves)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vi = t[3 * move.Key + k];
                    v.Add(v[vi] + move.Value);
                    n.Add(n[vi]);
                    uv.Add(uv[vi]);
                    t[3 * move.Key + k] = v.Count - 1;
                }
            }
        }

        private static Vector2 Flat(Vector3 p, Vector3 u, Vector3 w) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w));

        private static bool Overlap(Vector2[] p, Vector2[] q)
        {
            return !Separated(p, p, q) && !Separated(q, p, q);
        }

        private static bool Separated(Vector2[] edges, Vector2[] p, Vector2[] q)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = edges[i], b = edges[(i + 1) % 3];
                var axis = new Vector2(b.y - a.y, a.x - b.x);
                float p0 = float.MaxValue, p1 = float.MinValue, q0 = float.MaxValue, q1 = float.MinValue;
                foreach (Vector2 x in p)
                {
                    float d = Vector2.Dot(x, axis);
                    p0 = Mathf.Min(p0, d);
                    p1 = Mathf.Max(p1, d);
                }
                foreach (Vector2 x in q)
                {
                    float d = Vector2.Dot(x, axis);
                    q0 = Mathf.Min(q0, d);
                    q1 = Mathf.Max(q1, d);
                }
                if (p1 <= q0 + 1e-5f || q1 <= p0 + 1e-5f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
