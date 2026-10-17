using System.Collections.Generic;
using UnityEngine;

namespace Flora.Models
{
    internal static class FloraTextures
    {
        private static Texture2D _outline;

        internal static Texture2D Outline => _outline ?? (_outline = CreateOutline(256, 0.07f));

        private static Texture2D _rings;
        private static Texture2D _leafy;
        private static Texture2D _speckle;
        private static readonly Dictionary<Color, Texture2D> FlowerHeads = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<Color, Texture2D> BellStalks = new Dictionary<Color, Texture2D>();

        private static readonly Color StemColor = new Color(0.36f, 0.50f, 0.27f);
        private static readonly Color FlowerEye = new Color(0.95f, 0.78f, 0.30f);

        internal static Texture2D Rings => _rings ?? (_rings = CreateRings(256));

        internal static Texture2D Leafy => _leafy ?? (_leafy = CreateLeafy(256, 170));

        internal static Texture2D Speckle => _speckle ?? (_speckle = CreateSpeckle(256, 900));

        internal static Texture2D FlowerHead(Color petals)
        {
            if (!FlowerHeads.TryGetValue(petals, out Texture2D tex))
            {
                tex = CreateFlowerHead(128, petals, 5 + FlowerHeads.Count % 2);
                FlowerHeads[petals] = tex;
            }
            return tex;
        }

        internal static Texture2D BellStalk(Color bells)
        {
            if (!BellStalks.TryGetValue(bells, out Texture2D tex))
            {
                tex = CreateBellStalk(128, 256, bells, 1000 + BellStalks.Count);
                BellStalks[bells] = tex;
            }
            return tex;
        }

        private static Texture2D NewTexture(string name, int width, int height, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
        }

        private static Texture2D Finish(Texture2D tex, Color[] pixels)
        {
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }

        private static Texture2D CreateRings(int size)
        {
            var tex = NewTexture("FloraRings", size, size, TextureWrapMode.Clamp);
            var light = new Color(0.86f, 0.72f, 0.52f);
            var dark = new Color(0.66f, 0.50f, 0.33f);
            var bark = new Color(0.36f, 0.27f, 0.20f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float wobble = Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.08f;
                    float ring = Mathf.Abs(Mathf.Sin((r + wobble) * Mathf.PI * 9f));
                    Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, ring * 1.4f - 0.2f));
                    c = Color.Lerp(dark * 0.9f, c, Mathf.SmoothStep(0.05f, 0.14f, r));
                    c = Color.Lerp(c, bark, Mathf.SmoothStep(0.86f, 0.9f, r));
                    c *= 0.94f + 0.12f * Mathf.PerlinNoise(x * 0.2f, y * 0.05f);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            return Finish(tex, pixels);
        }

