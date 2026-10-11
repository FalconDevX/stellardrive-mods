using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using Ships.Physics.Collision;
using Ships.Rooms;
using Ships.Services;
using Ships.Visuals;
using UnityEngine;

namespace Mining.Patches
{
    // Szybsze budowanie/usuwanie: po każdej zmianie kadłuba gra przebudowuje siatkę całego fragmentu 16x16x16 i dla
    // KAŻDEGO kafelka i krawędzi pobiera z silnika jego wierzchołki, normalne i trójkąty (Mesh.GetVertices itd.) -
    // przy dużym statku to tysiące kopii z silnika na jedno postawienie bloku. Kafelki to stałe modele z gry,
    // więc ich dane zapamiętujemy raz i potem tylko kopiujemy z pamięci. Wynikowa siatka jest identyczna.
    [HarmonyPatch]
    internal static class HullMeshCachePatch
    {
        private sealed class Data
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public int[] Triangles;
        }

        private static readonly Dictionary<int, Data> Cache = new Dictionary<int, Data>();
        private static readonly List<Vector3> TmpV3 = new List<Vector3>();
        private static readonly List<int> TmpInt = new List<int>();
        private static Mesh _lastMesh;
        private static Data _lastData;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(HullMeshBuilder), "BuildHullMesh");
            yield return AccessTools.Method(typeof(HullMeshBuilder), "BuildEdgeMesh");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getVertices = AccessTools.Method(typeof(Mesh), nameof(Mesh.GetVertices), new[] { typeof(List<Vector3>) });
            MethodInfo getNormals = AccessTools.Method(typeof(Mesh), nameof(Mesh.GetNormals), new[] { typeof(List<Vector3>) });
            MethodInfo getTangents = AccessTools.Method(typeof(Mesh), nameof(Mesh.GetTangents), new[] { typeof(List<Vector4>) });
            MethodInfo getTriangles = AccessTools.Method(typeof(Mesh), nameof(Mesh.GetTriangles), new[] { typeof(List<int>), typeof(int) });
            int replaced = 0;
            foreach (CodeInstruction code in instructions)
            {
                if (code.Calls(getVertices)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(HullMeshCachePatch), nameof(GetVertices)); replaced++; }
                else if (code.Calls(getNormals)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(HullMeshCachePatch), nameof(GetNormals)); replaced++; }
                else if (code.Calls(getTangents)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(HullMeshCachePatch), nameof(GetTangents)); replaced++; }
                else if (code.Calls(getTriangles)) { code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(HullMeshCachePatch), nameof(GetTriangles)); replaced++; }
                yield return code;
            }
            if (replaced == 0)
            {
                Melon<MiningMod>.Logger.Warning("HullMeshCachePatch: nie znaleziono wywołań Mesh.Get* - gra się zmieniła?");
            }
        }

        private static Data Get(Mesh mesh)
        {
            if (ReferenceEquals(mesh, _lastMesh) && _lastData != null)
            {
                return _lastData;
            }
            int key = mesh.GetInstanceID();
            if (!Cache.TryGetValue(key, out Data data))
            {
                data = new Data();
                mesh.GetVertices(TmpV3);
                data.Vertices = TmpV3.ToArray();
                mesh.GetNormals(TmpV3);
                data.Normals = TmpV3.ToArray();
                mesh.GetTriangles(TmpInt, 0);
                data.Triangles = TmpInt.ToArray();
                Cache[key] = data;
            }
            _lastMesh = mesh;
            _lastData = data;
            return data;
        }

        private static void GetVertices(Mesh mesh, List<Vector3> list)
        {
            list.Clear();
            list.AddRange(Get(mesh).Vertices);
        }

        private static void GetNormals(Mesh mesh, List<Vector3> list)
        {
            list.Clear();
            list.AddRange(Get(mesh).Normals);
        }

        // Gra pobiera styczne, ale ich nie używa (liczy własne) - pomijamy.
        private static void GetTangents(Mesh mesh, List<Vector4> list) => list.Clear();

        private static void GetTriangles(Mesh mesh, List<int> list, int submesh)
        {
            list.Clear();
            list.AddRange(Get(mesh).Triangles);
        }
    }

    // Dziennik przycięć: jeśli przebudowa kadłuba, kolizji albo pomieszczeń po zmianie statku trwa długo,
    // zapisujemy to w logu MelonLoadera (max raz na sekundę na rodzaj) - wiadomo, co jeszcze przyspieszać.
    [HarmonyPatch]
    internal static class BuildSpikeLog
    {
        private const double ThresholdMs = 6.0;
        private static readonly Dictionary<string, float> LastLog = new Dictionary<string, float>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ShipHullVisuals), nameof(ShipHullVisuals.UpdateHull));
            yield return AccessTools.Method(typeof(ShipHullColliders), nameof(ShipHullColliders.UpdateColliders));
            yield return AccessTools.Method(typeof(ShipRoomsVoxelUpdater), nameof(ShipRoomsVoxelUpdater.UpdateRooms));
        }

        private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();

        private static void Postfix(long __state, MethodBase __originalMethod)
        {
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            if (ms < ThresholdMs)
            {
                return;
            }
            string name = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name;
            if (LastLog.TryGetValue(name, out float last) && Time.realtimeSinceStartup - last < 1f)
            {
                return;
            }
            LastLog[name] = Time.realtimeSinceStartup;
            Melon<MiningMod>.Logger.Msg($"[perf] {name}: {ms:F1} ms");
        }
    }
}
