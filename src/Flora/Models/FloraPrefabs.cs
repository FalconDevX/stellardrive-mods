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
        private static Material _grassTemplate;
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        // Kolory dobrane do palety gry (stonowane, lekko przygaszone jak liście jodeł).
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

        // Kwiaty używają kopii materiału trawy (tryb _Is_Grass): zlewają się z terenem i nie dostają
        // czarnych konturów jak drzewa - tak jak trawa w grze.
        internal static void SetGrassTemplate(Material grass)
        {
            if (_grassTemplate == null && grass != null)
            {
                _grassTemplate = grass;
            }
        }

        private static GameObject[] _flowers;

        // Prefaby tworzymy raz i używamy na wszystkich planetach.
        internal static GameObject[] CreateFlowers() => _flowers ?? (_flowers = BuildFlowers());

        private static GameObject[] BuildFlowers()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < PetalColors.Length; i++)
            {
                Mesh mesh = LowPolyMeshes.FlowerClump(2000 + i, 8 + i % 4, 0.14f);
                Material[] mats =
                {
                    GetMaterial("stem", StemGreen, true, grass: true),
                    GetMaterial("petals" + i, PetalColors[i], true, grass: true),
                    GetMaterial("center", FlowerCenter, false, grass: true),
                };
                list.Add(CreatePrefab($"FloraFlowers{i}", mesh, mats, castShadows: false));
            }
            return list.ToArray();
        }

        private static GameObject CreatePrefab(string name, Mesh mesh, Material[] materials, bool castShadows = true)
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
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        private static Material GetMaterial(string key, Color color, bool wind, bool grass = false)
        {
            bool useGrass = grass && _grassTemplate != null;
            key = (useGrass ? "grass_" : "") + key;
            if (Materials.TryGetValue(key, out Material existing))
            {
                return existing;
            }
            var mat = new Material(useGrass ? _grassTemplate : _template) { name = "Flora_" + key };
            // Tekstura z narysowanym obrysem; części bez obrysu mają UV w jej białym środku.
            SetTexture(mat, "_Albedo", FloraTextures.Outline);
            SetTexture(mat, "_Normal", null);
            SetColor(mat, "_AlbedoColor", color);
            SetFloat(mat, "_AlphaCutoff", 0f);
            SetFloat(mat, "_CullMode", 0f); // dwustronnie, jak liście w grze
            SetFloat(mat, "_Translucency", wind ? 1f : 0f);
            SetFloat(mat, "_EnableLeafWind", wind ? 1f : 0f);
            SetFloat(mat, "_Is_Tree", 0f);
            SetFloat(mat, "_Is_Grass", useGrass ? 1f : 0f);
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