        private static Texture2D CreateLeafy(int size, int leaves)
        {
            var tex = NewTexture("FloraLeafy", size, size, TextureWrapMode.Repeat);
            var rng = new System.Random(77);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            for (int l = 0; l < leaves; l++)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float len = size * (0.05f + 0.04f * (float)rng.NextDouble());
                float width = len * 0.45f;
                float shade = 0.75f + 0.4f * (float)rng.NextDouble();
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                int reach = Mathf.CeilToInt(len);
                for (int oy = -reach; oy <= reach; oy++)
                {
                    for (int ox = -reach; ox <= reach; ox++)
                    {
                        float lx = (ox * ca + oy * sa) / len;
                        float ly = (-ox * sa + oy * ca) / width;
                        float edge = 1f - lx * lx;
                        if (lx < -1f || lx > 1f || Mathf.Abs(ly) > edge)
                        {
                            continue;
                        }
                        int px = ((int)cx + ox + size) % size, py = ((int)cy + oy + size) % size;
                        float v = shade * (0.85f + 0.15f * lx) * (Mathf.Abs(ly) < 0.08f ? 0.88f : 1f) * (Mathf.Abs(ly) > edge - 0.12f ? 0.8f : 1f);
                        pixels[py * size + px] = new Color(v, v, v, 1f);
                    }
                }
            }
            return Finish(tex, pixels);
        }

        private static Texture2D CreateSpeckle(int size, int specks)
        {
            var tex = NewTexture("FloraSpeckle", size, size, TextureWrapMode.Repeat);
            var rng = new System.Random(91);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(0.92f, 0.92f, 0.92f, 1f);
            }
            for (int s = 0; s < specks; s++)
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                int r = 1 + rng.Next(4);
                float v = rng.NextDouble() < 0.6 ? 0.7f + 0.15f * (float)rng.NextDouble() : 1f;
                for (int oy = -r; oy <= r; oy++)
                {
                    for (int ox = -r; ox <= r; ox++)
                    {
                        if (ox * ox + oy * oy > r * r)
                        {
                            continue;
                        }
                        int px = (cx + ox + size) % size, py = (cy + oy + size) % size;
                        pixels[py * size + px] = new Color(v, v, v, 1f);
                    }
                }
            }
            return Finish(tex, pixels);
        }

        private static Texture2D CreateFlowerHead(int size, Color petals, int count)
        {
            var tex = NewTexture("FloraFlowerHead", size, size, TextureWrapMode.Clamp);
            var line = new Color(0.25f, 0.18f, 0.14f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = ((x + 0.5f) / size - 0.5f) * 2f, dy = ((y + 0.5f) / size - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float lobe = Mathf.Abs(Mathf.Cos(a * count * 0.5f));
                    float outer = 0.95f * (0.45f + 0.55f * Mathf.Pow(lobe, 0.5f));
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (r < outer)
                    {
                        float t = r / outer;
                        c = petals * (0.78f + 0.3f * t);
                        if (outer - r < 0.07f || (lobe < 0.12f && r > 0.25f))
                        {
                            c = Color.Lerp(c, line, 0.55f);
                        }
                        if (r < 0.26f)
                        {
                            c = FlowerEye * (0.85f + 0.3f * Mathf.PerlinNoise(x * 0.4f, y * 0.4f));
                            if (r > 0.21f)
                            {
                                c = Color.Lerp(c, line, 0.5f);
                            }
                        }
                        c.a = 1f;
                    }
                    pixels[y * size + x] = c;
                }
            }
            return Finish(tex, pixels);
        }

        private static Texture2D CreateBellStalk(int width, int height, Color bells, int seed)
        {
            var tex = NewTexture("FloraBellStalk", width, height, TextureWrapMode.Clamp);
            var rng = new System.Random(seed);
            var pixels = new Color[width * height];
            var line = new Color(0.25f, 0.18f, 0.14f);
            void Disc(float cx, float cy, float rx, float ry, Color fill)
            {
                for (int y = Mathf.Max(0, (int)(cy - ry) - 1); y < Mathf.Min(height, (int)(cy + ry) + 2); y++)
                {
                    for (int x = Mathf.Max(0, (int)(cx - rx) - 1); x < Mathf.Min(width, (int)(cx + rx) + 2); x++)
                    {
                        float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                        float d = nx * nx + ny * ny;
                        if (d > 1f)
                        {
                            continue;
                        }
                        Color c = d > 0.72f ? Color.Lerp(fill, line, 0.5f) : fill * (0.85f + 0.25f * (1f - d));
                        c.a = 1f;
                        pixels[y * width + x] = c;
                    }
                }
            }
            float mid = width * 0.5f;
            for (int y = 0; y < height * 0.95f; y++)
            {
                float sway = Mathf.Sin(y / (float)height * 3f) * width * 0.03f;
                for (int x = (int)(mid + sway - 2); x <= (int)(mid + sway + 2); x++)
                {
                    pixels[y * width + x] = new Color(StemColor.r, StemColor.g, StemColor.b, 1f);
                }
            }
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 6; k++)
                {
                    float t = k / 5f;
                    Disc(mid + s * width * (0.06f + 0.2f * t), height * (0.04f + 0.22f * t), width * (0.1f - 0.05f * t), width * 0.035f, StemColor);
                }
            }
            int count = 7;
            for (int k = 0; k < count; k++)
            {
                float t = k / (float)(count - 1);
                float side = k % 2 == 0 ? -1f : 1f;
                float size = width * (0.13f - 0.05f * t);
                float cx = mid + side * width * (0.14f - 0.08f * t) + (float)(rng.NextDouble() - 0.5) * width * 0.03f;
                float cy = height * (0.48f + 0.44f * t);
                Disc(cx, cy, size, size * 1.15f, bells * (0.9f + 0.2f * (float)rng.NextDouble()));
            }
            Disc(mid, height * 0.95f, width * 0.06f, width * 0.07f, bells);
            return Finish(tex, pixels);
        }

        private static Texture2D CreateOutline(int size, float lineWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = "FloraOutline",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            Vector2 a = LowPolyMeshes.Builder.UvA, b = LowPolyMeshes.Builder.UvB, c = LowPolyMeshes.Builder.UvC;
            var line = new Color(0.08f, 0.07f, 0.06f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float d = Mathf.Min(DistToSegment(p, a, b), Mathf.Min(DistToSegment(p, b, c), DistToSegment(p, c, a)));
                    float width = lineWidth * (0.85f + 0.3f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f));
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(width * 0.6f, width, d));
                    pixels[y * size + x] = Color.Lerp(line, Color.white, t);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
