using System.Collections.Generic;
using UnityEngine;
using Builder = Flora.Models.LowPolyMeshes.Builder;

namespace Flora.Models
{
    internal static class ForestMeshes
    {
        private const float BarkTile = 1.1f;

        private static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

        private static Vector3 Flat(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        private static void Limb(Builder b, int side, int cap, Vector3[] pts, float[] radii, int sides, System.Random rng, float jitter,
            bool capStart, bool capEnd, float splinterStart = 0f, float splinterEnd = 0f, bool bark = true)
        {
            Vector3 first = (pts[1] - pts[0]).normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(first, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            float phase = Range(rng, 0f, Mathf.PI * 2f);
            float uRepeat = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * radii[0] / BarkTile));
            float vPerMeter = uRepeat / (2f * Mathf.PI * radii[0]);
            int last = pts.Length - 1;
            var rings = new int[pts.Length][];
            var ringPos = new Vector3[pts.Length][];
            Vector3 startU = Vector3.zero, startV = Vector3.zero, endU = Vector3.zero, endV = Vector3.zero;
            float along = 0f;
            for (int k = 0; k < pts.Length; k++)
            {
                if (k > 0)
                {
                    along += Vector3.Distance(pts[k], pts[k - 1]);
                }
                Vector3 dir = k == 0 ? pts[1] - pts[0] : k == last ? pts[k] - pts[k - 1] : pts[k + 1] - pts[k - 1];
                dir.Normalize();
                Vector3 u = Vector3.Cross(dir, reference).normalized;
                Vector3 v = Vector3.Cross(dir, u);
                if (k == 0)
                {
                    startU = u;
                    startV = v;
                }
                if (k == last)
                {
                    endU = u;
                    endV = v;
                }
                var offsets = new float[sides];
                for (int i = 0; i < sides; i++)
                {
                    offsets[i] = Range(rng, -jitter, jitter);
                }
                ringPos[k] = new Vector3[sides];
                rings[k] = new int[sides + 1];
                for (int i = 0; i <= sides; i++)
                {
                    float a = phase + i * Mathf.PI * 2f / sides;
                    Vector3 radial = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                    Vector3 pos = pts[k] + radial * radii[k] * (1f + offsets[i % sides]);
                    if (i < sides)
                    {
                        ringPos[k][i] = pos;
                    }
                    Vector2 uv = bark ? new Vector2(i / (float)sides * uRepeat, along * vPerMeter) : Builder.UvPlain;
                    rings[k][i] = b.Vertex(pos, radial, uv);
                }
            }
            for (int k = 0; k < last; k++)
            {
                Vector3 inside = (pts[k] + pts[k + 1]) * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    b.OutwardIndexed(side, inside, rings[k][i], rings[k + 1][i], rings[k + 1][i + 1]);
                    b.OutwardIndexed(side, inside, rings[k][i], rings[k + 1][i + 1], rings[k][i + 1]);
                }
            }
            if (capStart)
            {
                Cap(b, cap, ringPos[0], pts[0], (pts[0] - pts[1]).normalized, radii[0], splinterStart, rng, startU, startV);
            }
            if (capEnd)
            {
                Cap(b, cap, ringPos[last], pts[last], (pts[last] - pts[last - 1]).normalized, radii[last], splinterEnd, rng, endU, endV);
            }
        }

