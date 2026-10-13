using System.Collections.Generic;
using UnityEngine;

namespace Water.Effects
{
    internal static class LensDroplets
    {
        private const int MaxDrops = 90;
        private const float TrailSpacing = 0.45f;

        private sealed class Drop
        {
            public Vector2 Position;
            public float Radius;
            public float Stick;
            public float Speed;
            public float Age;
            public float Life;
            public float Wobble;
            public float LastTrail;
            public bool Trail;
        }

        private static readonly List<Drop> Drops = new List<Drop>();
        private static Texture2D _texture;
        private static float _sheen;

        internal static void Clear()
        {
            Drops.Clear();
            _sheen = 0f;
        }

        internal static void Splash(float amount)
        {
            if (!WaterMod.LensDroplets.Value || amount <= 0.02f)
            {
                return;
            }
            int count = Mathf.RoundToInt(Mathf.Lerp(3f, 34f, Mathf.Clamp01(amount)));
            for (int i = 0; i < count; i++)
            {
                Spawn(Random.Range(0.006f, 0.022f + 0.012f * amount), Random.value, Random.Range(-0.05f, 1f));
            }
            _sheen = Mathf.Max(_sheen, amount * 0.5f);
        }

        internal static void Emerge()
        {
            if (!WaterMod.LensDroplets.Value)
            {
                return;
            }
            for (int i = 0; i < 55; i++)
            {
                Spawn(Random.Range(0.006f, 0.03f), Random.value, Random.Range(-0.05f, 1.05f));
            }
            _sheen = 1f;
        }

        internal static void Submerge() => Clear();

        private static void Spawn(float radius, float x, float y)
        {
            if (Drops.Count >= MaxDrops)
            {
                Drops.RemoveAt(0);
            }
            Drops.Add(new Drop
            {
                Position = new Vector2(x, y),
                Radius = radius,
                Stick = Random.Range(0.1f, 1.6f) * Mathf.Lerp(2.2f, 0.6f, Mathf.InverseLerp(0.006f, 0.03f, radius)),
                Life = Random.Range(3.5f, 8f),
                Wobble = Random.value * 10f,
            });
        }

        private static void Step(float dt)
        {
            _sheen = Mathf.MoveTowards(_sheen, 0f, dt * 0.6f);
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                Drop d = Drops[i];
                d.Age += dt;
                if (d.Age > d.Life || d.Position.y > 1.1f)
                {
                    Drops.RemoveAt(i);
                    continue;
                }
                if (d.Trail || d.Age < d.Stick || d.Radius < 0.009f)
                {
                    continue;
                }
                float pull = Mathf.InverseLerp(0.008f, 0.03f, d.Radius);
                d.Speed = Mathf.Min(d.Speed + dt * (0.08f + 0.5f * pull), 0.12f + 0.45f * pull);
                d.Position.y += d.Speed * dt;
                d.Position.x += Mathf.Sin(d.Age * 3f + d.Wobble) * 0.004f * dt;
                d.Radius = Mathf.Max(0.006f, d.Radius - dt * 0.0012f);
                if (d.Position.y - d.LastTrail > TrailSpacing * d.Radius * 4f)
                {
                    d.LastTrail = d.Position.y;
                    if (Drops.Count < MaxDrops)
                    {
                        Drops.Add(new Drop
                        {
                            Position = d.Position - new Vector2(0f, d.Radius * 0.9f),
                            Radius = d.Radius * Random.Range(0.25f, 0.45f),
                            Life = Random.Range(1.2f, 2.8f),
                            Trail = true,
                        });
                    }
                }
            }
        }

        internal static void Draw()
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (!WaterMod.LensDroplets.Value)
            {
                return;
            }
            Step(Time.deltaTime);
            if (Drops.Count == 0 && _sheen <= 0f)
            {
                return;
            }
            Texture2D tex = Texture();
            float h = Screen.height, w = Screen.width;
            Color saved = GUI.color;
            if (_sheen > 0f)
            {
                GUI.color = new Color(0.75f, 0.85f, 0.9f, 0.08f * _sheen);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
            }
            foreach (Drop d in Drops)
            {
                float fadeIn = Mathf.Clamp01(d.Age * 12f);
                float fadeOut = Mathf.Clamp01((d.Life - d.Age) / (d.Trail ? 0.8f : 1.5f));
                float r = d.Radius * h;
                float stretch = d.Trail ? 1f : 1f + Mathf.Clamp01(d.Speed * 4f) * 0.6f;
                GUI.color = new Color(1f, 1f, 1f, fadeIn * fadeOut * (d.Trail ? 0.6f : 0.9f));
                GUI.DrawTexture(new Rect(d.Position.x * w - r, d.Position.y * h - r * stretch * 1.4f + r, r * 2f, r * 2f * stretch), tex);
            }
            GUI.color = saved;
        }

        private static Texture2D Texture()
        {
            if (_texture != null)
            {
                return _texture;
            }
            const int size = 64;
            _texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LensDroplet", wrapMode = TextureWrapMode.Clamp };
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
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.88f, r));
                float rim = Mathf.SmoothStep(0.55f, 0.95f, r) * edge;
                float lower = Mathf.Clamp01(-v);
                float upper = Mathf.Clamp01(v);
                float glow = Mathf.Clamp01(1f - new Vector2(u * 1.3f, v + 0.45f).magnitude * 1.6f) * 0.55f;
                float spec = Mathf.Pow(Mathf.Clamp01(1f - new Vector2(u + 0.32f, v - 0.42f).magnitude * 5.5f), 2f);
                float dark = rim * upper * 0.75f + rim * 0.2f;
                float light = glow * (0.4f + lower) + spec + rim * lower * 0.35f;
                float c = Mathf.Clamp01(0.55f + light - dark * 0.6f);
                float a = Mathf.Clamp01(0.12f + dark * 0.55f + light * 0.75f) * edge;
                pixels[y * size + x] = new Color(c * 0.92f, c * 0.97f, c, a);
            }
            _texture.SetPixels(pixels);
            _texture.Apply();
            return _texture;
        }
    }
}
