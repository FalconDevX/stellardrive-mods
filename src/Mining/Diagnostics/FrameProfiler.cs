using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Unity.Profiling;
using UnityEngine;

namespace Mining.Diagnostics
{
    // F9: raport wydajności z 5 s gry (dla gracza, nie tylko twórcy): czasy klatek (CPU/GPU, jeśli gra je podaje),
    // statystyki renderowania, ile czasu zajmują Update każdego moda (osobno każda klasa) i co widoczne ma najwięcej
    // trójkątów. Wynik: UserData/Mining/fps_report.txt i log MelonLoadera.
    internal static class FrameProfiler
    {
        private const float Seconds = 5f;
        private static bool _running, _patched;
        private static readonly Dictionary<string, long> Ticks = new Dictionary<string, long>();
        private static readonly Dictionary<string, int> Calls = new Dictionary<string, int>();
        private static readonly Dictionary<MethodBase, string> Names = new Dictionary<MethodBase, string>();

        internal static void Start()
        {
            if (_running) return;
            _running = true;
            MelonCoroutines.Start(Run());
        }

        private static IEnumerator Run()
        {
            var log = Melon<MiningMod>.Logger;
            MiningMod.ShowMessage($"FPS report: measuring {Seconds:0} s...", Seconds);
            try
            {
                PatchMods();
            }
            catch (Exception e)
            {
                log.Warning($"[fps] nie udało się podpiąć pomiaru modów: {e.Message}");
            }
            Ticks.Clear();
            Calls.Clear();
            var frames = new List<float>();
            var cpuMain = new List<double>();
            var cpuRender = new List<double>();
            var gpu = new List<double>();
            var timings = new FrameTiming[1];
            string[] counters = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count" };
            var recorders = counters.Select(c => ProfilerRecorder.StartNew(ProfilerCategory.Render, c)).ToArray();
            var counterSums = new long[counters.Length];
            int gc0 = GC.CollectionCount(0);
            float end = Time.unscaledTime + Seconds;
            Measuring = true;
            while (Time.unscaledTime < end)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    if (timings[0].cpuMainThreadFrameTime > 0) cpuMain.Add(timings[0].cpuMainThreadFrameTime);
                    if (timings[0].cpuRenderThreadFrameTime > 0) cpuRender.Add(timings[0].cpuRenderThreadFrameTime);
                    if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
                }
                for (int i = 0; i < recorders.Length; i++)
                {
                    if (recorders[i].Valid) counterSums[i] += recorders[i].LastValue;
                }
            }
            Measuring = false;
            int frameCount = frames.Count;
            var sb = new StringBuilder();
            float avg = frames.Average();
            var sorted = frames.OrderBy(f => f).ToList();
            sb.AppendLine($"Raport FPS {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {frameCount} klatek w {Seconds:0} s");
            sb.AppendLine($"Klatka: średnio {avg:0.0} ms ({1000f / avg:0} FPS), mediana {sorted[sorted.Count / 2]:0.0} ms, 1% najgorszych {sorted[(int)(sorted.Count * 0.99f)]:0.0} ms, max {sorted.Last():0.0} ms");
            sb.AppendLine($"CPU wątek główny: {Avg(cpuMain)}, CPU wątek renderowania: {Avg(cpuRender)}, GPU: {Avg(gpu)}");
            sb.AppendLine($"Ekran {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, limit FPS {Application.targetFrameRate}, jakość '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', GC gen0: {GC.CollectionCount(0) - gc0} w {Seconds:0} s");
            sb.AppendLine("Renderowanie (średnio na klatkę): " + string.Join(", ", counters.Select((c, i) => recorders[i].Valid ? $"{c} {counterSums[i] / Math.Max(1, frameCount)}" : $"{c} -")));
            foreach (var r in recorders) r.Dispose();

