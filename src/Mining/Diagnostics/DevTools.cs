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

namespace Mining.Diagnostics
{
    // Narzędzia dla twórcy moda (domyślnie wyłączone): automatyczne wczytanie świata z menu,
    // zrzut budowy części/materiałów do pliku i zrzuty ekranu.
    internal static class DevTools
    {
        private static bool _autoLoaded;

        // Czy gracz pisze w polu tekstowym (np. nazwa beacona) - wtedy skróty klawiszowe moda nie działają.
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

        internal static void OnSceneLoaded(string sceneName)
        {
            string world = MiningMod.DevAutoLoadWorld.Value;
            if (!_autoLoaded && !string.IsNullOrEmpty(world) && sceneName != "MainGame" && File.Exists(world))
            {
                _autoLoaded = true;
                MelonCoroutines.Start(LoadWorld(world));
            }
            if (sceneName == "MainGame" && MiningMod.DevDump.Value)
            {
                MelonCoroutines.Start(DumpAfterDelay(25f));
            }
        }

        private static IEnumerator LoadWorld(string world)
        {
            yield return new WaitForSeconds(3f);
            Melon<MiningMod>.Logger.Msg($"[dev] wczytuję świat {world}");
            MainGameSceneLoadData.Mode = MainGameSceneLoadData.GameMode.SinglePlayer;
            MainGameSceneLoadData.SaveFileFullName = world;
            SceneManager.LoadScene("MainGame", LoadSceneMode.Single);
        }

