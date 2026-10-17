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

namespace Odyssey.Diagnostics
{
    internal static class FrameProfiler
    {
        private const float Seconds = 5f, Settle = 1.5f;
        private static bool _running, _patched;
        private static HarmonyLib.Harmony _harmony;
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
            var log = Melon<TweaksMod>.Logger;
            CoreMod.ShowMessage($"FPS report: measuring about {Seconds * 2 + Settle + 14:0} s (the picture changes for a moment)...", Seconds * 2 + Settle + 14);
            var cleanFrames = new List<float>();
            var cleanCpu = new List<double>();
            var cleanRender = new List<double>();
            var cleanGpu = new List<double>();
            var timings = new FrameTiming[1];
            if (!_patched)
            {
                yield return null;
                float cleanEnd = Time.unscaledTime + Seconds;
                while (Time.unscaledTime < cleanEnd)
                {
                    yield return null;
                    Sample(timings, cleanFrames, cleanCpu, cleanRender, cleanGpu);
                }
            }
            try
            {
                PatchMods();
            }
            catch (Exception e)
            {
                log.Warning($"[fps] could not hook mod measurement: {e.Message}");
            }
            float settle = Time.unscaledTime + Settle;
            while (Time.unscaledTime < settle) yield return null;
            Ticks.Clear();
            Calls.Clear();
            var frames = new List<float>();
            var cpuMain = new List<double>();
            var cpuRender = new List<double>();
            var gpu = new List<double>();
            string[] counters = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count" };
            var recorders = counters.Select(c => ProfilerRecorder.StartNew(ProfilerCategory.Render, c)).ToArray();
            string[] physicsCounters = { "Active Dynamic Bodies", "Active Kinematic Bodies", "Static Colliders", "Dynamic Bodies Count", "Active Dynamic Bodies Count", "Active Kinematic Bodies Count",
                "Static Colliders Count", "Total Contacts Count", "Contacts", "Trigger Overlaps", "Broadphase Adds/Removes", "Narrowphase Touches", "Physics Queries Count" };
            var physicsRecorders = physicsCounters.Select(c => ProfilerRecorder.StartNew(ProfilerCategory.Physics, c)).ToArray();
            var physicsSums = new long[physicsCounters.Length];
            var counterSums = new long[counters.Length];
            int gc0 = GC.CollectionCount(0);
            float end = Time.unscaledTime + Seconds;
            Measuring = true;
            while (Time.unscaledTime < end)
            {
                yield return null;
                Sample(timings, frames, cpuMain, cpuRender, gpu);
                for (int i = 0; i < recorders.Length; i++)
                {
                    if (recorders[i].Valid) counterSums[i] += recorders[i].LastValue;
                }
                for (int i = 0; i < physicsRecorders.Length; i++)
                {
                    if (physicsRecorders[i].Valid) physicsSums[i] += physicsRecorders[i].LastValue;
                }
            }
            Measuring = false;
            if (!_watching) Unpatch();
            int frameCount = frames.Count;
            var sb = new StringBuilder();
            float avg = frames.Average();
            float cleanAvg = cleanFrames.Count > 0 ? cleanFrames.Average() : avg;
            sb.AppendLine($"FPS report {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (cleanFrames.Count > 0)
            {
                sb.AppendLine($"Clean (no measurement hooks), {cleanFrames.Count} frames in {Seconds:0} s:");
                sb.AppendLine("  " + FrameStats(cleanFrames));
                sb.AppendLine($"  CPU main thread: {Avg(cleanCpu)}, CPU render thread: {Avg(cleanRender)}, GPU: {Avg(cleanGpu)}");
            }
            else
            {
                sb.AppendLine("Clean measurement skipped: the hitch watcher keeps the hooks installed");
            }
            sb.AppendLine($"With measurement hooks (slower than clean, used only for the method list below), {frameCount} frames in {Seconds:0} s after {Settle:0.0} s settling:");
            sb.AppendLine("  " + FrameStats(frames));
            sb.AppendLine($"  CPU main thread: {Avg(cpuMain)}, CPU render thread: {Avg(cpuRender)}, GPU: {Avg(gpu)}");
            sb.AppendLine($"Screen {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, FPS limit {Application.targetFrameRate}, quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', GC gen0: {GC.CollectionCount(0) - gc0} in {Seconds:0} s");
            sb.AppendLine("Rendering (average per frame): " + string.Join(", ", counters.Select((c, i) => recorders[i].Valid ? $"{c} {counterSums[i] / Math.Max(1, frameCount)}" : $"{c} -")));
            foreach (var r in recorders) r.Dispose();
            var physicsValid = physicsCounters.Select((c, i) => physicsRecorders[i].Valid ? $"{c} {physicsSums[i] / Math.Max(1, frameCount)}" : null).Where(t => t != null).ToList();
            foreach (var r in physicsRecorders) r.Dispose();
            sb.AppendLine("Physics counters (average per frame): " + (physicsValid.Count > 0 ? string.Join(", ", physicsValid) : "no data"));
            var bodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            int awake = bodies.Count(b => !b.isKinematic && !b.IsSleeping());
            int colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Count(c => c.enabled && c.gameObject.activeInHierarchy);
            sb.AppendLine($"Physics: rigidbodies {bodies.Length} (awake dynamic {awake}, kinematic {bodies.Count(b => b.isKinematic)}), enabled colliders {colliders}, scenes {UnityEngine.SceneManagement.SceneManager.sceneCount}");
            foreach (var b in bodies.Where(b => !b.isKinematic && !b.IsSleeping()).Select(b => (body: b, count: b.GetComponentsInChildren<Collider>(false).Length)).OrderByDescending(t => t.count).Take(8))
            {
                sb.AppendLine($"  awake body {b.body.name}: {b.count} colliders, mass {b.body.mass:0}, speed {b.body.linearVelocity.magnitude:0.00} m/s");
            }

            sb.AppendLine();
            sb.AppendLine("Updates of mods and the game (ms per frame, most expensive first, measured with hooks; a method's time includes the methods it calls, so nested entries must not be added up):");
            foreach (var kv in Ticks.OrderByDescending(kv => kv.Value).Take(40))
            {
                double ms = kv.Value * 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frameCount);
                sb.AppendLine($"  {ms,7:0.000} ms  {kv.Key} ({Calls[kv.Key] / Math.Max(1, frameCount)} calls/frame)");
            }
            double perFrame = 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frameCount);
            double game = Ticks.Where(kv => kv.Key.StartsWith("game ") && !kv.Key.StartsWith("game step")).Sum(kv => kv.Value) * perFrame;
            double mods = Ticks.Where(kv => !kv.Key.StartsWith("game ")).Sum(kv => kv.Value) * perFrame;
            sb.AppendLine($"  all mods: {mods:0.00} ms per frame, game scripts: {game:0.00} ms per frame (of {avg:0.0} ms)");

