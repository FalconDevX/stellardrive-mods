using System.Collections.Generic;
using Planets.Objects.Common.Model;
using UnityEngine;

namespace Flora.Models
{
    internal static class FloraPrefabs
    {
        private static GameObject _holder;
        private static Material _template;
        private static Material _grassTemplate;
        private static Material _leavesTemplate;
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static readonly Color StemGreen = new Color(0.36f, 0.50f, 0.27f);
        private static readonly Color[] PetalColors =
        {
            new Color(0.86f, 0.33f, 0.33f),
            new Color(0.95f, 0.82f, 0.38f),
            new Color(0.60f, 0.45f, 0.80f),
            new Color(0.95f, 0.93f, 0.88f),
            new Color(0.93f, 0.58f, 0.70f),
        };

        private static readonly Color Bark = new Color(0.60f, 0.50f, 0.42f);
        private static readonly Color DarkBark = new Color(0.44f, 0.37f, 0.31f);
        private static readonly Color Moss = new Color(0.38f, 0.50f, 0.24f);
        private static readonly Color Stone = new Color(0.52f, 0.52f, 0.50f);
        private static readonly Color MushroomStem = new Color(0.90f, 0.86f, 0.76f);
        private static readonly Color[] MushroomCaps = { new Color(0.78f, 0.25f, 0.20f), new Color(0.60f, 0.42f, 0.27f) };
        private static readonly Color FernGreen = new Color(0.32f, 0.47f, 0.24f);
        private static readonly Color TuftGreen = new Color(0.43f, 0.54f, 0.29f);
        private static readonly Color TuftDry = new Color(0.62f, 0.60f, 0.36f);
        private static readonly Color[] BushGreens =
        {
            new Color(0.33f, 0.45f, 0.26f), new Color(0.40f, 0.50f, 0.28f), new Color(0.28f, 0.40f, 0.25f),
        };
        private static readonly Color[] Berries = { new Color(0.70f, 0.20f, 0.20f), new Color(0.30f, 0.30f, 0.62f) };
        private static readonly Color[] BellColors = { new Color(0.55f, 0.45f, 0.82f), new Color(0.95f, 0.93f, 0.88f), new Color(0.90f, 0.55f, 0.75f) };

        internal static bool IsReady => _template != null;

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

        internal static void SetGrassTemplate(Material grass)
        {
            if (_grassTemplate == null && grass != null)
            {
                _grassTemplate = grass;
            }
        }

        internal static void SetLeavesTemplate(Material leaves)
        {
            if (_leavesTemplate == null && leaves != null)
            {
                _leavesTemplate = leaves;
            }
        }

        private static GameObject[] _flowers;
        private static GameObject[] _forestProps;
        private static GameObject[] _undergrowth;

        internal static GameObject[] CreateFlowers() => _flowers ?? (_flowers = BuildFlowers());

        internal static GameObject[] CreateForestProps() => _forestProps ?? (_forestProps = BuildForestProps());

        internal static GameObject[] CreateUndergrowth() => _undergrowth ?? (_undergrowth = BuildUndergrowth());

        internal static int[] ForestPropWeights { get; private set; }

        internal static int[] UndergrowthWeights { get; private set; }

        private static Material[] WoodMaterials(bool dark, bool mossy)
        {
            var mats = new List<Material> { GetBark(dark), GetMaterial("rings", Color.white, false, leaves: true, texture: FloraTextures.Rings) };
            if (mossy)
            {
                mats.Add(MossMaterial());
            }
            return mats.ToArray();
        }

        private static Material MossMaterial() => GetMaterial("moss", Moss, false, leaves: true, texture: FloraTextures.Leafy);

        private static Material StoneMaterial() => GetMaterial("stone", Stone, false, leaves: true, texture: FloraTextures.Speckle);

        private static Material GetBark(bool dark)
        {
            string key = dark ? "gamebark_dark" : "gamebark";
            if (Materials.TryGetValue(key, out Material existing))
            {
                return existing;
            }
            var mat = new Material(_template) { name = "Flora_" + key };
            SetColor(mat, "_AlbedoColor", dark ? DarkBark : Bark);
            SetFloat(mat, "_AlphaCutoff", 0f);
            SetFloat(mat, "_CullMode", 0f);
            SetFloat(mat, "_Translucency", 0f);
            SetFloat(mat, "_EnableLeafWind", 0f);
            SetFloat(mat, "_Is_Tree", 0f);
            SetFloat(mat, "_Is_Grass", 0f);
            mat.enableInstancing = true;
            Materials[key] = mat;
            return mat;
        }

