using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildSunRaysBundle
{
    public static void Build()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/URP.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11, UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 });
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        var build = new AssetBundleBuild { assetBundleName = "sunrays.bundle", assetNames = new[] { "Assets/SunRays/SunRays.shader" } };
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../SunRays/Assets"));
        Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(outDir, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        Debug.Log("SunRays bundle -> " + outDir + " (" + new FileInfo(Path.Combine(outDir, "sunrays.bundle")).Length + " B)");
    }
}
