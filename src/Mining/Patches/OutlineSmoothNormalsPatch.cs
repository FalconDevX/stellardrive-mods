using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch]
    internal static class OutlineSmoothNormalsPatch
    {
        private static bool Prepare() => TargetMethod() != null;

        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Outline"), "SmoothNormals", new[] { typeof(Mesh) });

        private static bool Prefix(Mesh mesh, ref List<Vector3> __result)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var result = new List<Vector3>(normals);
            if (normals.Length != vertices.Length)
            {
                __result = result;
                return false;
            }
            var sums = new Dictionary<Vector3, Vector3>(vertices.Length);
            var counts = new Dictionary<Vector3, int>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out Vector3 s);
                sums[vertices[i]] = s + normals[i];
                counts.TryGetValue(vertices[i], out int c);
                counts[vertices[i]] = c + 1;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                if (counts[vertices[i]] > 1)
                {
                    result[i] = sums[vertices[i]].normalized;
                }
            }
            __result = result;
            return false;
        }
    }
}