        private static GameObject[] BuildForestProps()
        {
            var list = new List<GameObject>();
            var weights = new List<int>();
            void Add(GameObject prefab, int weight)
            {
                list.Add(prefab);
                weights.Add(weight);
            }

            for (int i = 0; i < 5; i++)
            {
                int berries = i == 2 ? 0 : i == 4 ? 1 : -1;
                float size = 0.7f + 0.22f * i;
                var mats = new List<Material>
                {
                    GetMaterial("leaves" + i % BushGreens.Length, BushGreens[i % BushGreens.Length], true, leaves: true, texture: FloraTextures.Leafy),
                };
                if (berries >= 0)
                {
                    mats.Add(GetMaterial("berries" + berries, Berries[berries], false, leaves: true, texture: Texture2D.whiteTexture));
                }
                Add(CreatePrefab($"FloraBush{i}", ForestMeshes.Bush(1000 + i, size, berries >= 0), mats.ToArray()), 14);
            }

            Add(CreatePrefab("FloraLogSmall", ForestMeshes.Log(3000, 2.2f, 0.17f, false, false, 1), WoodMaterials(false, false)), 5);
            Add(CreatePrefab("FloraLogMedium", ForestMeshes.Log(3001, 3.6f, 0.27f, true, false, 2), WoodMaterials(false, true)), 5);
            Add(CreatePrefab("FloraLogLarge", ForestMeshes.Log(3002, 5.8f, 0.42f, true, false, 3), WoodMaterials(true, true)), 3);
            Add(CreatePrefab("FloraLogBroken", ForestMeshes.Log(3003, 4.2f, 0.34f, false, true, 1), WoodMaterials(true, false)), 3);

            Add(CreatePrefab("FloraStump", ForestMeshes.Stump(3100, 0.38f, 0.45f, false, true), WoodMaterials(false, true)), 5);
            Add(CreatePrefab("FloraStumpBroken", ForestMeshes.Stump(3101, 0.45f, 0.9f, true, false), WoodMaterials(true, false)), 4);
            Add(CreatePrefab("FloraStumpSmall", ForestMeshes.Stump(3102, 0.26f, 0.3f, false, false), WoodMaterials(false, false)), 4);

            Material stone = StoneMaterial();
            Material moss = MossMaterial();
            Add(CreatePrefab("FloraRockSmall", ForestMeshes.Rock(3200, 0.45f, false), new[] { stone }), 5);
            Add(CreatePrefab("FloraRockMossy", ForestMeshes.Rock(3201, 0.85f, true), new[] { stone, moss }), 4);
            Add(CreatePrefab("FloraRockLarge", ForestMeshes.Rock(3202, 1.4f, true), new[] { stone, moss }), 2);

            ForestPropWeights = weights.ToArray();
            return list.ToArray();
        }

        private static GameObject[] BuildUndergrowth()
        {
            var list = new List<GameObject>();
            var weights = new List<int>();
            void Add(GameObject prefab, int weight)
            {
                list.Add(prefab);
                weights.Add(weight);
            }

            Material fern = GetMaterial("fern", FernGreen, true, grass: true);
            Add(CreatePrefab("FloraFern0", ForestMeshes.Fern(4000, 7, 0.75f), new[] { fern }, castShadows: false), 12);
            Add(CreatePrefab("FloraFern1", ForestMeshes.Fern(4001, 9, 1.0f), new[] { fern }, castShadows: false), 10);
            Add(CreatePrefab("FloraFern2", ForestMeshes.Fern(4002, 5, 0.5f), new[] { fern }, castShadows: false), 10);

            Material[] tuft = { GetMaterial("tuft", TuftGreen, true, grass: true), GetMaterial("tuftdry", TuftDry, true, grass: true) };
            Add(CreatePrefab("FloraTuft0", ForestMeshes.GrassTuft(4100, 14, 0.45f), tuft, castShadows: false), 12);
            Add(CreatePrefab("FloraTuft1", ForestMeshes.GrassTuft(4101, 22, 0.6f), tuft, castShadows: false), 10);

            Material[] branch = WoodMaterials(false, false);
            Add(CreatePrefab("FloraBranch0", ForestMeshes.Branch(4200, 1.2f), branch, castShadows: false), 6);
            Add(CreatePrefab("FloraBranch1", ForestMeshes.Branch(4201, 1.9f), branch, castShadows: false), 5);

            Material stem = GetMaterial("mushroomstem", MushroomStem, false, leaves: true);
            for (int i = 0; i < MushroomCaps.Length; i++)
            {
                Material cap = GetMaterial("mushroomcap" + i, MushroomCaps[i], false, leaves: true);
                Add(CreatePrefab($"FloraMushrooms{i}", ForestMeshes.Mushrooms(4300 + i, 3 + i * 2, 0.13f - i * 0.03f), new[] { stem, cap },
                    castShadows: false), 5);
            }

            Add(CreatePrefab("FloraPebbles", ForestMeshes.Pebbles(4400, 6, 0.35f), new[] { StoneMaterial() }, castShadows: false), 5);

            for (int i = 0; i < BellColors.Length; i++)
            {
                Material bells = GetMaterial("bells" + i, Color.white, true, grass: true, texture: FloraTextures.BellStalk(BellColors[i]), cutoff: 0.5f);
                Add(CreatePrefab($"FloraTallFlowers{i}", ForestMeshes.TallFlowers(4500 + i, 3 + i, 0.45f), new[] { bells }, castShadows: false), 4);
            }

            UndergrowthWeights = weights.ToArray();
            return list.ToArray();
        }

        private static GameObject[] BuildFlowers()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < PetalColors.Length; i++)
            {
                Mesh mesh = LowPolyMeshes.FlowerClump(2000 + i, 8 + i % 4, 0.14f);
                Material[] mats =
                {
                    GetMaterial("stem", StemGreen, true, grass: true),
                    GetMaterial("head" + i, Color.white, true, grass: true, texture: FloraTextures.FlowerHead(PetalColors[i]), cutoff: 0.5f),
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

        private static Material GetMaterial(string key, Color color, bool wind, bool grass = false, bool leaves = false, Texture texture = null,
            float cutoff = 0f)
        {
            bool useGrass = grass && _grassTemplate != null;
            bool useLeaves = !useGrass && leaves && _leavesTemplate != null;
            key = (useGrass ? "grass_" : useLeaves ? "leaf_" : "") + key;
            if (Materials.TryGetValue(key, out Material existing))
            {
                return existing;
            }
            var mat = new Material(useGrass ? _grassTemplate : useLeaves ? _leavesTemplate : _template) { name = "Flora_" + key };
            SetTexture(mat, "_Albedo", texture ?? FloraTextures.Outline);
            SetTexture(mat, "_Normal", null);
            SetColor(mat, "_AlbedoColor", color);
            SetFloat(mat, "_AlphaCutoff", cutoff);
            SetFloat(mat, "_CullMode", 0f);
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
