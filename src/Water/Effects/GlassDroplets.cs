using System.Collections.Generic;
using Ships;
using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Effects
{
    internal static class GlassDroplets
    {
        private const int MaxPerShip = 260;
        private const float RefreshSeconds = 20f;
        private const float SettleSeconds = 2f;
        private const float TrackSeconds = 0.1f;

        private sealed class Pane
        {
            public Transform Transform;
            public Vector3 A, B, C, Normal;
            public float Area;
            public bool Submerged;
        }

        private sealed class Drop
        {
            public Pane Pane;
            public Vector3 Local;
            public float Radius, Stick, Speed, Age, Life, Wobble, LastTrail;
            public bool Trail;
        }

        private sealed class ShipGlass
        {
            public readonly List<Pane> Panes = new List<Pane>();
            public readonly List<Drop> Drops = new List<Drop>();
            public float NextRefresh, NextTrack, TotalArea;
            public Mesh Mesh;
            public int Seen;
            public int PartCount = -1;
            public Transform Root;
        }

        private static readonly Dictionary<uint, ShipGlass> Ships = new Dictionary<uint, ShipGlass>();
        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static readonly List<Vector2> Uvs = new List<Vector2>();
        private static readonly List<Color> Colors = new List<Color>();
        private static readonly List<int> Indices = new List<int>();
        private static readonly List<MeshFilter> Filters = new List<MeshFilter>();
        private static readonly HashSet<Pane> Current = new HashSet<Pane>();
        private static readonly Dictionary<(Transform, Vector3), Pane> Kept = new Dictionary<(Transform, Vector3), Pane>();
        private static Material _material;
        private static Texture2D _texture;
        private static int _frame;

        internal static void Clear()
        {
            foreach (ShipGlass glass in Ships.Values)
            {
                if (glass.Mesh != null) Object.Destroy(glass.Mesh);
            }
            Ships.Clear();
        }

        internal static void BeginFrame()
        {
            _frame++;
            if (_frame % 600 != 0)
            {
                return;
            }
            var stale = new List<uint>();
            foreach (var pair in Ships)
            {
                if (_frame - pair.Value.Seen > 600) stale.Add(pair.Key);
            }
            foreach (uint id in stale)
            {
                if (Ships[id].Mesh != null) Object.Destroy(Ships[id].Mesh);
                Ships.Remove(id);
            }
        }

        internal static void UpdateShip(TrackedShipClient ship, Vector3 up, Vector3 surface, bool lake, Vector3 camPos, float dt)
        {
            if (!WaterMod.GlassDroplets.Value || ship.VisualObject == null)
            {
                return;
            }
            if (!Ships.TryGetValue(ship.Id, out ShipGlass glass))
            {
                Ships[ship.Id] = glass = new ShipGlass();
            }
            glass.Seen = _frame;
            Transform root = ship.VisualObject.transform;
            bool changed = glass.PartCount != ship.PartCount || glass.Root != root;
            if (changed || (lake && Time.time >= glass.NextRefresh))
            {
                glass.NextRefresh = Time.time + (changed ? SettleSeconds : RefreshSeconds);
                glass.PartCount = ship.PartCount;
                glass.Root = root;
                Refresh(glass, root);
            }
            if (lake && Time.time >= glass.NextTrack)
            {
                glass.NextTrack = Time.time + TrackSeconds;
                foreach (Pane pane in glass.Panes)
                {
                    if (pane.Transform == null) continue;
                    Vector3 center = pane.Transform.TransformPoint((pane.A + pane.B + pane.C) / 3f);
                    float altitude = Vector3.Dot(center - surface, up);
                    if (pane.Submerged && altitude > 0.05f)
                    {
                        Spawn(glass, pane, Mathf.Clamp(Mathf.RoundToInt(pane.Area * Random.Range(9f, 16f)), 1, 14));
                    }
                    pane.Submerged = altitude < -0.05f;
                }
            }
            Step(glass, up, dt);
            Draw(glass, camPos);
        }

        internal static void SplashAt(Vector3 point, float power)
        {
            if (!WaterMod.GlassDroplets.Value || power <= 0.02f)
            {
                return;
            }
            float reach = 2f + 7f * Mathf.Sqrt(Mathf.Clamp(power, 0f, 2.5f));
            foreach (ShipGlass glass in Ships.Values)
            {
                foreach (Pane pane in glass.Panes)
                {
                    if (pane.Transform == null) continue;
                    Vector3 center = pane.Transform.TransformPoint((pane.A + pane.B + pane.C) / 3f);
                    float distance = (center - point).magnitude;
                    if (distance > reach) continue;
                    float k = 1f - distance / reach;
                    int count = Mathf.RoundToInt(pane.Area * k * k * Random.Range(6f, 20f) * Mathf.Clamp01(power + 0.3f));
                    if (count > 0) Spawn(glass, pane, Mathf.Min(count, 16));
                }
            }
        }

        private static void Refresh(ShipGlass glass, Transform root)
        {
            Kept.Clear();
            foreach (Pane pane in glass.Panes)
            {
                if (pane.Transform != null) Kept[(pane.Transform, pane.A)] = pane;
            }
            glass.Panes.Clear();
            glass.TotalArea = 0f;
            root.GetComponentsInChildren(false, Filters);
            foreach (MeshFilter filter in Filters)
            {
                string name = filter.gameObject.name;
                if (!name.Contains("IsGlass: True") || name.Contains("Edge")) continue;
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    float area = cross.magnitude * 0.5f;
                    if (area < 0.01f) continue;
                    if (!Kept.TryGetValue((filter.transform, a), out Pane pane))
                    {
                        pane = new Pane { Transform = filter.transform, A = a, B = b, C = c, Normal = cross.normalized, Area = area };
                    }
                    glass.Panes.Add(pane);
                    glass.TotalArea += area;
                }
            }
            Filters.Clear();
            Kept.Clear();
            Current.Clear();
            Current.UnionWith(glass.Panes);
            glass.Drops.RemoveAll(d => d.Pane.Transform == null || !Current.Contains(d.Pane));
            Current.Clear();
        }

        private static void Spawn(ShipGlass glass, Pane pane, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (glass.Drops.Count >= MaxPerShip)
                {
                    glass.Drops.RemoveAt(0);
                }
                float r1 = Random.value, r2 = Random.value;
                if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                float radius = Random.Range(0.006f, 0.024f);
                glass.Drops.Add(new Drop
                {
                    Pane = pane,
                    Local = pane.A + (pane.B - pane.A) * r1 + (pane.C - pane.A) * r2,
                    Radius = radius,
                    Stick = Random.Range(0.1f, 2.2f) * Mathf.Lerp(1.8f, 0.5f, Mathf.InverseLerp(0.006f, 0.024f, radius)),
                    Life = Random.Range(5f, 11f),
                    Wobble = Random.value * 10f,
                });
            }
        }

        private static void Step(ShipGlass glass, Vector3 up, float dt)
        {
            for (int i = glass.Drops.Count - 1; i >= 0; i--)
            {
                Drop d = glass.Drops[i];
                d.Age += dt;
                if (d.Age > d.Life || d.Pane.Transform == null)
                {
                    glass.Drops.RemoveAt(i);
                    continue;
                }
                if (d.Trail || d.Age < d.Stick || d.Radius < 0.008f)
                {
                    continue;
                }
                Vector3 localUp = d.Pane.Transform.InverseTransformDirection(up);
                Vector3 down = -Vector3.ProjectOnPlane(localUp, d.Pane.Normal);
                float slope = down.magnitude;
                if (slope < 0.15f)
                {
                    continue;
                }
                down /= slope;
                float pull = Mathf.InverseLerp(0.008f, 0.024f, d.Radius) * slope;
                d.Speed = Mathf.Min(d.Speed + dt * (0.03f + 0.25f * pull), 0.04f + 0.3f * pull);
                Vector3 side = Vector3.Cross(d.Pane.Normal, down);
                d.Local += (down * d.Speed + side * Mathf.Sin(d.Age * 2.5f + d.Wobble) * 0.01f) * dt;
                d.Radius = Mathf.Max(0.006f, d.Radius - dt * 0.0006f);
                if (!Inside(d.Pane, d.Local) && !Move(glass, d))
                {
                    glass.Drops.RemoveAt(i);
                    continue;
                }
                d.LastTrail += d.Speed * dt;
                if (d.LastTrail > d.Radius * 2.2f && glass.Drops.Count < MaxPerShip)
                {
                    d.LastTrail = 0f;
                    glass.Drops.Add(new Drop
                    {
                        Pane = d.Pane,
                        Local = d.Local - down * d.Radius * 1.2f,
                        Radius = d.Radius * Random.Range(0.25f, 0.45f),
                        Life = d.Age + Random.Range(1.5f, 3.5f),
                        Age = d.Age,
                        Trail = true,
                    });
                }
            }
        }

        private static bool Inside(Pane pane, Vector3 p)
        {
            Vector3 n = pane.Normal;
            return Vector3.Dot(Vector3.Cross(pane.B - pane.A, p - pane.A), n) >= -1e-4f &&
                   Vector3.Dot(Vector3.Cross(pane.C - pane.B, p - pane.B), n) >= -1e-4f &&
                   Vector3.Dot(Vector3.Cross(pane.A - pane.C, p - pane.C), n) >= -1e-4f;
        }

        private static bool Move(ShipGlass glass, Drop d)
        {
            foreach (Pane other in glass.Panes)
            {
                if (other == d.Pane || other.Transform != d.Pane.Transform || Vector3.Dot(other.Normal, d.Pane.Normal) < 0.99f) continue;
                if (Mathf.Abs(Vector3.Dot(d.Local - other.A, other.Normal)) > 0.01f) continue;
                if (Inside(other, d.Local))
                {
                    d.Pane = other;
                    return true;
                }
            }
            return false;
        }

        private static void Draw(ShipGlass glass, Vector3 camPos)
        {
            if (glass.Drops.Count == 0)
            {
                return;
            }
            Material material = Material();
            if (material == null)
            {
                return;
            }
            Vertices.Clear();
            Uvs.Clear();
            Colors.Clear();
            Indices.Clear();
            float exposure = Surface.WaterSurface.Exposure;
            foreach (Drop d in glass.Drops)
            {
                Transform t = d.Pane.Transform;
                Vector3 n = t.TransformDirection(d.Pane.Normal);
                Vector3 c = t.TransformPoint(d.Local);
                if (Vector3.Dot(camPos - c, n) < 0f) n = -n;
                if ((camPos - c).sqrMagnitude > 900f) continue;
                Vector3 down = Vector3.ProjectOnPlane(-t.up, n);
                if (down.sqrMagnitude < 1e-4f) down = Vector3.ProjectOnPlane(-t.forward, n);
                down.Normalize();
                Vector3 right = Vector3.Cross(n, down);
                float stretch = d.Trail ? 1f : 1f + Mathf.Clamp01(d.Speed * 8f) * 0.7f;
                float r = d.Radius;
                Vector3 center = c + n * 0.006f - down * r * (stretch - 1f);
                Vector3 du = down * r * stretch, ru = right * r;
                int baseIndex = Vertices.Count;
                Vertices.Add(center - ru - du);
                Vertices.Add(center + ru - du);
                Vertices.Add(center + ru + du);
                Vertices.Add(center - ru + du);
                Uvs.Add(new Vector2(0f, 1f));
                Uvs.Add(new Vector2(1f, 1f));
                Uvs.Add(new Vector2(1f, 0f));
                Uvs.Add(new Vector2(0f, 0f));
                float fade = Mathf.Clamp01(d.Age * 10f) * Mathf.Clamp01((d.Life - d.Age) / (d.Trail ? 0.8f : 1.5f)) * (d.Trail ? 0.65f : 0.95f);
                var color = new Color(exposure, exposure, exposure, fade);
                Colors.Add(color); Colors.Add(color); Colors.Add(color); Colors.Add(color);
                Indices.Add(baseIndex); Indices.Add(baseIndex + 1); Indices.Add(baseIndex + 2);
                Indices.Add(baseIndex); Indices.Add(baseIndex + 2); Indices.Add(baseIndex + 3);
            }
            if (Vertices.Count == 0)
            {
                return;
            }
            if (glass.Mesh == null)
            {
                glass.Mesh = new Mesh { name = "GlassDroplets" };
                glass.Mesh.MarkDynamic();
            }
            glass.Mesh.Clear();
            glass.Mesh.SetVertices(Vertices);
            glass.Mesh.SetUVs(0, Uvs);
            glass.Mesh.SetColors(Colors);
            glass.Mesh.SetTriangles(Indices, 0);
            glass.Mesh.RecalculateBounds();
            Graphics.DrawMesh(glass.Mesh, Matrix4x4.identity, material, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        private static Material Material()
        {
            if (_material != null)
            {
                return _material;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }
            _material = new Material(shader) { name = "GlassDroplets" };
            _material.SetTexture("_BaseMap", Texture());
            _material.SetColor("_BaseColor", Color.white);
            _material.SetFloat("_Surface", 1f);
            _material.SetFloat("_ZWrite", 0f);
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_Cull", 0f);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.SetOverrideTag("RenderType", "Transparent");
            _material.renderQueue = 3050;
            return _material;
        }

        private static Texture2D Texture()
        {
            if (_texture != null)
            {
                return _texture;
            }
            const int size = 64;
            _texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "GlassDroplet", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.86f, r));
                float rim = Mathf.SmoothStep(0.55f, 0.95f, r) * edge;
                float lower = Mathf.Clamp01(-v);
                float upper = Mathf.Clamp01(v);
                float glow = Mathf.Clamp01(1f - new Vector2(u * 1.3f, v + 0.45f).magnitude * 1.6f) * 0.55f;
                float spec = Mathf.Pow(Mathf.Clamp01(1f - new Vector2(u + 0.32f, v - 0.42f).magnitude * 5.5f), 2f);
                float dark = rim * upper * 0.75f + rim * 0.2f;
                float light = glow * (0.4f + lower) + spec + rim * lower * 0.35f;
                float c = Mathf.Clamp01(0.55f + light - dark * 0.6f);
                float a = Mathf.Clamp01(0.15f + dark * 0.6f + light * 0.75f) * edge;
                pixels[y * size + x] = new Color(c * 0.92f, c * 0.97f, c, a);
            }
            _texture.SetPixels(pixels);
            _texture.Apply();
            return _texture;
        }
    }
}
