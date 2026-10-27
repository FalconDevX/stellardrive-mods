using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using Odyssey.Patches;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using UnityEngine;

namespace Odyssey.Diagnostics
{
    internal static class ModelExport
    {
        internal static void Run()
        {
            var log = Melon<DevMod>.Logger;
            string dir = Path.Combine(DevTools.OutDir, "export");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var provider = new PartSettingsProvider();
            var parts = new List<(PartSettings part, string module)>();
            foreach (var mod in ModParts.All) parts.Add((mod.get(), Module(mod.create)));
            foreach (var extra in ModParts.ExtraMenuParts) parts.Add((extra().part, Module(extra)));
            foreach (var variants in ModParts.MenuVariants)
            {
                foreach (var (id, _) in variants()) parts.Add((provider.GetPartSettingById(id), Module(variants)));
            }
            var textures = new Dictionary<int, string>();
            var json = new StringBuilder("{\"parts\":[");
            int exported = 0, skipped = 0;
            foreach (var (part, module) in parts.Where(p => p.part != null && p.part.part != null).GroupBy(p => p.part.id).Select(g => g.First()))
            {
                var meshes = new List<string>();
                Transform root = part.part.transform;
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null || !renderer.enabled || !Visible(renderer.transform, root)) continue;
                    if (!mesh.isReadable)
                    {
                        skipped++;
                        log.Msg($"[export] {part.fullLabel}: {renderer.name} mesh {mesh.name} is not readable");
                        continue;
                    }
                    Matrix4x4 m = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                        Material material = renderer.sharedMaterials.Length > sub ? renderer.sharedMaterials[sub] : renderer.sharedMaterial;
                        string file = $"p{part.id}_{meshes.Count}.bin";
                        WriteMesh(Path.Combine(dir, file), mesh, sub, m);
                        string texture = SaveTexture(dir, material, textures);
                        bool emissive = material != null && material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.01f;
                        meshes.Add($"{{\"file\":\"{file}\",\"name\":\"{Escape(renderer.name)}\",\"path\":\"{Escape(HierarchyPath(renderer.transform, root))}\",\"texture\":\"{texture}\",\"emissive\":{(emissive ? "true" : "false")}}}");
                    }
                }
                if (meshes.Count == 0) continue;
                if (exported > 0) json.Append(',');
                json.Append($"{{\"id\":{part.id},\"label\":\"{Escape(part.fullLabel ?? part.name)}\",\"module\":\"{module}\",\"meshes\":[{string.Join(",", meshes)}]}}");
                exported++;
            }
            json.Append("]}");
            File.WriteAllText(Path.Combine(dir, "manifest.json"), json.ToString());
            log.Msg($"[export] {exported} parts, {textures.Count} textures, {skipped} unreadable meshes -> {dir}");
        }

        private static string Module(Delegate d) => d.Method.DeclaringType?.Assembly.GetName().Name ?? "";

        private static bool Visible(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static string HierarchyPath(Transform t, Transform root)
        {
            var names = new List<string>();
            for (; t != null && t != root; t = t.parent) names.Insert(0, t.name);
            return string.Join("/", names);
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string SaveTexture(string dir, Material material, Dictionary<int, string> saved)
        {
            if (material == null) return "";
            Texture tex = material.HasProperty("_Albedo") ? material.GetTexture("_Albedo") : null;
            if (tex == null && material.HasProperty("_MainTex")) tex = material.mainTexture;
            if (tex == null) return "";
            if (!saved.TryGetValue(tex.GetInstanceID(), out string file))
            {
                string name = new string(tex.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
                file = $"{(name.Length > 0 ? name : "texture")}_{saved.Count}.png";
                DevTools.SaveTexture(tex, Path.Combine(dir, file));
                saved[tex.GetInstanceID()] = file;
            }
            return file;
        }

        private static void WriteMesh(string path, Mesh mesh, int sub, Matrix4x4 m)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.GetTriangles(sub);
            Matrix4x4 normalMatrix = m.inverse.transpose;
            var remap = new Dictionary<int, int>();
            var used = new List<int>();
            foreach (int i in triangles)
            {
                if (remap.ContainsKey(i)) continue;
                remap[i] = used.Count;
                used.Add(i);
            }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(Encoding.ASCII.GetBytes("MSH1"));
                w.Write(used.Count);
                w.Write(triangles.Length);
                foreach (int i in used)
                {
                    Vector3 p = m.MultiplyPoint3x4(vertices[i]);
                    Vector3 n = normals.Length > i ? normalMatrix.MultiplyVector(normals[i]).normalized : Vector3.up;
                    Vector2 uv = uvs.Length > i ? uvs[i] : Vector2.zero;
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(n.x); w.Write(n.y); w.Write(n.z);
                    w.Write(uv.x); w.Write(uv.y);
                }
                bool flip = m.determinant < 0f;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    w.Write(remap[triangles[t]]);
                    w.Write(remap[triangles[flip ? t + 2 : t + 1]]);
                    w.Write(remap[triangles[flip ? t + 1 : t + 2]]);
                }
            }
        }
    }
}
