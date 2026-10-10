using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Flora.Models
{
    // Proste siatki low-poly w stylu gry: płaskie cieniowanie (każdy trójkąt ma własne wierzchołki i normalną),
    // mało trójkątów, kolor nadaje materiał. Losowość z podanego ziarna, więc każdy wariant wygląda zawsze tak samo.
    internal static class LowPolyMeshes
    {
        // Pomocnik zbierający trójkąty w kilka podsiatek (jedna podsiatka = jeden materiał/kolor).
        internal sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();

            // UV trójkąta na teksturze z obrysem (FloraTextures.Outline): rogi trójkąta = krawędzie z czarną kreską.
            public static readonly Vector2 UvA = new Vector2(0.02f, 0.02f);
            public static readonly Vector2 UvB = new Vector2(0.98f, 0.02f);
            public static readonly Vector2 UvC = new Vector2(0.5f, 0.98f);
            // Punkt w środku tekstury (sam kolor, bez kreski) - dla części bez obrysu.
            public static readonly Vector2 UvPlain = new Vector2(0.5f, 0.33f);
            private readonly List<List<int>> _submeshes = new List<List<int>>();

            public Builder(int submeshCount)
            {
                for (int i = 0; i < submeshCount; i++)
                {
                    _submeshes.Add(new List<int>());
                }
            }

            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(submesh, a, b, c, UvPlain, UvPlain, UvPlain);
            }

            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _uvs.Add(uvA);
                _uvs.Add(uvB);
                _uvs.Add(uvC);
                _normals.Add(n);
                _normals.Add(n);
                _normals.Add(n);
                _submeshes[submesh].Add(i);
                _submeshes[submesh].Add(i + 1);
                _submeshes[submesh].Add(i + 2);
            }

            // Trójkąt zwrócony na zewnątrz od punktu "inside" (żeby bryły nie były wywrócone na lewą stronę).
            public void OutwardTriangle(int submesh, Vector3 inside, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Triangle(submesh, a, c, b);
                }
                else
                {
                    Triangle(submesh, a, b, c);
                }
            }

            // Dwustronny trójkąt (płatki, liście) - widoczny z obu stron niezależnie od cullingu materiału.
            public void DoubleTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(submesh, a, b, c);
                Triangle(submesh, a, c, b);
            }

            // Dwustronny trójkąt z narysowanym obrysem na krawędziach (tekstura z kreską).
            public void OutlinedTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(submesh, a, b, c, UvA, UvB, UvC);
                Triangle(submesh, a, c, b, UvA, UvC, UvB);
            }

            // upNormals: wszystkie normalne w górę, jak w trawie gry - roślina jest oświetlona jak teren pod nią,
            // a filtr konturów (liczony m.in. z normalnych) nie widzi na niej krawędzi.
            // softCenter: normalne "od środka kępy" (jak w liściastych koronach drzew) - miękkie, zaokrąglone
            // światło zamiast ostrych ścianek i mniej konturów na każdej krawędzi.
            public Mesh Build(string name, bool upNormals = false, Vector3? softCenter = null)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                List<Vector3> normals = _normals;
                if (upNormals)
                {
                    normals = Enumerable.Repeat(Vector3.up, _vertices.Count).ToList();
                }
                else if (softCenter.HasValue)
                {
                    normals = _vertices.Select(v => ((v - softCenter.Value).normalized + Vector3.up * 0.6f).normalized).ToList();
                }
                mesh.SetNormals(normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _submeshes.Count;
                for (int i = 0; i < _submeshes.Count; i++)
                {
                    mesh.SetTriangles(_submeshes[i], i);
                }
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }

        private static readonly Vector3[] IcoVertices = CreateIcoVertices();

        private static readonly int[] IcoTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private static Vector3[] CreateIcoVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = v[i].normalized;
            }
            return v;
        }

        // Nieregularna "kula" z 20 trójkątów - podstawa krzaka (jak kępa liści w low-poly).
        private static void Blob(Builder b, int submesh, Vector3 center, Vector3 radius, System.Random rng, float jitter)
        {
            var verts = new Vector3[IcoVertices.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float j = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                Vector3 v = IcoVertices[i] * j;
                verts[i] = center + Vector3.Scale(v, radius);
            }
            for (int i = 0; i < IcoTriangles.Length; i += 3)
            {
                b.OutwardTriangle(submesh, center, verts[IcoTriangles[i]], verts[IcoTriangles[i + 1]], verts[IcoTriangles[i + 2]]);
            }
        }

        // Krzak: 3-5 nachodzących na siebie kęp liści, opcjonalnie owoce (podsiatka 1).
        internal static Mesh Bush(int seed, float size, bool berries)
        {
            var rng = new System.Random(seed);
            var b = new Builder(berries ? 2 : 1);
            int blobs = 3 + rng.Next(3);
            var centers = new List<Vector3>();
            for (int i = 0; i < blobs; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = i == 0 ? 0f : size * (0.25f + 0.25f * (float)rng.NextDouble());
                float r = size * (0.45f + 0.2f * (float)rng.NextDouble()) * (i == 0 ? 1.15f : 1f);
                var c = new Vector3(Mathf.Cos(angle) * dist, r * 0.75f, Mathf.Sin(angle) * dist);
                centers.Add(c);
                Blob(b, 0, c, new Vector3(r, r * 0.8f, r), rng, 0.18f);
            }
            if (berries)
            {
                int count = 6 + rng.Next(6);
                for (int i = 0; i < count; i++)
                {
                    Vector3 c = centers[rng.Next(centers.Count)];
                    Vector3 dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.9f, (float)rng.NextDouble() * 2f - 1f).normalized;
                    float r = size * 0.5f;
                    Blob(b, 1, c + dir * r * 0.95f, Vector3.one * size * 0.07f, rng, 0.1f);
                }
            }
            return b.Build($"FloraBush_{seed}", softCenter: new Vector3(0f, size * 0.6f, 0f));
        }

        // Kępa kwiatów: kilka łodyg z liśćmi (podsiatka 0), płatki (podsiatka 1) i środek (podsiatka 2).
        internal static Mesh FlowerClump(int seed, int flowers, float height)
        {
            var rng = new System.Random(seed);
            var b = new Builder(3);
            for (int f = 0; f < flowers; f++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = f == 0 ? 0f : 0.1f + 0.35f * (float)rng.NextDouble();
                var basePos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                float h = height * (0.7f + 0.5f * (float)rng.NextDouble());
                var lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.15f;
                Vector3 top = basePos + Vector3.up * h + lean;

                // Łodyga: wąski trójkątny graniastosłup.
                float w = 0.02f;
                Vector3 s0 = new Vector3(w, 0, 0), s1 = new Vector3(-w * 0.5f, 0, w * 0.87f), s2 = new Vector3(-w * 0.5f, 0, -w * 0.87f);
                Vector3[] bottom = { basePos + s0, basePos + s1, basePos + s2 };
                Vector3[] upper = { top + s0, top + s1, top + s2 };
                Vector3 axisMid = (basePos + top) * 0.5f;
                for (int i = 0; i < 3; i++)
                {
                    int n = (i + 1) % 3;
                    b.OutwardTriangle(0, axisMid, bottom[i], upper[i], upper[n]);
                    b.OutwardTriangle(0, axisMid, bottom[i], upper[n], bottom[n]);
                }

                // Liść w połowie łodygi.
                Vector3 mid = Vector3.Lerp(basePos, top, 0.4f);
                float la = (float)(rng.NextDouble() * Mathf.PI * 2f);
                var leafDir = new Vector3(Mathf.Cos(la), 0.35f, Mathf.Sin(la));
                var leafSide = Vector3.Cross(leafDir, Vector3.up).normalized * 0.03f;
                b.OutlinedTriangle(0, mid + leafSide * 1.5f, mid - leafSide * 1.5f, mid + leafDir * 0.1f);

                // Kwiat: 5 płatków w kształcie gwiazdy, lekko uniesionych na końcach.
                int petals = 5;
                float petalLen = 0.09f + 0.04f * (float)rng.NextDouble();
                float rot = (float)(rng.NextDouble() * Mathf.PI * 2f);
                for (int p = 0; p < petals; p++)
                {
                    float a0 = rot + p * Mathf.PI * 2f / petals;
                    float a1 = a0 + Mathf.PI / petals * 0.8f;
                    float a2 = a0 - Mathf.PI / petals * 0.8f;
                    Vector3 tip = top + new Vector3(Mathf.Cos(a0), 0.25f, Mathf.Sin(a0)) * petalLen;
                    Vector3 l = top + new Vector3(Mathf.Cos(a1), 0.05f, Mathf.Sin(a1)) * petalLen * 0.45f;
                    Vector3 r = top + new Vector3(Mathf.Cos(a2), 0.05f, Mathf.Sin(a2)) * petalLen * 0.45f;
                    // Obrys narysowany na teksturze: brzeg płatka i kreska w środku (jak pseudo-kontury na trawie).
                    b.OutlinedTriangle(1, l, tip, r);
                    b.OutlinedTriangle(1, top, l, r);
                }

                // Środek kwiatu: mała piramidka.
                float c = petalLen * 0.25f;
                Vector3 cTop = top + Vector3.up * c * 0.8f;
                Vector3[] ring =
                {
                    top + new Vector3(c, 0.01f, 0), top + new Vector3(-c * 0.5f, 0.01f, c * 0.87f), top + new Vector3(-c * 0.5f, 0.01f, -c * 0.87f),
                };
                for (int i = 0; i < 3; i++)
                {
                    b.OutlinedTriangle(2, ring[i], cTop, ring[(i + 1) % 3]);
                }
            }
            return b.Build($"FloraFlowers_{seed}", upNormals: true);
        }
    }
}
