using System.Collections.Generic;
using UnityEngine;
using Builder = Flora.Models.LowPolyMeshes.Builder;

namespace Flora.Models
{
    internal static class ForestMeshes
    {
        private static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

        private static Vector3 Flat(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        private static void Limb(Builder b, int side, int cap, int core, Vector3[] pts, float[] radii, int sides, System.Random rng, float jitter,
            bool capStart, bool capEnd, float splinterStart = 0f, float splinterEnd = 0f)
        {
            Vector3 first = (pts[1] - pts[0]).normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(first, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            var rings = new Vector3[pts.Length][];
            for (int k = 0; k < pts.Length; k++)
            {
                Vector3 dir = k == 0 ? pts[1] - pts[0] : k == pts.Length - 1 ? pts[k] - pts[k - 1] : pts[k + 1] - pts[k - 1];
                dir.Normalize();
                Vector3 u = Vector3.Cross(dir, reference).normalized;
                Vector3 v = Vector3.Cross(dir, u);
                rings[k] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = phase + i * Mathf.PI * 2f / sides;
                    float r = radii[k] * (1f + Range(rng, -jitter, jitter));
                    rings[k][i] = pts[k] + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
                }
            }
            for (int k = 0; k < pts.Length - 1; k++)
            {
                Vector3 inside = (pts[k] + pts[k + 1]) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    b.OutwardTriangle(side, inside, rings[k][i], rings[k + 1][i], rings[k + 1][n]);
                    b.OutwardTriangle(side, inside, rings[k][i], rings[k + 1][n], rings[k][n]);
                }
            }
            int last = pts.Length - 1;
            if (capStart)
            {
                Cap(b, side, cap, core, rings[0], pts[0], (pts[0] - pts[1]).normalized, radii[0], splinterStart, rng);
            }
            if (capEnd)
            {
                Cap(b, side, cap, core, rings[last], pts[last], (pts[last] - pts[last - 1]).normalized, radii[last], splinterEnd, rng);
            }
        }