            sb.AppendLine();
            sb.AppendLine("Update modów (ms na klatkę, najwięcej na górze):");
            foreach (var kv in Ticks.OrderByDescending(kv => kv.Value).Take(25))
            {
                double ms = kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frameCount);
                sb.AppendLine($"  {ms,7:0.000} ms  {kv.Key} ({Calls[kv.Key] / Math.Max(1, frameCount)} wywołań/klatkę)");
            }
            double total = Ticks.Values.Sum() * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frameCount);
            sb.AppendLine($"  razem mody: {total:0.00} ms na klatkę");

            sb.AppendLine();
            sb.AppendLine("Widoczne obiekty (trójkąty, najwięcej na górze):");
            var groups = new Dictionary<string, (long tris, int count, int shadows)>();
            int visible = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.isVisible) continue;
                visible++;
                long tris = 0;
                if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                {
                    Mesh m = mf.sharedMesh;
                    for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
                }
                else if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    tris = smr.sharedMesh.triangles.Length / 3;
                }
                string key = Describe(r);
                groups.TryGetValue(key, out var g);
                groups[key] = (g.tris + tris, g.count + 1, g.shadows + (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off ? 1 : 0));
            }
            sb.AppendLine($"  widocznych rendererów: {visible}, trójkątów razem: {groups.Values.Sum(g => g.tris):N0}");
            foreach (var kv in groups.OrderByDescending(kv => kv.Value.tris).Take(20))
            {
                sb.AppendLine($"  {kv.Value.tris,10:N0} tr.  x{kv.Value.count,-4} (cień: {kv.Value.shadows})  {kv.Key}");
            }
            sb.AppendLine($"Światła: {UnityEngine.Object.FindObjectsOfType<Light>().Count(l => l.isActiveAndEnabled)}, systemy cząsteczek grające: {UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Count(p => p.isPlaying)}");

            string report = sb.ToString();
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "fps_report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            foreach (string line in report.Split('\n')) log.Msg("[fps] " + line.TrimEnd());
            MiningMod.ShowMessage($"FPS report saved: {1000f / avg:0} FPS avg - UserData/Mining/fps_report.txt", 8f);
            _running = false;
        }

        private static string Avg(List<double> v) => v.Count == 0 ? "brak danych" : $"{v.Average():0.0} ms";

        // Grupa obiektu: korzeń i nazwa obiektu (bez numerów kopii), np. "Ship 12 / Body".
        private static string Describe(Renderer r)
        {
            string root = r.transform.root.name;
            int i = root.IndexOf(' ');
            if (root.StartsWith("Ship", StringComparison.Ordinal) && i > 0) root = "Ship";
            string name = r.name.Replace("(Clone)", "").Trim();
            Transform parent = r.transform.parent;
            if (parent != null && parent != r.transform.root) name = parent.name.Replace("(Clone)", "").Trim() + "/" + name;
            return root + " / " + name;
        }

        internal static bool Measuring;

        // Pomiar Update/LateUpdate/FixedUpdate wszystkich klas MonoBehaviour z modów i OnUpdate samych modów.
        private static void PatchMods()
        {
            if (_patched) return;
            _patched = true;
            var harmony = new HarmonyLib.Harmony("mining.fpsprofiler");
            var prefix = new HarmonyMethod(typeof(FrameProfiler), nameof(Prefix));
            var postfix = new HarmonyMethod(typeof(FrameProfiler), nameof(Postfix));
            string[] methods = { "Update", "LateUpdate", "FixedUpdate", "OnUpdate", "OnLateUpdate", "OnFixedUpdate", "OnGUI" };
            int count = 0;
            foreach (MelonBase melon in MelonBase.RegisteredMelons)
            {
                Assembly asm = melon.MelonAssembly?.Assembly;
                if (asm == null) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in types)
                {
                    if (type.IsAbstract && !type.IsSealed || type.ContainsGenericParameters) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type) && !typeof(MelonBase).IsAssignableFrom(type)) continue;
                    foreach (string name in methods)
                    {
                        MethodInfo m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (m == null || m.IsAbstract) continue;
                        Names[m] = $"{melon.Info.Name}: {type.Name}.{name}";
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            count++;
                        }
                        catch (Exception)
                        {
                            // metoda nie do podpięcia - pomijamy
                        }
                    }
                }
            }
            Melon<MiningMod>.Logger.Msg($"[fps] pomiar podpięty do {count} metod modów");
        }

        private static void Prefix(out long __state) => __state = Measuring ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

        private static void Postfix(long __state, MethodBase __originalMethod)
        {
            if (__state == 0 || !Measuring) return;
            long dt = System.Diagnostics.Stopwatch.GetTimestamp() - __state;
            string key = Names.TryGetValue(__originalMethod, out string n) ? n : __originalMethod.Name;
            Ticks.TryGetValue(key, out long t);
            Ticks[key] = t + dt;
            Calls.TryGetValue(key, out int c);
            Calls[key] = c + 1;
        }
    }
}
