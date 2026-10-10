using UnityEngine;

namespace Flora.Models
{
    // Tekstura z "narysowanym" obrysem dla płatków i liści: biały trójkąt z ciemną kreską przy krawędziach.
    // Kolor daje materiał (_AlbedoColor mnoży teksturę), więc jedna tekstura służy wszystkim kolorom.
    // Kreska jest częścią tekstury (jak pseudo-kontury na trawie gry), a mipmapy sprawiają,
    // że z daleka zlewa się w kolor zamiast robić czarne plamy.
    internal static class FloraTextures
    {
        private static Texture2D _outline;

        internal static Texture2D Outline => _outline ?? (_outline = CreateOutline(256, 0.07f));

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
                    // Miękkie przejście kreski (bez "schodków"), lekko nierówna grubość jak rysowana ręcznie.
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