        private static void Cap(Builder b, int cap, Vector3[] ring, Vector3 center, Vector3 outward, float radius, float splinter, System.Random rng,
            Vector3 u, Vector3 v)
        {
            Vector2 Uv(Vector3 p)
            {
                Vector3 d = p - center;
                return new Vector2(0.5f + Vector3.Dot(d, u) / (radius * 2.1f), 0.5f + Vector3.Dot(d, v) / (radius * 2.1f));
            }

            int sides = ring.Length;
            Vector3 inside = center - outward * radius;
            if (splinter <= 0f)
            {
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = ring[i], c = ring[(i + 1) % sides];
                    b.OutwardTriangle(cap, inside, a, c, center, Uv(a), Uv(c), Uv(center));
                }
                return;
            }
            var inner = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                inner[i] = center + (ring[i] - center) * 0.75f + outward * splinter * radius * Range(rng, 0.1f, 1f);
            }
            Vector3 tip = center + outward * splinter * radius * Range(rng, 0.8f, 1.5f);
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                b.OutwardTriangle(cap, inside, ring[i], ring[n], inner[n], Uv(ring[i]), Uv(ring[n]), Uv(inner[n]));
                b.OutwardTriangle(cap, inside, ring[i], inner[n], inner[i], Uv(ring[i]), Uv(inner[n]), Uv(inner[i]));
                b.OutwardTriangle(cap, inside, inner[i], inner[n], tip, Uv(inner[i]), Uv(inner[n]), Uv(tip));
            }
        }

        private static Vector3 AlongCurve(Vector3[] pts, float t)
        {
            float f = t * (pts.Length - 1);
            int i = Mathf.Min((int)f, pts.Length - 2);
            return Vector3.Lerp(pts[i], pts[i + 1], f - i);
        }

        internal static Mesh Log(int seed, float length, float radius, bool mossy, bool broken, int stubs)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 3 : 2);
            const int count = 6;
            var pts = new Vector3[count];
            var radii = new float[count];
            float bendY = Range(rng, -0.06f, 0.06f) * radius;
            float bendZ = Range(rng, -0.1f, 0.1f) * length;
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)(count - 1);
                float arc = Mathf.Sin(t * Mathf.PI);
                pts[k] = new Vector3(-length * 0.5f + length * t, radius * 0.72f + bendY * arc, bendZ * arc);
                radii[k] = radius * Mathf.Lerp(1.05f, 0.85f, t) * (k == 0 ? 1.08f : 1f);
            }
            Limb(b, 0, 1, pts, radii, 12, rng, 0.04f, true, true, 0f, broken ? 1.1f : 0f);
            for (int s = 0; s < stubs; s++)
            {
                float t = Range(rng, 0.2f, 0.75f);
                Vector3 axis = AlongCurve(pts, t);
                var dir = new Vector3(Range(rng, -0.3f, 0.3f), Range(rng, 0.3f, 0.9f), s % 2 == 0 ? 1f : -1f).normalized;
                float r = radius * Range(rng, 0.2f, 0.28f);
                float len = radius * Range(rng, 0.9f, 1.8f);
                Limb(b, 0, 1, new[] { axis + dir * radius * 0.5f, axis + dir * (radius + len * 0.5f), axis + dir * (radius + len) },
                    new[] { r, r * 0.8f, r * 0.55f }, 7, rng, 0.05f, false, true, 0f, 0.8f);
            }
            if (mossy)
            {
                b.ProjectScale = 2f;
                int patches = 2 + rng.Next(2);
                for (int m = 0; m < patches; m++)
                {
                    Vector3 axis = AlongCurve(pts, Range(rng, 0.15f, 0.85f));
                    LowPolyMeshes.Blob(b, 2, axis + Vector3.up * radius * 0.8f,
                        new Vector3(length * Range(rng, 0.08f, 0.14f), radius * 0.22f, radius * 0.72f), rng, 0.15f, smooth: true);
                }
            }
            return b.Build($"FloraLog_{seed}");
        }

        internal static Mesh Stump(int seed, float radius, float height, bool jagged, bool mossy)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 3 : 2);
            var lean = new Vector3(Range(rng, -0.06f, 0.06f), 0f, Range(rng, -0.06f, 0.06f)) * height;
            Limb(b, 0, 1,
                new[] { new Vector3(0f, -0.2f, 0f), Vector3.up * height * 0.2f + lean * 0.2f, Vector3.up * height * 0.6f + lean * 0.6f, Vector3.up * height + lean },
                new[] { radius * 1.25f, radius * 1.08f, radius, radius * 0.96f }, 14, rng, 0.04f, false, true, 0f, jagged ? 1.6f : 0f);
            int roots = 4 + rng.Next(2);
            float start = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < roots; i++)
            {
                Vector3 d = Flat(start + i * Mathf.PI * 2f / roots + Range(rng, -0.3f, 0.3f));
                float reach = Range(rng, 1.6f, 2.3f);
                Limb(b, 0, 1,
                    new[]
                    {
                        d * radius * 0.6f + Vector3.up * height * 0.3f, d * radius * 1.1f + Vector3.up * height * 0.1f,
                        d * radius * 1.5f, d * radius * reach - Vector3.up * 0.15f,
                    },
                    new[] { radius * 0.36f, radius * 0.26f, radius * 0.16f, radius * 0.07f }, 8, rng, 0.05f, false, false);
            }
            if (mossy)
            {
                b.ProjectScale = 2f;
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                LowPolyMeshes.Blob(b, 2, d * radius * 0.95f + Vector3.up * height * 0.3f, new Vector3(radius * 0.35f, height * 0.3f, radius * 0.35f), rng, 0.15f,
                    smooth: true);
                if (!jagged)
                {
                    LowPolyMeshes.Blob(b, 2, Vector3.up * height + lean + d * radius * 0.25f, new Vector3(radius * 0.45f, radius * 0.08f, radius * 0.4f), rng, 0.15f,
                        smooth: true);
                }
            }
            return b.Build($"FloraStump_{seed}");
        }

        internal static Mesh Branch(int seed, float length)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            float r = Range(rng, 0.035f, 0.055f);
            float bend = Range(rng, -0.12f, 0.12f) * length;
            var pts = new Vector3[5];
            for (int k = 0; k < pts.Length; k++)
            {
                float t = k / (float)(pts.Length - 1);
                pts[k] = new Vector3(-length * 0.5f + length * t, r * Mathf.Lerp(0.85f, 0.7f, t), bend * Mathf.Sin(t * Mathf.PI) + Range(rng, -0.02f, 0.02f));
            }
            Limb(b, 0, 1, pts, new[] { r, r * 0.9f, r * 0.78f, r * 0.62f, r * 0.45f }, 7, rng, 0.04f, true, true, 0.7f, 0f);
            int twigs = 1 + rng.Next(3);
            for (int i = 0; i < twigs; i++)
            {
                Vector3 basePos = AlongCurve(pts, Range(rng, 0.3f, 0.85f));
                float side = i % 2 == 0 ? 1f : -1f;
                var dir = new Vector3(1f, 0f, side * Range(rng, 0.5f, 1.1f)).normalized;
                float len = length * Range(rng, 0.18f, 0.35f);
                Vector3 end = basePos + dir * len;
                end.y = r * 0.35f;
                Vector3 mid = Vector3.Lerp(basePos, end, 0.5f) + Vector3.up * r * 0.2f;
                Limb(b, 0, 1, new[] { basePos, mid, end }, new[] { r * 0.5f, r * 0.4f, r * 0.25f }, 6, rng, 0.04f, false, true);
            }
            return b.Build($"FloraBranch_{seed}");
        }

        internal static Mesh Rock(int seed, float size, bool mossy)
        {
            var rng = new System.Random(seed);
            var b = new Builder(mossy ? 2 : 1) { ProjectScale = 1.2f };
            LowPolyMeshes.Blob(b, 0, new Vector3(0f, size * 0.2f, 0f), new Vector3(size, size * 0.55f, size * 0.8f), rng, 0.28f);
            if (rng.NextDouble() < 0.6)
            {
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                float s = size * Range(rng, 0.35f, 0.5f);
                LowPolyMeshes.Blob(b, 0, d * size * 0.9f + Vector3.up * s * 0.2f, new Vector3(s, s * 0.7f, s * 0.9f), rng, 0.25f);
            }
            if (mossy)
            {
                b.ProjectScale = 2f;
                LowPolyMeshes.Blob(b, 1, new Vector3(Range(rng, -0.2f, 0.2f) * size, size * 0.66f, Range(rng, -0.15f, 0.15f) * size),
                    new Vector3(size * 0.62f, size * 0.12f, size * 0.48f), rng, 0.15f, smooth: true);
            }
            return b.Build($"FloraRock_{seed}");
        }

        internal static Mesh Pebbles(int seed, int count, float spread)
        {
            var rng = new System.Random(seed);
            var b = new Builder(1) { ProjectScale = 3f };
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
                Limb(b, 0, 0, new[] { basePos - Vector3.up * 0.02f, basePos + Vector3.up * h * 0.5f + lean * 0.4f, top },
                    new[] { sr * 1.3f, sr, sr * 0.9f }, 8, rng, 0.04f, false, false, bark: false);
                float rc = h * Range(rng, 0.45f, 0.65f);
                const int sides = 10;
                var outer = new Vector3[sides];
                var mid = new Vector3[sides];
                float phase = Range(rng, 0f, Mathf.PI * 2f);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 d = Flat(phase + i * Mathf.PI * 2f / sides);
                    outer[i] = top + d * rc * Range(rng, 0.95f, 1.05f) - Vector3.up * rc * 0.15f;
                    mid[i] = top + d * rc * 0.68f + Vector3.up * rc * 0.35f;
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
            return b.Build($"FloraMushrooms_{seed}", softCenter: new Vector3(0f, -height, 0f));
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
            var b = new Builder(1);
            for (int s = 0; s < stems; s++)
            {
                Vector3 basePos = s == 0 ? Vector3.zero : Flat(Range(rng, 0f, Mathf.PI * 2f)) * Range(rng, 0.08f, 0.22f);
                float h = height * Range(rng, 0.75f, 1.15f);
                float w = h * 0.5f;
                var lean = new Vector3(Range(rng, -0.08f, 0.08f), 0f, Range(rng, -0.08f, 0.08f)) * h;
                float angle = Range(rng, 0f, Mathf.PI);
                for (int c = 0; c < 2; c++)
                {
                    Vector3 d = Flat(angle + c * Mathf.PI * 0.5f) * (w * 0.5f);
                    Vector3 p00 = basePos - d, p10 = basePos + d;
                    Vector3 p01 = basePos - d + Vector3.up * h + lean, p11 = basePos + d + Vector3.up * h + lean;
                    b.Triangle(0, p00, p10, p11, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
                    b.Triangle(0, p00, p11, p01, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
                }
            }
            return b.Build($"FloraTallFlowers_{seed}", upNormals: true);
        }

        internal static Mesh Bush(int seed, float size, bool berries)
        {
            var rng = new System.Random(seed);
            var b = new Builder(berries ? 2 : 1) { ProjectScale = 1.4f };
            int blobs = 4 + rng.Next(3);
            var centers = new List<Vector3>();
            var radii = new List<float>();
            for (int i = 0; i < blobs; i++)
            {
                Vector3 d = Flat(Range(rng, 0f, Mathf.PI * 2f));
                float dist = i == 0 ? 0f : size * Range(rng, 0.25f, 0.5f);
                float r = size * Range(rng, 0.42f, 0.6f) * (i == 0 ? 1.15f : 1f);
                var c = d * dist + Vector3.up * r * (i == 0 ? 0.75f : Range(rng, 0.55f, 0.95f));
                centers.Add(c);
                radii.Add(r);
                LowPolyMeshes.Blob(b, 0, c, new Vector3(r, r * 0.85f, r), rng, 0.05f, smooth: true, detail: 2, roundNormals: true);
            }
            if (berries)
            {
                int count = 8 + rng.Next(6);
                for (int i = 0; i < count; i++)
                {
                    int k = rng.Next(centers.Count);
                    var dir = new Vector3(Range(rng, -1f, 1f), Range(rng, 0f, 0.9f), Range(rng, -1f, 1f)).normalized;
                    LowPolyMeshes.Blob(b, 1, centers[k] + Vector3.Scale(dir, new Vector3(1f, 0.85f, 1f)) * radii[k] * 0.97f, Vector3.one * size * 0.05f, rng,
                        0.05f, smooth: true);
                }
            }
            return b.Build($"FloraBush_{seed}", softCenter: new Vector3(0f, size * 0.45f, 0f), softBlend: 0.45f);
        }
    }
}
