using System.IO;
using UnityEditor;
using UnityEngine;

// Buduje paczkę z shaderem i teksturami wody dla moda Water (Unity w tej samej wersji co gra: 6000.3.24f1).
// Wywołanie: Unity.exe -batchmode -quit -projectPath <ten projekt> -executeMethod BuildWaterBundle.Build
public static class BuildWaterBundle
{
    public static void Build()
    {
        string[] assets =
        {
            "Assets/Water/StylizedWater.shader",
            "Assets/Water/water_caustics.png",
            "Assets/Water/water_foam.png",
            "Assets/Water/water_normal.png",
        };
        foreach (string a in assets)
        {
            if (a.EndsWith(".png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(a);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.sRGBTexture = false; // maski i normalne - wartości liniowe
                importer.textureType = a.Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }
        // shader jest dla URP - projekt musi mieć ustawiony potok URP, inaczej Unity wyrzuca jego warianty
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/URP.asset");
        if (asset == null || asset.scriptableRenderer == null)
        {
            Directory.CreateDirectory("Assets/Settings");
            // dane renderera muszą być zapisanym zasobem (inaczej po ponownym wczytaniu potok URP jest pusty)
            var renderer = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/URPRenderer.asset");
            asset = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.Create(renderer);
            asset.supportsCameraDepthTexture = true;
            AssetDatabase.CreateAsset(asset, "Assets/Settings/URP.asset");
            AssetDatabase.SaveAssets();
        }
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
        // gra działa na Direct3D 11 - shader musi być skompilowany także pod niego (domyślnie Unity 6 bierze DX12)
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        var build = new AssetBundleBuild { assetBundleName = "water.bundle", assetNames = assets };
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Assets"));
        Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(outDir, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        Debug.Log("Water bundle -> " + outDir + " (" + new FileInfo(Path.Combine(outDir, "water.bundle")).Length + " B)");
    }
}
