using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Mining.Patches
{
    // Obrys podglądu budowania (QuickOutline) przy pierwszym wyborze części liczy "wygładzone normalne" każdej siatki,
    // ale przy każdym wierzchołku pobiera z silnika kopię całej tablicy normalnych (mesh.normals w pętli) - czas rośnie
    // z kwadratem liczby wierzchołków: brama 16x8 to ok. 6 s przycięcia, brama 10x5 ok. 2,4 s. Ten sam wynik liczymy raz
    // na tablicach (średnia normalnych wierzchołków w tym samym punkcie).
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
