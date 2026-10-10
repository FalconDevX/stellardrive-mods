using System.Collections.Generic;
using Planets.Objects.Common.Model;
using UnityEngine;

namespace Flora.Models
{
    // Prefaby nowych roślin, zbudowane tak jak roślinność gry:
    //   Root -> Visuals [PlanetObjectVisuals] -> Model [MeshFilter, MeshRenderer]
    // Materiały to kopie materiału z jodły (shader Custom/PlanetFoliage: to samo światło, wiatr i kontury),
    // tylko z jednolitym kolorem zamiast tekstury.
    internal static class FloraPrefabs
    {
        private static GameObject _holder;
        private static Material _template;
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        // Kolory dobrane do palety gry (stonowane, lekko przygaszone jak liście jodeł).
        private static readonly Color[] BushGreens =
        {
            new Color(0.33f, 0.45f, 0.26f), new Color(0.40f, 0.50f, 0.28f), new Color(0.28f, 0.40f, 0.25f),
        };
        private static readonly Color BerryRed = new Color(0.70f, 0.20f, 0.20f);
        private static readonly Color StemGreen = new Color(0.36f, 0.50f, 0.27f);
        private static readonly Color FlowerCenter = new Color(0.93f, 0.76f, 0.30f);
        private static readonly Color[] PetalColors =
        {
            new Color(0.86f, 0.33f, 0.33f), // czerwone
            new Color(0.95f, 0.82f, 0.38f), // żółte
            new Color(0.60f, 0.45f, 0.80f), // fioletowe
            new Color(0.95f, 0.93f, 0.88f), // białe
            new Color(0.93f, 0.58f, 0.70f), // różowe
        };

        internal static bool IsReady => _template != null;

        // Szablon materiału bierzemy z kory jodły (nieprzezroczysty, bez wycinania alfą).
        internal static void SetTemplate(Material bark)
        {
            if (_template != null || bark == null)
            {
                return;
            }
            _template = bark;
            _holder = new GameObject("FloraPrefabs");
            _holder.SetActive(false);
            _holder.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_holder);
        }

        private static GameObject[] _bushes;
        private static GameObject[] _flowers;

        // Prefaby tworzymy raz i używamy na wszystkich planetach.
        internal static GameObject[] CreateBushes() => _bushes ?? (_bushes = BuildBushes());

        internal static GameObject[] CreateFlowers() => _flowers ?? (_flowers = BuildFlowers());

        private static GameObject[] BuildBushes()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < 6; i++)
            {
                bool berries = i % 3 == 2;
                float size = 0.8f + 0.25f * (i % 3);
                Mesh mesh = LowPolyMeshes.Bush(1000 + i, size, berries);
                var mats = new List<Material> { GetMaterial("leaves" + i % BushGreens.Length, BushGreens[i % BushGreens.Length], true) };
                if (berries)
                {
                    mats.Add(GetMaterial("berries", BerryRed, false));
                }
                list.Add(CreatePrefab($"FloraBush{i}", mesh, mats.ToArray()));
            }
            return list.ToArray();
        }

        private static GameObject[] BuildFlowers()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < PetalColors.Length; i++)
            {
                Mesh mesh = LowPolyMeshes.FlowerClump(2000 + i, 5 + i % 3, 0.35f);
                Material[] mats =
                {
                    GetMaterial("stem", StemGreen, true),
                    GetMaterial("petals" + i, PetalColors[i], true),
                    GetMaterial("center", FlowerCenter, false),
                };
                list.Add(CreatePrefab($"FloraFlowers{i}", mesh, mats));
            }
            return list.ToArray();
        }

        private static GameObject CreatePrefab(string name, Mesh mesh, Material[] materials)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_holder.transform, false);
            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            visuals.AddComponent<PlanetObjectVisuals>();
            var model = new GameObject("Model");
            model.transform.SetParent(visuals.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }

        private static Material GetMaterial(string key, Color color, bool wind)
        {
            if (Materials.TryGetValue(key, out Material existing))
            {
                return existing;
            }
            var mat = new Material(_template) { name = "Flora_" + key };
            SetTexture(mat, "_Albedo", Texture2D.whiteTexture);
            SetTexture(mat, "_Normal", null);
            SetColor(mat, "_AlbedoColor", color);
            SetFloat(mat, "_AlphaCutoff", 0f);
            SetFloat(mat, "_CullMode", 0f); // dwustronnie, jak liście w grze
            SetFloat(mat, "_Translucency", wind ? 1f : 0f);
            SetFloat(mat, "_EnableLeafWind", wind ? 1f : 0f);
            SetFloat(mat, "_Is_Tree", 0f);
            SetFloat(mat, "_Is_Grass", 0f);
            mat.enableInstancing = true;
            Materials[key] = mat;
            return mat;
        }

        private static void SetTexture(Material m, string name, Texture value)
        {
            if (m.HasProperty(name)) m.SetTexture(name, value);
        }

        private static void SetColor(Material m, string name, Color value)
        {
            if (m.HasProperty(name)) m.SetColor(name, value);
        }

        private static void SetFloat(Material m, string name, float value)
        {
            if (m.HasProperty(name)) m.SetFloat(name, value);
        }
    }
}