        private static IEnumerator DumpAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            try
            {
                Dump();
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"[dev] zrzut nie powiódł się: {e}");
            }
            if (MiningMod.DevTest.Value)
            {
                yield return RunVisualTest();
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "screen.png"));
            if (MiningMod.DevQuitAfterDump.Value)
            {
                yield return new WaitForSeconds(3f);
                Application.Quit();
            }
        }

        // Podgląd bez budowania: koparka (z pracującym wiertłem) i najbliższe złoże przed kamerą + zrzuty ekranu.
        private static IEnumerator RunVisualTest()
        {
            var log = Melon<MiningMod>.Logger;
            PartSettings part = new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().FirstOrDefault(p => p.id == Content.Excavator.PartId);
            log.Msg($"[test] część koparki: {(part != null ? "jest" : "BRAK")}");
            foreach (ResearchSettingsList list in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                log.Msg($"[test] lista badań {list.name}: " + string.Join("; ", list.list.Where(r => r != null && r.availableInDemo && !r.hideFromTechTree).Select(r => $"{r.techName}@{r.positionInTechTree}")));
            }
            Camera cam = Camera.main;
            if (part == null || cam == null)
            {
                yield break;
            }
            Vector3 up = cam.transform.up;
            if (WorldTracking.Interface.Values.ClientOrigin.LocalVisualSpace is WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace surface)
            {
                var transpose = Core.Services.ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeClient>();
                var planetSpace = new WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Core.Values.Vector3Double.Zero };
                Vector3 camPlanet = (Vector3)transpose.Position(surface, planetSpace, (Core.Values.Vector3Double)cam.transform.position);
                up = transpose.Rotation(planetSpace, surface, Quaternion.identity) * camPlanet.normalized;
            }
            int planetMask = LayerMask.GetMask("Planet");
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
            Vector3 ground = cam.transform.position + fwd * 5f;
            if (Physics.Raycast(ground + up * 20f, -up, out RaycastHit hit, 60f, planetMask))
            {
                ground = hit.point;
            }
            log.Msg($"[test] ziemia: {(hit.collider != null ? hit.collider.name : "brak trafienia")}");
            var visualTemplate = part.part.GetComponentInChildren<Content.ExcavatorVisuals>(true).gameObject;
            GameObject excavator = UnityEngine.Object.Instantiate(visualTemplate, ground, Quaternion.LookRotation(-fwd, up));
            excavator.SetActive(true);
            excavator.GetComponent<Content.ExcavatorVisuals>().ForceWorking = true;

            Sites.MiningSite site = Sites.MiningSiteVisuals.FindNearest(3000f, out float distance);
            log.Msg($"[test] najbliższe złoże: {(site != null ? site.Kind.Name + $" {distance:0} m, promień {site.Radius:0.0}" : "brak")}");
            log.Msg($"[test] {Sites.MiningSiteVisuals.DescribeNearest()}");
            Vector3 side = Vector3.Cross(up, fwd);
            PartSettings chest = new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().FirstOrDefault(p => p.id == 30);
            if (chest != null)
            {
                GameObject chestVisual = UnityEngine.Object.Instantiate(chest.part.GetComponentInChildren<Ships.Parts.Common.DefaultShipPartVisuals>(true).gameObject,
                    ground - side * 2.2f + up * 0.05f, Quaternion.LookRotation(-fwd, up));
                chestVisual.SetActive(true);
                var sb = new StringBuilder();
                DumpHierarchy(sb, excavator.transform, 0);
                DumpHierarchy(sb, chestVisual.transform, 0);
                foreach (MeshFilter mf in chestVisual.GetComponentsInChildren<MeshFilter>(true).Concat(excavator.GetComponentsInChildren<MeshFilter>(true)))
                {
                    Mesh m = mf.sharedMesh;
                    if (m == null) continue;
                    sb.AppendLine($"mesh {m.name}: readable={m.isReadable} sub={m.subMeshCount}");
                    if (!m.isReadable) continue;
                    var uv = new List<Vector2>(); m.GetUVs(0, uv);
                    var uv2 = new List<Vector2>(); m.GetUVs(1, uv2);
                    var uv3 = new List<Vector4>(); m.GetUVs(2, uv3);
                    var cols = m.colors;
                    sb.AppendLine($"  uv0 {Range(uv)} uv1 {Range(uv2)} uv2count={uv3.Count} colors={cols.Length} tangents={m.tangents.Length} normals={m.normals.Length}");
                    if (uv2.Count > 0) sb.AppendLine($"  uv1 sample: {string.Join(" ", uv2.Take(8))}");
                    if (cols.Length > 0) sb.AppendLine($"  colors sample: {string.Join(" ", cols.Take(5))}");
                }
                File.WriteAllText(Path.Combine(OutDir, "test_objects.txt"), sb.ToString());
            }
            if (site != null)
            {
                GameObject preview = Sites.MiningSiteVisuals.Build(site);
                Vector3 sitePos = ground + fwd * 6f + side * 3f;
                if (Physics.Raycast(sitePos + up * 20f, -up, out RaycastHit siteHit, 60f, planetMask)) sitePos = siteHit.point;
                preview.transform.position = sitePos;
                preview.transform.rotation = Quaternion.LookRotation(fwd, up);
            }
            yield return new WaitForSeconds(4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test1.png"));
            yield return new WaitForSeconds(1f);

            // Złoże w prawdziwym miejscu, oglądane z osobnej kamery (kopia ustawień głównej, z postprocesem i konturami).
            if (site != null && WorldTracking.Interface.Values.ClientOrigin.LocalVisualSpace is WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace surface2)
            {
                var transpose = Core.Services.ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeClient>();
                var planetSpace = new WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace { PlanetId = surface2.PlanetId, Center = Core.Values.Vector3Double.Zero };
                Vector3 camPlanet2 = (Vector3)transpose.Position(surface2, planetSpace, (Core.Values.Vector3Double)cam.transform.position);
                float cellSize = Sites.MiningSites.CellSize(surface2.PlanetId);
                var targets = Sites.MiningSites.Near(surface2.PlanetId, camPlanet2, Mathf.CeilToInt(4000f / cellSize))
                    .GroupBy(x => x.Kind.Key)
                    .Select(g => g.OrderBy(x => (x.Center - camPlanet2).sqrMagnitude).First())
                    .ToList();
                log.Msg($"[test] złoża do zdjęć: {string.Join(", ", targets.Select(t => $"{t.Kind.Name} {(t.Center - camPlanet2).magnitude:0} m"))}");
                var testCam = new GameObject("MiningTestCam").AddComponent<Camera>();
                testCam.CopyFrom(cam);
                var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var camData = testCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (mainData != null)
                {
                    camData.renderPostProcessing = mainData.renderPostProcessing;
                    camData.antialiasing = mainData.antialiasing;
                }
                for (int shot = 0; shot < targets.Count * 2; shot++)
                {
                    Sites.MiningSite target = targets[shot / 2];
                    GameObject real = Sites.MiningSiteVisuals.Build(target);
                    for (int frame = 0; frame < 30; frame++)
                    {
                        Vector3 center = (Vector3)transpose.Position(planetSpace, surface2, (Core.Values.Vector3Double)target.Center);
                        Quaternion rot = transpose.Rotation(planetSpace, surface2, target.Rotation);
                        real.transform.SetPositionAndRotation(center, rot);
                        Vector3 siteUp = rot * Vector3.up;
                        Vector3 siteFwd = rot * Vector3.forward;
                        float dist = shot % 2 == 0 ? 10f : 5f;
                        testCam.transform.position = center + siteUp * dist * 0.8f - siteFwd * dist;
                        testCam.transform.rotation = Quaternion.LookRotation(center - testCam.transform.position, siteUp);
                        cam.enabled = false;
                        yield return null;
                    }
                    ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, $"test_site_{target.Kind.Key}{shot % 2}.png"));
                    yield return null;
                    yield return null;
                    UnityEngine.Object.Destroy(real);
                }
                cam.enabled = true;
                UnityEngine.Object.Destroy(testCam.gameObject);

                // Logika serwera: czy punkt nad złożem i obok niego jest rozpoznany, i czy dokładanie rudy wypełnia 4 sloty po 100.
                Sites.MiningSite s0 = targets.FirstOrDefault();
                if (s0 != null)
                {
                    var onSite = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Direction * 0.5f + s0.Rotation * Vector3.right * 2f, 1f, 3f);
                    var offSite = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Rotation * Vector3.right * (s0.Radius + 4f), 1f, 3f);
                    var high = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Direction * 10f, 1f, 3f);
                    log.Msg($"[test] serwer: na złożu={(onSite != null ? onSite.Kind.Name : "nie")} obok={(offSite != null ? "TAK (błąd)" : "nie")} 10 m nad={(high != null ? "TAK (błąd)" : "nie")}");
                }
                var add = typeof(Server.ExcavatorServer).GetMethod("TryAddItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                object state = new Ships.Interface.Model.Parts.State.StorageState();
                int added = 0;
                for (int i = 0; i < 450; i++)
                {
                    object[] args = { state, 101u, null };
                    if (!(bool)add.Invoke(null, args)) break;
                    state = args[2];
                    added++;
                }
                var st = (Ships.Interface.Model.Parts.State.StorageState)state;
                log.Msg($"[test] serwer: dodano {added} rudy, sloty: {string.Join(", ", Enumerable.Range(0, 5).Select(i => st.GetSlot((byte)i).HasItem ? st.GetSlot((byte)i).Quantity.ToString() : "-"))}");
            }

            // Drzewo badań (jak po otwarciu stołu badawczego).
            var activator = UnityEngine.Object.FindObjectOfType<UI.Research.GenericPartOpenableMenuActivator>();
            GameObject menuPrefab = activator != null ? Traverse.Create(activator).Field("researchMenuPrefab").GetValue<GameObject>() : null;
            if (menuPrefab != null)
            {
                GameObject menu = UnityEngine.Object.Instantiate(menuPrefab, activator.transform);
                yield return new WaitForSeconds(1.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test_research.png"));
                yield return new WaitForSeconds(0.5f);
                UnityEngine.Object.Destroy(menu);
            }
            else
            {
                log.Msg("[test] brak menu badań");
            }

            // Miniaturka do menu budowania.
            var thumbs = UnityEngine.Object.FindObjectOfType<Ships.Services.ShipPartsThumbnailRuntimeCache>();
            Texture2D thumb = thumbs != null ? thumbs.GetLargeThumbnail(Content.Excavator.PartId) : null;
            log.Msg($"[test] miniaturka: {(thumb != null ? thumb.width + "x" + thumb.height : "brak")}");
            if (thumb != null)
            {
                var rt = RenderTexture.GetTemporary(thumb.width, thumb.height);
                Graphics.Blit(thumb, rt);
                var copy = new Texture2D(thumb.width, thumb.height, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, thumb.width, thumb.height), 0, 0);
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                var encode = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                if (encode != null) File.WriteAllBytes(Path.Combine(OutDir, "test_thumb.png"), (byte[])encode.Invoke(null, new object[] { copy }));
            }

            // Prawdziwa część na statku z save'a (obok stołu badawczego), z ręcznie dokładaną rudą, żeby ruszyło wiertło.
            var shipsServer = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            Ships.TrackedShipServer ship = shipsServer?.Ships.FirstOrDefault(sh => sh.GetAllStatefulParts<Ships.Interface.Model.Parts.State.ResearchTableState>().Any());
            if (ship == null)
            {
                log.Msg("[test] brak statku ze stołem badawczym");
                yield break;
            }
            var table = ship.GetAllStatefulParts<Ships.Interface.Model.Parts.State.ResearchTableState>().First();
            Vector3 coord = table.LocalPosition + new Vector3(0f, 0f, -3f);
            ushort partId = ship.AddStatefulPart(part, new Ships.Interface.Model.Placement.FloatingPartPlacement(coord,
                Ships.Interface.Model.Placement.PartOrientation.Forward, Ships.Interface.Model.Placement.PartOrientation.Up));
            log.Msg($"[test] dodano koparkę na statku {ship.Id} jako część {partId} w {coord} (stół w {table.LocalPosition})");
            yield return new WaitForSeconds(2f);

            var shipsClient = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsClientTracker>();
            var testCam2 = new GameObject("MiningTestCam2").AddComponent<Camera>();
            testCam2.CopyFrom(cam);
            var data2 = testCam2.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData2 = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData2 != null) data2.renderPostProcessing = mainData2.renderPostProcessing;
            for (int shot = 0; shot < 3; shot++)
            {
                for (int frame = 0; frame < 90; frame++)
                {
                    if (shot > 0 && frame % 20 == 0)
                    {
                        ship.TryGetChestSlot(partId, 0, out InventorySlot slot);
                        ship.SetChestSlot(partId, 0, new InventorySlot { HasItem = true, ItemId = 112, Quantity = (byte)(slot.Quantity + 1) });
                    }
                    if (shipsClient != null && shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(ship.Id, partId), out var visual))
                    {
                        Transform t = visual.GameObject.transform;
                        Vector3 offset = shot == 2 ? (t.forward * 1.5f - t.right * 3.5f + t.up * 0.6f) : (-t.forward * 4.5f + t.right * 1.5f + t.up * 2.2f);
                        testCam2.transform.position = t.position + offset;
                        testCam2.transform.rotation = Quaternion.LookRotation(t.position + t.up * 0.7f - testCam2.transform.position, t.up);
                        cam.enabled = false;
                    }
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, $"test_ship{shot}.png"));
                yield return null;
                yield return null;
            }
            cam.enabled = true;
            UnityEngine.Object.Destroy(testCam2.gameObject);
        }

        private static string Range(List<Vector2> list)
        {
            if (list.Count == 0) return "-";
            return $"[{list.Min(v => v.x):0.###},{list.Min(v => v.y):0.###}]..[{list.Max(v => v.x):0.###},{list.Max(v => v.y):0.###}] n={list.Count}";
        }

        private static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== PARTS ==");
            PartSettings[] parts = Resources.FindObjectsOfTypeAll<PartSettings>().OrderBy(p => p.id).ToArray();
            foreach (PartSettings p in parts)
            {
                sb.AppendLine($"{p.id} '{p.fullLabel}' ({p.name}) type={p.GetType().Name} state={p.internalStateClass} create={p.customCreationClass} " +
                              $"snap={p.snappingStyle} size={p.size} mass={p.mass} prefab={(p.part != null ? p.part.name : "-")} thumbRot={p.thumbnailRotation.eulerAngles} " +
                              $"cost=[{string.Join(", ", SafeCost(p))}]");
            }

            sb.AppendLine("== ITEMS ==");
            foreach (ItemSettings i in Resources.FindObjectsOfTypeAll<ItemSettings>().OrderBy(i => i.id))
            {
                sb.AppendLine($"{i.id} '{i.itemName}' ({i.name}) stack={i.maxStackSize} mass={i.mass} fluid={i.isFluid} obj={(i.itemObject != null ? i.itemObject.name : "-")} icon={(i.icon != null ? i.icon.name : "-")}");
            }

            sb.AppendLine("== RESEARCH ==");
            foreach (ResearchSettings r in Resources.FindObjectsOfTypeAll<ResearchSettings>().OrderBy(r => r.id))
            {
                sb.AppendLine($"{r.id} '{r.techName}' ({r.name}) pos={r.positionInTechTree} demo={r.availableInDemo} hide={r.hideFromTechTree} " +
                              $"cost=[{string.Join(", ", (r.resourceCost ?? new ItemInstance[0]).Select(c => c.ItemDrop.ItemId + "x" + c.ItemDrop.Quantity))}] " +
                              $"features=[{string.Join(", ", ((System.Collections.IEnumerable)r.unlockedFeatures ?? new object[0]).Cast<object>().Select(f => f?.ToString()))}] guide=[{string.Join(",", r.unlockedGuideEntriesIds ?? new uint[0])}]");
            }

            foreach (string label in new[] { "Storage Chest", "Electrolyzer", "Wood-Fired Generator", "Research Table", "Floor", "Ethanol Distiller" })
            {
                PartSettings p = parts.FirstOrDefault(x => x.fullLabel == label);
                if (p?.part == null)
                {
                    continue;
                }
                sb.AppendLine($"== PREFAB {label} ==");
                DumpHierarchy(sb, p.part.transform, 0);
            }

            sb.AppendLine("== AUDIO ==");
            sb.AppendLine($"unity audio config: {AudioSettings.GetConfiguration().sampleRate} Hz, speakers {AudioSettings.GetConfiguration().speakerMode}");
            sb.AppendLine($"AudioListeners: {UnityEngine.Object.FindObjectsOfType<AudioListener>().Length}, FMOD listeners: {UnityEngine.Object.FindObjectsOfType<FMODUnity.StudioListener>().Length}");

            sb.AppendLine("== LAYERS ==");
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) sb.AppendLine($"{i}: {n}");
            }

            string path = Path.Combine(OutDir, "dump.txt");
            File.WriteAllText(path, sb.ToString());
            Melon<MiningMod>.Logger.Msg($"[dev] zrzut zapisany: {path}");
        }

        private static IEnumerable<string> SafeCost(PartSettings p)
        {
            try
            {
                return p.BuildingCost.Select(c => c.ItemId + "x" + c.Quantity).ToList();
            }
            catch
            {
                return new[] { "?" };
            }
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

        private static string DescribeMaterial(Material m)
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