        private static void Cap(Builder b, int side, int cap, int core, Vector3[] ring, Vector3 center, Vector3 outward, float radius, float splinter,
            System.Random rng)
        {
            int sides = ring.Length;
            Vector3 inside = center - outward * radius;
            var inner = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float push = splinter > 0f ? splinter * radius * Range(rng, 0f, 1f) : 0f;
                inner[i] = center + (ring[i] - center) * 0.8f + outward * push;
            }
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                b.OutwardTriangle(side, inside, ring[i], ring[n], inner[n]);
                b.OutwardTriangle(side, inside, ring[i], inner[n], inner[i]);
            }
            if (splinter > 0f || core < 0)
            {
                Vector3 tip = center + outward * splinter * radius * Range(rng, 0.6f, 1.3f);
                for (int i = 0; i < sides; i++)
                {
                    b.OutwardTriangle(cap, inside, inner[i], inner[(i + 1) % sides], tip);
                }
                return;
            }
            var heart = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                heart[i] = center + (ring[i] - center) * Range(rng, 0.3f, 0.4f);
            }
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                b.OutwardTriangle(cap, inside, inner[i], inner[n], heart[n]);
                b.OutwardTriangle(cap, inside, inner[i], heart[n], heart[i]);
                b.OutwardTriangle(core, inside, heart[i], heart[n], center);
            }
        }

        internal static Mesh Log(int seed, float length, float radius, bool mossy, bool broken, int stubs)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 4 : 3);
            const int count = 4;
            var pts = new Vector3[count];
            var radii = new float[count];
            float bendY = Range(rng, -0.06f, 0.06f) * radius;
            float bendZ = Range(rng, -0.12f, 0.12f) * length;
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)(count - 1);
                float arc = Mathf.Sin(t * Mathf.PI);
                pts[k] = new Vector3(-length * 0.5f + length * t, radius * 0.7f + bendY * arc, bendZ * arc);
                radii[k] = radius * Mathf.Lerp(1.05f, 0.85f, t);
            }
            Limb(b, 0, 1, 2, pts, radii, 7, rng, 0.08f, true, true, 0f, broken ? 1.1f : 0f);
            for (int s = 0; s < stubs; s++)
            {
                float t = Range(rng, 0.2f, 0.75f);
                Vector3 axis = Vector3.Lerp(pts[0], pts[count - 1], t) + new Vector3(0f, 0f, bendZ * Mathf.Sin(t * Mathf.PI));
                var dir = new Vector3(Range(rng, -0.3f, 0.3f), Range(rng, 0.3f, 0.9f), s % 2 == 0 ? 1f : -1f).normalized;
                float r = radius * Range(rng, 0.22f, 0.32f);
                float len = radius * Range(rng, 0.9f, 1.8f);
                Limb(b, 0, 1, -1, new[] { axis + dir * radius * 0.6f, axis + dir * (radius + len) }, new[] { r, r * 0.55f }, 5, rng, 0.1f,
                    false, true, 0f, 0.8f);
            }
            if (mossy)
            {
                int patches = 2 + rng.Next(2);
                for (int m = 0; m < patches; m++)
                {
                    float t = Range(rng, 0.15f, 0.85f);
                    Vector3 axis = Vector3.Lerp(pts[0], pts[count - 1], t) + new Vector3(0f, 0f, bendZ * Mathf.Sin(t * Mathf.PI));
                    LowPolyMeshes.Blob(b, 3, axis + Vector3.up * radius * 0.78f,
                        new Vector3(length * Range(rng, 0.08f, 0.15f), radius * 0.28f, radius * 0.75f), rng, 0.2f);
                }
            }
            return b.Build($"FloraLog_{seed}");
        }

        internal static Mesh Stump(int seed, float radius, float height, bool jagged, bool mossy)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 4 : 3);
            var lean = new Vector3(Range(rng, -0.08f, 0.08f), 0f, Range(rng, -0.08f, 0.08f)) * height;
            Limb(b, 0, 1, 2, new[] { new Vector3(0f, -0.2f, 0f), Vector3.up * height * 0.45f + lean * 0.5f, Vector3.up * height + lean },
                new[] { radius * 1.2f, radius, radius * 0.95f }, 8, rng, 0.07f, false, true, 0f, jagged ? 1.6f : 0f);
            int roots = 4 + rng.Next(2);
            float start = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < roots; i++)
            {
                Vector3 d = Flat(start + i * Mathf.PI * 2f / roots + Range(rng, -0.3f, 0.3f));
                float reach = Range(rng, 1.6f, 2.3f);
                Limb(b, 0, 1, -1,
                    new[] { d * radius * 0.6f + Vector3.up * height * 0.35f, d * radius * 1.25f + Vector3.up * height * 0.06f, d * radius * reach - Vector3.up * 0.15f },
                    new[] { radius * 0.36f, radius * 0.22f, radius * 0.08f }, 5, rng, 0.1f, false, false);
            }
            if (mossy)
            {
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                LowPolyMeshes.Blob(b, 3, d * radius * 0.95f + Vector3.up * height * 0.3f, new Vector3(radius * 0.35f, height * 0.3f, radius * 0.35f), rng, 0.2f);
                if (!jagged)
                {
                    LowPolyMeshes.Blob(b, 3, Vector3.up * height + lean + d * radius * 0.25f, new Vector3(radius * 0.45f, radius * 0.1f, radius * 0.4f), rng, 0.2f);
                }
            }
            return b.Build($"FloraStump_{seed}");
        }

        internal static Mesh Branch(int seed, float length)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            float r = Range(rng, 0.035f, 0.06f);
            float bend = Range(rng, -0.15f, 0.15f) * length;
            var pts = new[]
            {
                new Vector3(-length * 0.5f, r * 0.8f, 0f), new Vector3(0f, r * 0.8f, bend), new Vector3(length * 0.5f, r * 0.7f, bend * 0.3f),
            };
            Limb(b, 0, 1, -1, pts, new[] { r, r * 0.8f, r * 0.5f }, 5, rng, 0.1f, true, true, 0.8f, 0f);
            int twigs = 1 + rng.Next(3);
            for (int i = 0; i < twigs; i++)
            {
                float t = Range(rng, 0.3f, 0.85f);
                Vector3 basePos = t < 0.5f ? Vector3.Lerp(pts[0], pts[1], t * 2f) : Vector3.Lerp(pts[1], pts[2], t * 2f - 1f);
                float side = i % 2 == 0 ? 1f : -1f;
                var dir = new Vector3(1f, 0f, side * Range(rng, 0.5f, 1.1f)).normalized;
                float len = length * Range(rng, 0.18f, 0.35f);
                Vector3 end = basePos + dir * len;
                end.y = r * 0.4f;
                Limb(b, 0, 1, -1, new[] { basePos, end }, new[] { r * 0.55f, r * 0.3f }, 4, rng, 0.1f, false, true);
            }
            return b.Build($"FloraBranch_{seed}");
        }

        internal static Mesh Rock(int seed, float size, bool mossy)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 2 : 1);
            LowPolyMeshes.Blob(b, 0, new Vector3(0f, size * 0.2f, 0f), new Vector3(size, size * 0.55f, size * 0.8f), rng, 0.28f);
            if (rng.NextDouble() < 0.6)
            {
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                float s = size * Range(rng, 0.35f, 0.5f);
                LowPolyMeshes.Blob(b, 0, d * size * 0.9f + Vector3.up * s * 0.2f, new Vector3(s, s * 0.7f, s * 0.9f), rng, 0.25f);
            }
            if (mossy)
            {
                LowPolyMeshes.Blob(b, 1, new Vector3(Range(rng, -0.2f, 0.2f) * size, size * 0.66f, Range(rng, -0.15f, 0.15f) * size),
                    new Vector3(size * 0.65f, size * 0.13f, size * 0.5f), rng, 0.2f);
            }
            return b.Build($"FloraRock_{seed}");
        }

        internal static Mesh Pebbles(int seed, int count, float spread)
        {
            var rng = new System.Random(seed);
            var b = new Builder(1);
            for (int i = 0; i < count; i++)
            {
                float s = Range(rng, 0.05f, 0.16f);
                Vector3 p = Flat(Range(rng, 0f, Mathf.PI * 2f)) * Range(rng, 0f, spread);
                LowPolyMeshes.Blob(b, 0, p + Vector3.up * s * 0.25f, new Vector3(s, s * 0.6f, s * 0.8f), rng, 0.25f);
            }
            return b.Build($"FloraPebbles_{seed}");
        }

        internal static Mesh Mushrooms(int seed, int count, float height)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            for (int m = 0; m < count; m++)
            {
                Vector3 basePos = m == 0 ? Vector3.zero : Flat(Range(rng, 0f, Mathf.PI * 2f)) * Range(rng, 0.05f, 0.16f);
                float h = height * Range(rng, 0.5f, 1.1f) * (m == 0 ? 1.2f : 1f);
                var lean = new Vector3(Range(rng, -0.2f, 0.2f), 0f, Range(rng, -0.2f, 0.2f)) * h;
                float sr = h * Range(rng, 0.12f, 0.17f);
                Vector3 top = basePos + Vector3.up * h + lean;
                Limb(b, 0, 0, -1, new[] { basePos - Vector3.up * 0.02f, basePos + Vector3.up * h * 0.5f + lean * 0.4f, top },
                    new[] { sr * 1.3f, sr, sr * 0.9f }, 5, rng, 0.08f, false, false);
                float rc = h * Range(rng, 0.45f, 0.65f);
                const int sides = 7;
                var outer = new Vector3[sides];
                var mid = new Vector3[sides];
                float phase = Range(rng, 0f, Mathf.PI * 2f);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 d = Flat(phase + i * Mathf.PI * 2f / sides);
                    outer[i] = top + d * rc * Range(rng, 0.92f, 1.08f) - Vector3.up * rc * 0.15f;
                    mid[i] = top + d * rc * 0.65f + Vector3.up * rc * 0.35f;
                }
                Vector3 apex = top + Vector3.up * rc * 0.55f;
                Vector3 under = top - Vector3.up * rc * 0.05f;
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    b.OutwardTriangle(1, top, outer[i], outer[n], mid[n]);
                    b.OutwardTriangle(1, top, outer[i], mid[n], mid[i]);
                    b.OutwardTriangle(1, top, mid[i], mid[n], apex);
                    b.OutwardTriangle(0, top + Vector3.up * rc, outer[i], outer[n], under);
                }
            }
            return b.Build($"FloraMushrooms_{seed}");
        }

        internal static Mesh Fern(int seed, int fronds, float length)
        {
            var rng = new System.Random(seed);
            var b = new Builder(1);
            const int steps = 7;
            float start = Range(rng, 0f, Mathf.PI * 2f);
            for (int f = 0; f < fronds; f++)
            {
                Vector3 d = Flat(start + f * Mathf.PI * 2f / fronds + Range(rng, -0.25f, 0.25f));
                float len = length * Range(rng, 0.75f, 1.1f);
                float lift = Range(rng, 0.55f, 0.85f);
                var pts = new Vector3[steps];
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)(steps - 1);
                    pts[k] = d * len * t * 0.95f + Vector3.up * len * lift * (1.6f * t - 1.15f * t * t);
                }
                for (int k = 0; k < steps - 1; k++)
                {
                    Vector3 tangent = pts[k + 1] - pts[k];
                    Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
                    b.DoubleTriangle(0, pts[k], pts[k + 1], pts[k] + side * 0.012f);
                    if (k == 0)
                    {
                        continue;
                    }
                    float t = k / (float)(steps - 1);
                    float leaf = len * 0.2f * (1f - t * 0.75f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 tip = pts[k] + side * s * leaf + tangent.normalized * leaf * 0.35f - Vector3.up * leaf * 0.15f;
                        b.DoubleTriangle(0, pts[k], pts[k] + tangent * 0.75f, tip);
                    }
                }
            }
            return b.Build($"FloraFern_{seed}", upNormals: true);
        }

        internal static Mesh GrassTuft(int seed, int blades, float height)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            for (int i = 0; i < blades; i++)
            {
                Vector3 basePos = Flat(Range(rng, 0f, Mathf.PI * 2f)) * Range(rng, 0f, 0.12f);
                Vector3 facing = Flat(Range(rng, 0f, Mathf.PI * 2f));
                Vector3 perp = Vector3.Cross(Vector3.up, facing);
                float h = height * Range(rng, 0.6f, 1.1f);
                Vector3 lean = (basePos.normalized * 0.7f + facing * 0.3f) * h * Range(rng, 0.15f, 0.4f);
                float w = Range(rng, 0.022f, 0.038f);
                int sub = rng.NextDouble() < 0.3 ? 1 : 0;
                Vector3 m = basePos + Vector3.up * h * 0.55f + lean * 0.3f;
                Vector3 tip = basePos + Vector3.up * h + lean;
                Vector3 b0 = basePos - perp * w, b1 = basePos + perp * w, m0 = m - perp * w * 0.6f, m1 = m + perp * w * 0.6f;
                b.DoubleTriangle(sub, b0, b1, m1);
                b.DoubleTriangle(sub, b0, m1, m0);
                b.DoubleTriangle(sub, m0, m1, tip);
            }
            return b.Build($"FloraTuft_{seed}", upNormals: true);
        }

        internal static Mesh TallFlowers(int seed, int stems, float height)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            for (int s = 0; s < stems; s++)
            {
                Vector3 basePos = s == 0 ? Vector3.zero : Flat(Range(rng, 0f, Mathf.PI * 2f)) * Range(rng, 0.06f, 0.2f);
                float h = height * Range(rng, 0.7f, 1.15f);
                var lean = new Vector3(Range(rng, -0.1f, 0.1f), 0f, Range(rng, -0.1f, 0.1f)) * h;
                Vector3 top = basePos + Vector3.up * h + lean;
                Limb(b, 0, 0, -1, new[] { basePos, top }, new[] { 0.012f, 0.008f }, 3, rng, 0f, false, false);
                for (int l = 0; l < 2; l++)
                {
                    Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                    Vector3 perp = Vector3.Cross(Vector3.up, d) * 0.02f;
                    b.OutlinedTriangle(0, basePos + perp, basePos - perp, basePos + d * h * 0.3f + Vector3.up * h * 0.35f);
                }
                int bells = 6 + rng.Next(4);
                for (int k = 0; k < bells; k++)
                {
                    float f = Mathf.Lerp(0.55f, 1f, k / (float)(bells - 1));
                    Vector3 p = Vector3.Lerp(basePos, top, f);
                    Vector3 d = Flat(k * 2.4f + s);
                    Vector3 perp = Vector3.Cross(Vector3.up, d);
                    float size = 0.03f * (1.15f - f * 0.5f);
                    Vector3 c = p + d * size * 0.6f;
                    b.OutlinedTriangle(1, p + Vector3.up * size * 0.5f, c + perp * size * 0.55f, c - perp * size * 0.55f);
                    b.OutlinedTriangle(1, c + perp * size * 0.55f, c - perp * size * 0.55f, p + d * size * 1.1f - Vector3.up * size * 0.6f);
                }
            }
            return b.Build($"FloraTallFlowers_{seed}", upNormals: true);
        }

        internal static Mesh Bush(int seed, float size, bool berries)
        {
            var rng = new System.Random(seed);
            var b = new Builder(berries ? 2 : 1);
            int blobs = 3 + rng.Next(3);
            var centers = new List<Vector3>();
            for (int i = 0; i < blobs; i++)
            {
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                float dist = i == 0 ? 0f : size * Range(rng, 0.25f, 0.5f);
                float r = size * Range(rng, 0.45f, 0.65f) * (i == 0 ? 1.15f : 1f);
                var c = d * dist + Vector3.up * r * 0.7f;
                centers.Add(c);
                LowPolyMeshes.Blob(b, 0, c, new Vector3(r, r * 0.8f, r), rng, 0.18f);
            }
            if (berries)
            {
                int count = 6 + rng.Next(6);
                for (int i = 0; i < count; i++)
                {
                    Vector3 c = centers[rng.Next(centers.Count)];
                    var dir = new Vector3(Range(rng, -1f, 1f), Range(rng, 0f, 0.9f), Range(rng, -1f, 1f)).normalized;
                    LowPolyMeshes.Blob(b, 1, c + dir * size * 0.48f, Vector3.one * size * 0.07f, rng, 0.1f);
                }
            }
            return b.Build($"FloraBush_{seed}", softCenter: new Vector3(0f, size * 0.6f, 0f));
        }
    }
}