            sb.AppendLine();
            sb.AppendLine("Visible objects (triangles, most first):");
            var groups = new Dictionary<string, (long tris, int count, int shadows)>();
            int visible = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
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
            sb.AppendLine($"  visible renderers: {visible}, triangles in total: {groups.Values.Sum(g => g.tris):N0}");
            foreach (var kv in groups.OrderByDescending(kv => kv.Value.tris).Take(20))
            {
                sb.AppendLine($"  {kv.Value.tris,10:N0} tris  x{kv.Value.count,-4} (shadow: {kv.Value.shadows})  {kv.Key}");
            }
            sb.AppendLine($"Lights: {UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled)}, playing particle systems: {UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.isPlaying)}");

            sb.AppendLine();
            sb.AppendLine("Experiments (2 s each, settings restored afterwards):");
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Camera mainCam = Camera.main;
            var camData = mainCam != null ? mainCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            var result = new float[1];
            yield return Measure(result);
            sb.AppendLine($"  as is: {result[0]:0} FPS");
            if (urp != null)
            {
                float shadowDistance = urp.shadowDistance;
                urp.shadowDistance = 0f;
                yield return Measure(result);
                urp.shadowDistance = shadowDistance;
                sb.AppendLine($"  without shadows: {result[0]:0} FPS");
            }
            if (camData != null)
            {
                bool post = camData.renderPostProcessing;
                camData.renderPostProcessing = false;
                yield return Measure(result);
                camData.renderPostProcessing = post;
                sb.AppendLine($"  without post-processing: {result[0]:0} FPS");
            }
            var particles = UnityEngine.Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None).Where(r => r.enabled).ToList();
            foreach (var r in particles) r.enabled = false;
            yield return Measure(result);
            foreach (var r in particles) if (r != null) r.enabled = true;
            sb.AppendLine($"  without particle rendering ({particles.Count}): {result[0]:0} FPS");
            if (mainCam != null)
            {
                float far = mainCam.farClipPlane;
                mainCam.farClipPlane = Mathf.Min(far, 300f);
                yield return Measure(result);
                mainCam.farClipPlane = far;
                sb.AppendLine($"  view distance 300 m (was {far:0} m): {result[0]:0} FPS");
            }
            sb.AppendLine($"  physics step {Time.fixedDeltaTime * 1000f:0.0} ms");
            sb.AppendLine("  " + GpuCulling.Status());

            string report = sb.ToString();
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "fps_report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            foreach (string line in report.Split('\n')) log.Msg("[fps] " + line.TrimEnd());
            CoreMod.ShowMessage($"FPS report saved: {1000f / cleanAvg:0} FPS avg - UserData/Mining/fps_report.txt", 8f);
            _running = false;
        }

        private static IEnumerator Measure(float[] result)
        {
            yield return null;
            yield return null;
            int frames = 0;
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 2f)
            {
                yield return null;
                frames++;
            }
            result[0] = frames / Mathf.Max(0.001f, Time.unscaledTime - start);
        }

        private static void Sample(FrameTiming[] timings, List<float> frames, List<double> cpuMain, List<double> cpuRender, List<double> gpu)
        {
            frames.Add(Time.unscaledDeltaTime * 1000f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                if (timings[0].cpuMainThreadFrameTime > 0) cpuMain.Add(timings[0].cpuMainThreadFrameTime);
                if (timings[0].cpuRenderThreadFrameTime > 0) cpuRender.Add(timings[0].cpuRenderThreadFrameTime);
                if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
            }
        }

        private static string FrameStats(List<float> frames)
        {
            float avg = frames.Average();
            var sorted = frames.OrderBy(f => f).ToList();
            float median = sorted[sorted.Count / 2];
            return $"average {avg:0.0} ms ({1000f / avg:0} FPS), median {median:0.0} ms ({1000f / median:0} FPS), worst 1% {sorted[(int)(sorted.Count * 0.99f)]:0.0} ms, max {sorted.Last():0.0} ms";
        }

        private static void Unpatch()
        {
            if (!_patched || _harmony == null) return;
            _harmony.UnpatchSelf();
            _patched = false;
            Names.Clear();
        }

        private static string Avg(List<double> v) => v.Count == 0 ? "no data" : $"{v.Average():0.0} ms";

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
        private static bool _watching;
        private static readonly Dictionary<string, long> FrameTicks = new Dictionary<string, long>();
        internal static readonly List<string> Hitches = new List<string>();

        internal static void StartWatch(float thresholdMs)
        {
            if (_watching) return;
            _watching = true;
            try
            {
                PatchMods();
            }
            catch (Exception e)
            {
                Melon<TweaksMod>.Logger.Warning($"[hitch] could not hook measurement: {e.Message}");
            }
            Measuring = true;
            MelonCoroutines.Start(Watch(thresholdMs));
        }

        internal static void StopWatch()
        {
            _watching = false;
            if (!_running) Unpatch();
        }

        internal static IEnumerable<string> Totals(int frames)
        {
            double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, frames);
            var parents = new HashSet<string> { "game FishNet.Runtime: NetworkReaderLoop.Update", "game step WorldPhysicsServerUpdater.PrePhysics", "game step WorldPhysicsServerUpdater.UpdateShipParts",
                "game step TrackedShipServer.UpdateParts", "game step TrackedShipServer.PrepareConsumers", "game Assembly-CSharp: ClientVisualsUpdater.Update" };
            return Ticks.Where(kv => !parents.Contains(kv.Key)).OrderByDescending(kv => kv.Value).Take(60).Select(kv => $"{kv.Value * toMs,8:0.000} ms/frame  {kv.Key} ({Calls[kv.Key] / Math.Max(1, frames)} calls/frame)");
        }

        internal static void ResetTotals()
        {
            Ticks.Clear();
            Calls.Clear();
        }

        private static IEnumerator Watch(float thresholdMs)
        {
            int gc = GC.CollectionCount(0);
            while (_watching)
            {
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                int gcNow = GC.CollectionCount(0);
                if (ms > thresholdMs)
                {
                    double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    string top = string.Join(", ", FrameTicks.Where(kv => !kv.Key.StartsWith("game Assembly-CSharp: ClientVisualsUpdater")).OrderByDescending(kv => kv.Value).Take(6)
                        .Select(kv => $"{kv.Key} {kv.Value * toMs:0.0}"));
                    string line = $"{ms:0} ms (gc +{gcNow - gc}): {top}";
                    Hitches.Add(line);
                    Melon<TweaksMod>.Logger.Msg("[hitch] " + line);
                }
                gc = gcNow;
                FrameTicks.Clear();
            }
            Measuring = false;
        }

        private static void PatchMods()
        {
            if (_patched) return;
            _patched = true;
            var harmony = _harmony = new HarmonyLib.Harmony("mining.fpsprofiler");
            var prefix = new HarmonyMethod(typeof(FrameProfiler), nameof(Prefix));
            var postfix = new HarmonyMethod(typeof(FrameProfiler), nameof(Postfix));
            string[] methods = { "Update", "LateUpdate", "FixedUpdate", "OnUpdate", "OnLateUpdate", "OnFixedUpdate", "OnGUI" };
            int count = 0;
            var modAssemblies = new HashSet<Assembly>();
            foreach (MelonBase melon in MelonBase.RegisteredMelons)
            {
                Assembly asm = melon.MelonAssembly?.Assembly;
                if (asm == null || !modAssemblies.Add(asm)) continue;
                count += PatchAssembly(harmony, asm, melon.Info.Name, methods, prefix, postfix, true);
                count += PatchHarmonyHooks(harmony, asm, melon.Info.Name, prefix, postfix);
            }
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (modAssemblies.Contains(asm) || asm.IsDynamic) continue;
                string name = asm.GetName().Name;
                string location;
                try { location = asm.Location ?? ""; }
                catch (Exception) { continue; }
                if (!location.Replace(System.IO.Path.DirectorySeparatorChar, '/').Contains("_Data/Managed")) continue;
                if (name.StartsWith("Unity") || name.StartsWith("System") || name.StartsWith("Mono") || name == "mscorlib" || name == "netstandard" || name.StartsWith("Newtonsoft")) continue;
                count += PatchAssembly(harmony, asm, "game " + name, methods, prefix, postfix, false);
            }
            count += PatchDeep(harmony, prefix, postfix);
            Melon<TweaksMod>.Logger.Msg($"[fps] measurement hooked into {count} mod and game methods");
        }

        private static readonly string[] DeepRoots = { "Managers.Server.WorldPhysicsServerUpdater", "Managers.Client.ClientVisualsUpdater" };
        private static readonly string[] DeepPrefixes = { "Update", "Sync", "Simulate", "Prepare", "Process", "Post", "Pre", "Ship", "Objects", "Calculate", "Write" };

        private static int PatchDeep(HarmonyLib.Harmony harmony, HarmonyMethod prefix, HarmonyMethod postfix)
        {
            int count = 0;
            var done = new HashSet<MethodBase>(Names.Keys);
            foreach (string rootName in DeepRoots)
            {
                Type root = AccessTools.TypeByName(rootName);
                if (root == null) continue;
                var types = new List<Type> { root };
                foreach (FieldInfo f in root.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    Type ft = f.FieldType;
                    if (ft.IsClass && !ft.IsGenericType && ft.Assembly != typeof(UnityEngine.Object).Assembly && !ft.Namespace?.StartsWith("System") == true && !types.Contains(ft)) types.Add(ft);
                }
                foreach (Type type in types)
                {
                    foreach (MethodInfo m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (m.IsAbstract || m.ContainsGenericParameters || m.IsSpecialName || m.GetMethodBody() == null || done.Contains(m)) continue;
                        if (type != root && !DeepPrefixes.Any(px => m.Name.StartsWith(px))) continue;
                        Names[m] = $"game step {type.Name}.{m.Name}";
                        done.Add(m);
                        try
                        {
                            harmony.Patch(m, prefix, postfix);
                            count++;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            var extra = new List<Type>();
            Type shipServer = AccessTools.TypeByName("Ships.TrackedShipServer");
            if (shipServer != null)
            {
                extra.Add(shipServer);
                try
                {
                    foreach (Type type in shipServer.Assembly.GetTypes())
                    {
                        if (type.IsClass && !type.IsGenericType && (type.Name.EndsWith("Updater") || type.Name.EndsWith("Updaters"))) extra.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                }
            }
            foreach (Type type in extra)
            {
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters || m.IsSpecialName || m.GetMethodBody() == null || done.Contains(m)) continue;
                    if (!DeepPrefixes.Any(px => m.Name.StartsWith(px))) continue;
                    Names[m] = $"game step {type.Name}.{m.Name}";
                    done.Add(m);
                    try
                    {
                        harmony.Patch(m, prefix, postfix);
                        count++;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return count;
        }

        private static int PatchHarmonyHooks(HarmonyLib.Harmony harmony, Assembly asm, string label, HarmonyMethod prefix, HarmonyMethod postfix)
        {
            int count = 0;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            catch (Exception) { return 0; }
            foreach (Type type in types)
            {
                if (type == typeof(FrameProfiler) || type.ContainsGenericParameters || !type.GetCustomAttributes(typeof(HarmonyPatch), true).Any()) continue;
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters || m.GetMethodBody() == null || Names.ContainsKey(m)) continue;
                    if (m.Name == "TargetMethod" || m.Name == "TargetMethods" || m.Name == "Prepare" || m.Name == "Cleanup") continue;
                    if (m.GetCustomAttributes(typeof(HarmonyPatch), true).Any() == false && m.Name != "Prefix" && m.Name != "Postfix" && m.Name != "Transpiler" && m.Name != "Finalizer"
                        && !m.GetCustomAttributes(true).Any(a => a is HarmonyPrefix || a is HarmonyPostfix)) continue;
                    if (m.Name == "Transpiler") continue;
                    Names[m] = $"{label} hook: {type.Name}.{m.Name}";
                    try
                    {
                        harmony.Patch(m, prefix, postfix);
                        count++;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return count;
        }

        private static int PatchAssembly(HarmonyLib.Harmony harmony, Assembly asm, string label, string[] methods, HarmonyMethod prefix, HarmonyMethod postfix, bool mod)
        {
            int count = 0;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            catch (Exception) { return 0; }
            foreach (Type type in types)
            {
                if (type.IsAbstract && !type.IsSealed || type.ContainsGenericParameters) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(type) && !(mod && typeof(MelonBase).IsAssignableFrom(type))) continue;
                foreach (string name in methods)
                {
                    MethodInfo m = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (m == null || m.IsAbstract || m.GetMethodBody() == null) continue;
                    Names[m] = $"{label}: {type.Name}.{name}";
                    try
                    {
                        harmony.Patch(m, prefix, postfix);
                        count++;
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return count;
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
            if (_watching)
            {
                FrameTicks.TryGetValue(key, out long f);
                FrameTicks[key] = f + dt;
            }
        }
    }
}
