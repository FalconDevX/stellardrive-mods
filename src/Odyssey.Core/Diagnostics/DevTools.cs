using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Items.Model;
using Managers.Interface;
using MelonLoader;
using MelonLoader.Utils;
using Research.Interface.Values;
using Ships.Interface.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Diagnostics
{
    internal static class DevTools
    {
        internal static bool IsTyping()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null);
        }

        internal static string OutDir
        {
            get
            {
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        internal static void SaveTexture(Texture tex, string path)
        {
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var encode = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode != null) File.WriteAllBytes(path, (byte[])encode.Invoke(null, new object[] { copy }));
            UnityEngine.Object.Destroy(copy);
        }

        internal static void DumpHierarchy(StringBuilder sb, Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}- {t.name} layer={LayerMask.LayerToName(t.gameObject.layer)} active={t.gameObject.activeSelf} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale}");
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                sb.AppendLine($"{indent}    [{c.GetType().FullName}] {Describe(c)}");
            }
            foreach (Transform child in t)
            {
                DumpHierarchy(sb, child, depth + 1);
            }
        }

        private static string Describe(Component c)
        {
            switch (c)
            {
                case MeshFilter mf:
                    return mf.sharedMesh == null ? "no mesh" : $"mesh={mf.sharedMesh.name} verts={mf.sharedMesh.vertexCount} bounds={mf.sharedMesh.bounds} uv2={mf.sharedMesh.uv2?.Length} colors={mf.sharedMesh.colors?.Length}";
                case Renderer r:
                    return string.Join(" | ", r.sharedMaterials.Select(DescribeMaterial)) + $" shadows={r.shadowCastingMode} rlayer={r.renderingLayerMask}";
                case BoxCollider b:
                    return $"center={b.center} size={b.size} trigger={b.isTrigger}";
                case MeshCollider m:
                    return $"mesh={(m.sharedMesh != null ? m.sharedMesh.name : "-")} convex={m.convex}";
                default:
                    return DescribeFields(c);
            }
        }

        internal static string DescribeMaterial(Material m)
        {
            if (m == null) return "null";
            var props = new List<string>();
            Shader s = m.shader;
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                string n = s.GetPropertyName(i);
                switch (s.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        Texture tex = m.GetTexture(n);
                        props.Add($"{n}={(tex != null ? tex.name + "(" + tex.width + "x" + tex.height + ")" : "-")}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        props.Add($"{n}={m.GetColor(n)}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        props.Add($"{n}={m.GetFloat(n)}");
                        break;
                }
            }
            return $"mat={m.name} shader={s.name} kw=[{string.Join(",", m.shaderKeywords)}] {{{string.Join(" ", props)}}}";
        }

        private static string DescribeFields(Component c)
        {
            var parts = new List<string>();
            foreach (FieldInfo f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                object v;
                try { v = f.GetValue(c); } catch { continue; }
                string s = v switch
                {
                    null => "null",
                    UnityEngine.Object o => o != null ? o.name : "null",
                    Array a => $"[{a.Length}: {string.Join(",", a.Cast<object>().Take(6).Select(x => x is UnityEngine.Object uo ? (uo != null ? uo.name : "null") : x?.ToString()))}]",
                    _ => v.ToString()
                };
                parts.Add($"{f.Name}={s}");
            }
            return string.Join(" ", parts);
        }
    }
}
