using System;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using UnityEngine;

namespace Flora.Diagnostics
{
    [HarmonyPatch(typeof(PlanetDenseEnvironment), "Awake")]
    internal static class EnvironmentDumpPatch
    {
        private static void Prefix(PlanetDenseEnvironment __instance)
        {
            if (!FloraMod.DumpEnvironment.Value)
            {
                return;
            }
            try
            {
                Dump(__instance);
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Error("Vegetation dump error: " + e);
            }
        }

        private static void Dump(PlanetDenseEnvironment env)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Planet: {env.gameObject.name} (configs: {env.configs?.Length ?? 0})");
            for (int i = 0; i < (env.configs?.Length ?? 0); i++)
            {
                PlanetDensePlacementConfig c = env.configs[i];
                sb.AppendLine($"## config {i}: level={c.level} coverage={c.approximateCoverageRatio} physics={c.hasPhysics}/{c.physicsRange} " +
                              $"align={c.alignWithTerrain} elevMin={c.elevationMin} noiseOffset={c.noiseOffset} sound={c.hasSound} " +
                              $"viewRange={Traverse.Create(c).Field("viewRange").GetValue()} pattern={(c.pattern != null ? c.pattern.name + " objects=" + c.pattern.GetObjectCount() : "null")}");
                foreach (var spawn in c.objectsSpawn ?? Array.Empty<PlanetDensePlacementConfig.DenseObjectPlacementType>())
                {
                    sb.AppendLine($"- prefab '{spawn.prefab?.name}' prob={spawn.probability} bias={spawn.densityBiasValue}^{spawn.densityBiasPower}");
                    if (spawn.prefab != null)
                    {
                        DumpObject(sb, spawn.prefab.transform, "    ");
                    }
                }
            }

            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Flora");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"env_{Sanitize(env.gameObject.name)}.txt");
            File.WriteAllText(file, sb.ToString());
            Melon<FloraMod>.Logger.Msg($"Vegetation dump: {file}");
        }

        private static void DumpObject(StringBuilder sb, Transform t, string indent)
        {
            var components = t.GetComponents<Component>().Where(x => x != null && !(x is Transform)).Select(x => x.GetType().Name);
            sb.AppendLine($"{indent}{t.name} scale={t.localScale} [{string.Join(", ", components)}]");
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh m = mf.sharedMesh;
                sb.AppendLine($"{indent}  mesh '{m.name}' verts={m.vertexCount} tris={m.triangles.Length / 3} bounds={m.bounds.size} colors={m.colors.Length > 0} uv={m.uv.Length > 0}");
            }
            var mr = t.GetComponent<Renderer>();
            if (mr != null)
            {
                foreach (Material mat in mr.sharedMaterials.Where(x => x != null))
                {
                    sb.AppendLine($"{indent}  material '{mat.name}' shader '{mat.shader.name}' tex '{(mat.mainTexture != null ? mat.mainTexture.name : "-")}'");
                    for (int p = 0; p < mat.shader.GetPropertyCount(); p++)
                    {
                        string name = mat.shader.GetPropertyName(p);
                        switch (mat.shader.GetPropertyType(p))
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                sb.AppendLine($"{indent}    {name} = {mat.GetColor(name)}");
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                sb.AppendLine($"{indent}    {name} = {mat.GetFloat(name)}");
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                Texture tex = mat.GetTexture(name);
                                sb.AppendLine($"{indent}    {name} = {(tex != null ? tex.name + " " + tex.width + "x" + tex.height : "-")}");
                                break;
                        }
                    }
                }
            }
            foreach (Transform child in t)
            {
                DumpObject(sb, child, indent + "  ");
            }
        }

        private static string Sanitize(string name)
        {
            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(ch, '_');
            }
            return name;
        }
    }
}
