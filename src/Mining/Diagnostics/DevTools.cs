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
    internal static class DevTools
    {
        private static bool _autoLoaded;
        private static bool _dumpStarted;

        internal static bool IsTyping()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null);
        }

        private static string Arg(string name)
        {
            string prefix = "--mining-dev-" + name + "=";
            string arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(prefix));
            return arg?.Substring(prefix.Length).Trim('"');
        }

        private static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains("--mining-dev-" + name);

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
            string world = Arg("world") ?? MiningMod.DevAutoLoadWorld.Value;
            if (!_autoLoaded && !string.IsNullOrEmpty(world) && sceneName != "MainGame" && File.Exists(world))
            {
                _autoLoaded = true;
                MelonCoroutines.Start(LoadWorld(world));
            }
            if (sceneName == "MainGame" && (HasArg("dump") || MiningMod.DevDump.Value) && !_dumpStarted)
            {
                _dumpStarted = true;
                MelonCoroutines.Start(DumpAfterDelay(25f));
            }
        }

        private static IEnumerator LoadWorld(string world)
        {
            yield return new WaitForSeconds(3f);
            Melon<MiningMod>.Logger.Msg($"[dev] loading world {world}");
            MainGameSceneLoadData.Mode = MainGameSceneLoadData.GameMode.SinglePlayer;
            MainGameSceneLoadData.SaveFileFullName = world;
            SceneManager.LoadScene("MainGame", LoadSceneMode.Single);
        }

        private static IEnumerator RunPerfTest()
        {
            var log = Melon<MiningMod>.Logger;
            var shipsServer = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            var floor = new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().FirstOrDefault(p => p.id == 70) as HullPartSettings;
            Ships.TrackedShipServer ship = shipsServer?.Ships.OrderByDescending(sh => sh.GetStateSnapshot().HullParts.Length).FirstOrDefault();
            log.Msg($"[perf] ship {ship?.Id}: {ship?.GetStateSnapshot().HullParts.Length} hull parts, {ship?.StatefulParts.Count()} parts");
            if (floor == null || ship == null)
            {
                log.Msg("[perf] no ship or floor");
                yield break;
            }
            var frames = new List<float>();
            for (int i = 0; i < 120; i++) { yield return null; frames.Add(Time.unscaledDeltaTime * 1000f); }
            log.Msg($"[perf] idle: average {frames.Average():0.0} ms, max {frames.Max():0.0} ms");

            var call = new List<double>();
            frames.Clear();
            var positions = new List<Ships.Interface.Model.Placement.HullPartPosition>();
            for (int i = 1; i <= 30; i++)
            {
                var placement = new Ships.Interface.Model.Placement.HullPartPlacement(new Vector3Int(i, 40, 0), Ships.Interface.Model.VoxelPartType.Floor,
                    (Ships.Interface.Model.Placement.PartOrientation)4, (Ships.Interface.Model.Placement.PartOrientation)1);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                ship.AddHullPart(floor, placement);
                call.Add(sw.Elapsed.TotalMilliseconds);
                positions.Add(placement.Position);
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
            }
            for (int i = 0; i < 30; i++) { yield return null; frames.Add(Time.unscaledDeltaTime * 1000f); }
            log.Msg($"[perf] placing: server call average {call.Average():0.00} ms (max {call.Max():0.00}), frames average {frames.Average():0.0} ms, max {frames.Max():0.0} ms");

            call.Clear();
            frames.Clear();
            foreach (var position in positions.AsEnumerable().Reverse())
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                ship.RemoveHullPart(position);
                call.Add(sw.Elapsed.TotalMilliseconds);
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
            }
            for (int i = 0; i < 30; i++) { yield return null; frames.Add(Time.unscaledDeltaTime * 1000f); }
            log.Msg($"[perf] removing: server call average {call.Average():0.00} ms (max {call.Max():0.00}), frames average {frames.Average():0.0} ms, max {frames.Max():0.0} ms");

            PartSettings rotor = Content.RotorUnlock.Rotor;
            ushort rotorId = ship.AddStatefulPart(rotor, new Ships.Interface.Model.Placement.FloatingPartPlacement(new Vector3(0f, -0.46f, 0.25f),
                Ships.Interface.Model.Placement.PartOrientation.Forward, Ships.Interface.Model.Placement.PartOrientation.Up));
            yield return new WaitForSeconds(1f);
            ship.TryGetStatefulPart(rotorId, out var rotorPart);
            var rotorState = rotorPart?.State as Ships.Interface.Model.Parts.State.RotorState;
            var child = rotorState != null ? shipsServer.GetShip(rotorState.AnchoredShipId) : null;
            log.Msg($"[rotor] placed: {rotorPart != null}, plate (sub-ship): {(child != null ? child.Id.ToString() : "MISSING")}");
            if (child != null)
            {
                Quaternion before = Quaternion.Inverse(ship.LocalPhysicsState.Rotation) * child.LocalPhysicsState.Rotation;
                Server.RotorDegreesPatch.TestTargetDegrees = 90f;
                yield return new WaitForSeconds(0.5f);
                Quaternion mid = Quaternion.Inverse(ship.LocalPhysicsState.Rotation) * child.LocalPhysicsState.Rotation;
                yield return new WaitForSeconds(2f);
                Quaternion after = Quaternion.Inverse(ship.LocalPhysicsState.Rotation) * child.LocalPhysicsState.Rotation;
                Server.RotorDegreesPatch.TestTargetDegrees = null;
                ship.TryGetStatefulPart(rotorId, out rotorPart);
                log.Msg($"[rotor] signal 90: after 0.5 s the plate turned {Quaternion.Angle(before, mid):0.0}°, after 2.5 s {Quaternion.Angle(before, after):0.0}° " +
                        $"(rotor angle {((Ships.Interface.Model.Parts.State.RotorState)rotorPart.State).Angle * Mathf.Rad2Deg:0.0}°)");
            }
        }

        private static IEnumerator RunJetpackTest()
        {
            var log = Melon<MiningMod>.Logger;
            var players = Core.Services.ServiceLocator.GetService<Players.Services.PlayersServerTracker>();
            Players.TrackedPlayerServer player = players?.ActivePlayers.FirstOrDefault();
            if (player == null)
            {
                log.Msg("[jetpack] no player");
                yield break;
            }
            var net = Traverse.Create(player).Property("NetworkState").GetValue<Players.Network.PlayerNetworkState>();
            net.ServerSetInventorySlot(Content.Jetpack.FuelSlot, new InventorySlot { HasItem = true, ItemId = Content.Jetpack.FuelItem, Quantity = 10 });
            float oldSeconds = MiningMod.JetpackSecondsPerWood.Value;
            MiningMod.JetpackSecondsPerWood.Value = 1f;
            yield return new WaitForSeconds(0.5f);
            Vector3 start = player.PhysicsState.Position;
            Vector3 up = player.PhysicsState.Rotation * Vector3.up;
            log.Msg($"[jetpack] unlocked: {Content.Jetpack.IsUnlocked()}, wood in slot: {net.Inventory[Content.Jetpack.FuelSlot].Quantity}");
            var step = AccessTools.Method(typeof(Players.TrackedPlayerServer), "DoPlayerPhysicsStep");
            var jump = new Players.LocalClient.Controls.Model.PlayerControlInput { MoveInput = Vector3.up };
            step.Invoke(player, new object[] { jump, 0.02f });
            Content.Jetpack.TestForceUp = true;
            float maxHeight = 0f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                maxHeight = Mathf.Max(maxHeight, Vector3.Dot(player.PhysicsState.Position - start, up));
                yield return null;
            }
            Content.Jetpack.TestForceUp = false;
            MiningMod.JetpackSecondsPerWood.Value = oldSeconds;
            var inventoryUi = UnityEngine.Object.FindObjectOfType<UI.Inventory.PlayerInventoryActivator>(true);
            inventoryUi?.ToggleInventory();
            yield return new WaitForSeconds(1f);
            ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test_jetpack_inventory.png"));
            yield return new WaitForSeconds(0.5f);
            inventoryUi?.ToggleInventory();
            log.Msg($"[jetpack] after 3 s of thrust: height {Vector3.Dot(player.PhysicsState.Position - start, up):0.0} m (max {maxHeight:0.0} m), wood in slot: {net.Inventory[Content.Jetpack.FuelSlot].Quantity}");
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
                Melon<MiningMod>.Logger.Error($"[dev] dump failed: {e}");
            }
            if (HasArg("jetpack") || MiningMod.DevJetpack.Value)
            {
                yield return RunJetpackTest();
            }
            else if (HasArg("perf") || MiningMod.DevPerf.Value)
            {
                yield return RunPerfTest();
            }
            else if (HasArg("test") || MiningMod.DevTest.Value)
            {
                yield return RunVisualTest();
            }
            else if (HasArg("pump"))
            {
                yield return RunPumpTest();
            }
            else if (HasArg("tank"))
            {
                yield return RunTankTest();
            }
            else if (HasArg("fps"))
            {
                FrameProfiler.Start();
                yield return new WaitForSeconds(30f);
            }
            else if (HasArg("doors"))
            {
                yield return RunDoorsTest();
            }
            else if (HasArg("seat"))
            {
                yield return RunSeatTest();
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "screen.png"));
            if (HasArg("quit") || MiningMod.DevQuitAfterDump.Value)
            {
                yield return new WaitForSeconds(3f);
                Application.Quit();
            }
        }

        private static IEnumerator RunVisualTest()
        {
            var log = Melon<MiningMod>.Logger;
            PartSettings part = new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().FirstOrDefault(p => p.id == Content.Excavator.PartId);
            log.Msg($"[test] excavator part: {(part != null ? "present" : "MISSING")}");
            foreach (ResearchSettingsList list in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                log.Msg($"[test] research list {list.name}: " + string.Join("; ", list.list.Where(r => r != null && r.availableInDemo && !r.hideFromTechTree).Select(r => $"{r.techName}@{r.positionInTechTree}")));
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
            log.Msg($"[test] ground: {(hit.collider != null ? hit.collider.name : "no hit")}");
            var visualTemplate = part.part.GetComponentInChildren<Content.ExcavatorVisuals>(true).gameObject;
            GameObject excavator = UnityEngine.Object.Instantiate(visualTemplate, ground, Quaternion.LookRotation(-fwd, up));
            excavator.SetActive(true);
            excavator.GetComponent<Content.ExcavatorVisuals>().ForceWorking = true;

            var provider = new Ships.Interface.Services.PartSettingsProvider();
            foreach (var (id, offset) in new[] { (Content.HydrogenThruster.PartId, -2.2f), (Content.ElectrolyzerPart.PartId, 2.4f), (Content.Reactor.PartId, 5.6f) })
            {
                PartSettings extra = provider.GetAllPartSettings().FirstOrDefault(p => p.id == id);
                log.Msg($"[test] part {id}: {(extra != null ? extra.fullLabel : "MISSING")}");
                var visualComp = extra?.part.GetComponentInChildren<Ships.Parts.Common.Model.Reactions.IShipPartVisual>(true);
                if (visualComp != null)
                {
                    GameObject v = UnityEngine.Object.Instantiate(visualComp.GameObject, ground + Vector3.Cross(up, fwd) * offset, Quaternion.LookRotation(-fwd, up));
                    v.SetActive(true);
                }
            }

            if (Content.Reactor.Large != null)
            {
                GameObject large = UnityEngine.Object.Instantiate(Content.Reactor.Large.part, ground + Vector3.Cross(up, fwd) * -7.5f + fwd * 3f, Quaternion.LookRotation(fwd, up));
                large.SetActive(true);
                log.Msg($"[test] large reactor placed, sockets: {large.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true).Length}");
            }

            var storageMenu = UnityEngine.Object.FindObjectsOfType<UI.Inventory.StorageMenuActivator>(true).FirstOrDefault();
            log.Msg($"[test] electrolyzer window: {(storageMenu != null && Traverse.Create(storageMenu).Field("electrolyzerMenu").GetValue<GameObject>() != null ? "present" : "MISSING")}");

            Sites.MiningSite site = Sites.MiningSiteVisuals.FindNearest(3000f, out float distance);
            log.Msg($"[test] nearest deposit: {(site != null ? site.Kind.Name + $" {distance:0} m, radius {site.Radius:0.0}" : "none")}");
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
                log.Msg($"[test] deposits to photograph: {string.Join(", ", targets.Select(t => $"{t.Kind.Name} {(t.Center - camPlanet2).magnitude:0} m"))}");
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
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    GameObject real = Sites.MiningSiteVisuals.Build(target);
                    log.Msg($"[test] deposit build {target.Kind.Name}: {sw.Elapsed.TotalMilliseconds:0.0} ms, renderers: {real.GetComponentsInChildren<MeshRenderer>().Length}");
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

                Sites.MiningSite s0 = targets.FirstOrDefault();
                if (s0 != null)
                {
                    var onSite = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Direction * 0.5f + s0.Rotation * Vector3.right * 2f, 1f, 3f);
                    var offSite = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Rotation * Vector3.right * (s0.Radius + 4f), 1f, 3f);
                    var high = Sites.MiningSites.FindAt(s0.PlanetId, s0.Center + s0.Direction * 10f, 1f, 3f);
                    log.Msg($"[test] server: on deposit={(onSite != null ? onSite.Kind.Name : "no")} next to={(offSite != null ? "YES (bug)" : "no")} 10 m above={(high != null ? "YES (bug)" : "no")}");
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
                log.Msg($"[test] server: added {added} ore, slots: {string.Join(", ", Enumerable.Range(0, 5).Select(i => st.GetSlot((byte)i).HasItem ? st.GetSlot((byte)i).Quantity.ToString() : "-"))}");
            }

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
                log.Msg("[test] no research menu");
            }

            var thumbs = UnityEngine.Object.FindObjectOfType<Ships.Services.ShipPartsThumbnailRuntimeCache>();
            Texture2D thumb = thumbs != null ? thumbs.GetLargeThumbnail(Content.Excavator.PartId) : null;
            log.Msg($"[test] thumbnail: {(thumb != null ? thumb.width + "x" + thumb.height : "none")}");
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

            Vector3 up2 = Core.Services.ServiceLocator.GetService<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? cam.transform.up;
            Vector3 fwd2 = Vector3.ProjectOnPlane(cam.transform.forward, up2).normalized;
            Vector3 groundPos = cam.transform.position + fwd2 * 6f;
            if (WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(groundPos + up2 * 10f, -up2, out RaycastHit gHit, 30f, Tools.Common.RaycastMasks.BuildDetectionLayer))
            {
                groundPos = gHit.point;
                log.Msg($"[test] ground (client scene): {gHit.collider.name} tag={gHit.collider.tag}");
            }
            Quaternion rot2 = Quaternion.LookRotation(-fwd2, up2);
            Patches.CreateShipWithExcavatorPatch.AlignToTerrain(ref groundPos, ref rot2, up2);
            log.Msg($"[test] terrain slope under the excavator: {Vector3.Angle(rot2 * Vector3.up, up2):0.0}°");
            var shipsBefore = new HashSet<uint>(Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>().Ships.Select(sh => sh.Id));
            var interaction = UnityEngine.Object.FindObjectOfType<Managers.Server.Services.ShipInteractionService>();
            var conn = FishNet.InstanceFinder.ServerManager.Clients.Values.FirstOrDefault();
            var testPlayer = conn != null ? Core.Services.ServiceLocator.GetService<Players.Services.PlayersServerTracker>().GetTrackedPlayerFromConnectionId(conn.ClientId) : null;
            log.Msg($"[test] connection: {(conn != null ? conn.ClientId.ToString() : "none")}, player: {(testPlayer != null ? "present" : "none")}");
            testPlayer?.AddItemToInventory(new ItemDrop { ItemId = 101, Quantity = 30 });
            testPlayer?.AddItemToInventory(new ItemDrop { ItemId = 112, Quantity = 10 });
            var rpc = AccessTools.Method(typeof(Managers.Server.Services.ShipInteractionService), "RpcLogic___CreateShip_ServerRPC___2476380455");
            rpc.Invoke(interaction, new object[] { new Managers.Interface.Model.CreateShipNetworkRequest
            {
                PartId = Content.Excavator.PartId,
                WorldTransform = new WorldTracking.Interface.Model.WorldTransform { SpaceId = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpaceId, Position = groundPos, Rotation = rot2 },
            }, conn });
            uint shipId = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>().Ships.Select(sh => sh.Id).FirstOrDefault(id => !shipsBefore.Contains(id));
            log.Msg($"[test] excavator ship: {shipId}");
            UnityEngine.Object.Destroy(excavator);
            yield return new WaitForSeconds(3f);

            var shipsServer = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            Ships.TrackedShipServer ship = shipsServer.GetShip(shipId);
            var shipsClient = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsClientTracker>();
            var testCam2 = new GameObject("MiningTestCam2").AddComponent<Camera>();
            testCam2.CopyFrom(cam);
            var data2 = testCam2.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData2 = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData2 != null) data2.renderPostProcessing = mainData2.renderPostProcessing;
            for (int shot = 0; shot < 3; shot++)
            {
                for (int frame = 0; frame < (shot == 0 ? 90 : 240); frame++)
                {
                    if (ship != null && shot > 0 && frame % 20 == 0)
                    {
                        ship.TryGetChestSlot(1, 0, out InventorySlot slot);
                        ship.SetChestSlot(1, 0, new InventorySlot { HasItem = true, ItemId = 112, Quantity = (byte)(slot.Quantity + 1) });
                    }
                    if (shipsClient != null && shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(shipId, 1), out var visual))
                    {
                        Transform t = visual.GameObject.transform;
                        var ev = visual.GameObject.GetComponent<Content.ExcavatorVisuals>();
                        if (ev != null) ev.ForceWorking = shot > 0;
                        Vector3 offset = shot == 0 ? (-t.forward * 4.2f + t.right * 2.2f + t.up * 1.8f)
                            : shot == 1 ? (t.right * 3.8f + t.up * 1.3f + t.forward * 0.6f)
                            : (t.forward * 3.8f - t.right * 1.8f + t.up * 1.2f);
                        testCam2.transform.position = t.position + offset;
                        testCam2.transform.rotation = Quaternion.LookRotation(t.position + t.up * 0.8f - testCam2.transform.position, up2);
                        cam.enabled = false;
                    }
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, $"test_ground{shot}.png"));
                yield return null;
                yield return null;
            }
            foreach (var socket in UnityEngine.Object.FindObjectsOfType<Ships.Cables.VisualObjects.SocketInteractionObject>().Where(o => o.EntityPartSocket.EntityId == shipId))
            {
                Vector3 target = socket.transform.position;
                Vector3 outward = (target - (ship != null && shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(shipId, 1), out var v2) ? v2.GameObject.transform.position + v2.GameObject.transform.up * 0.42f : target - up2)).normalized;
                Vector3 from = target + outward * 1.2f + up2 * 0.3f;
                bool any = WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(from, (target - from).normalized, out RaycastHit sh, 3f, ~0, QueryTriggerInteraction.Collide);
                log.Msg($"[test] socket {socket.EntityPartSocket.Socket.SocketId}: ray hits {(any ? sh.collider.name + " (layer " + LayerMask.LayerToName(sh.collider.gameObject.layer) + ")" : "nothing")}" +
                        $"{(any && sh.collider.GetComponentInParent<Ships.Cables.VisualObjects.SocketInteractionObject>() == socket ? " = SOCKET OK" : "")}");
            }
            if (shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(shipId, 1), out var v3))
            {
                Transform t3 = v3.GameObject.transform;
                Vector3 aim = t3.position + t3.up * 1.05f - t3.forward * 0.6f;
                Vector3 eye = t3.position + t3.up * 1.6f - t3.forward * 2.2f;
                bool hitAny = WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(eye, (aim - eye).normalized, out RaycastHit ih, 4f, ~0, QueryTriggerInteraction.Collide);
                log.Msg($"[test] opening: ray hits {(hitAny ? ih.collider.name + " (layer " + LayerMask.LayerToName(ih.collider.gameObject.layer) + ")" : "nothing")}" +
                        $"{(hitAny && ih.collider.GetComponent<Ships.Parts.Common.PartInteraction>() != null ? " = OPEN ZONE OK" : "")}");
            }
            if (ship != null) log.Msg($"[test] ship after 8 s: position {ship.LocalPhysicsState.Position}, tilt {Vector3.Angle(ship.LocalPhysicsState.Rotation * Vector3.up, up2):0.0}°");
            cam.enabled = true;
            UnityEngine.Object.Destroy(testCam2.gameObject);
        }

        private static IEnumerator RunPumpTest()
        {
            var log = Melon<MiningMod>.Logger;
            Camera cam = Camera.main;
            var transpose = Core.Services.ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeClient>();
            if (cam == null || transpose == null || !(WorldTracking.Interface.Values.ClientOrigin.LocalVisualSpace is WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace surface))
            {
                log.Msg("[pump] no camera/planet");
                yield break;
            }
            var planetSpace = new WorldTracking.Interface.Model.Spaces.PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Core.Values.Vector3Double.Zero };
            Vector3 camPlanet = (Vector3)transpose.Position(surface, planetSpace, (Core.Values.Vector3Double)cam.transform.position);
            Vector3 up = camPlanet.normalized;
            Vector3 t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized, t2 = Vector3.Cross(up, t1);
            Vector3 best = Vector3.zero;
            float bestDist = float.MaxValue;
            for (float x = -4000f; x <= 4000f; x += 50f)
            {
                for (float y = -4000f; y <= 4000f; y += 50f)
                {
                    Vector3 p = (camPlanet + t1 * x + t2 * y).normalized * camPlanet.magnitude;
                    if (x * x + y * y < bestDist && Content.WaterPump.TryProbe(surface.PlanetId, p, out _, out bool w) && w &&
                        Content.WaterPump.TryProbe(surface.PlanetId, (p + t1 * 12f).normalized * p.magnitude, out _, out bool w2) && w2 &&
                        Content.WaterPump.TryProbe(surface.PlanetId, (p - t2 * 12f).normalized * p.magnitude, out _, out bool w3) && w3)
                    {
                        bestDist = x * x + y * y;
                        best = p.normalized;
                    }
                }
            }
            if (bestDist == float.MaxValue)
            {
                log.Msg("[pump] no lake within 4 km");
                yield break;
            }
            var tracked = Sites.MiningSites.FindTrackedPlanet(surface.PlanetId);
            float radius = tracked.Planet.Radius;
            Vector3 hoverPlanet = best * (radius + Content.WaterPump.SeaLevel + 6f);
            log.Msg($"[pump] lake {Mathf.Sqrt(bestDist):0} m from the camera");

            var physicsSpace = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpace;
            Vector3 pos = (Vector3)transpose.Position(planetSpace, physicsSpace, (Core.Values.Vector3Double)hoverPlanet);
            Vector3 fwdPlanet = Vector3.Cross(Vector3.Cross(best, t1), best).normalized;
            Quaternion rot = transpose.Rotation(planetSpace, physicsSpace, Quaternion.LookRotation(fwdPlanet, best));
            var shipsServer = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            var shipsBefore = new HashSet<uint>(shipsServer.Ships.Select(sh => sh.Id));
            var interaction = UnityEngine.Object.FindObjectOfType<Managers.Server.Services.ShipInteractionService>();
            var conn = FishNet.InstanceFinder.ServerManager.Clients.Values.FirstOrDefault();
            var testPlayer = conn != null ? Core.Services.ServiceLocator.GetService<Players.Services.PlayersServerTracker>().GetTrackedPlayerFromConnectionId(conn.ClientId) : null;
            testPlayer?.AddItemToInventory(new ItemDrop { ItemId = 101, Quantity = 30 });
            testPlayer?.AddItemToInventory(new ItemDrop { ItemId = 112, Quantity = 10 });
            testPlayer?.AddItemToInventory(new ItemDrop { ItemId = 102, Quantity = 10 });
            Patches.ServerCreateShipPatch.CreateExcavatorShip(Content.WaterPump.Part,
                new WorldTracking.Interface.Model.WorldTransform { SpaceId = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpaceId, Position = pos, Rotation = rot });
            uint shipId = shipsServer.Ships.Select(sh => sh.Id).FirstOrDefault(id => !shipsBefore.Contains(id));
            Ships.TrackedShipServer ship = shipsServer.GetShip(shipId);
            log.Msg($"[pump] pump ship: {(ship != null ? shipId.ToString() : "MISSING")}");
            if (ship == null)
            {
                yield break;
            }
            yield return null;
            var hold = ship.PhysicsState;
            ushort partId = ship.StatefulParts.First().Id;
            var shipsClient = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsClientTracker>();
            var testCam = new GameObject("PumpTestCam").AddComponent<Camera>();
            testCam.CopyFrom(cam);
            var data = testCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;
            Audio.LoopEmitter.ListenerOverride = testCam;

            for (int shot = 0; shot < 4; shot++)
            {
                if (shot == 1)
                {
                    ship.OpenDoor(partId);
                }
                for (int frame = 0; frame < (shot == 1 ? 420 : 90); frame++)
                {
                    ship.SetPhysicsState(new Ships.Interface.Model.State.ShipPhysicsState(hold.SpaceId, hold.Position, hold.Rotation, Vector3.zero));
                    if (shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(shipId, partId), out var visual))
                    {
                        Transform t = visual.GameObject.transform;
                        Vector3 offset = shot == 0 ? (-t.forward * 1.8f + t.right * 1.0f + t.up * 1.3f)
                            : shot == 1 ? (-t.forward * 5f + t.right * 4f - t.up * 2.5f)
                            : shot == 2 ? (-t.forward * 11f + t.right * 4f + t.up * 1f)
                            : (-t.forward * 1.6f + t.right * 0.6f + t.up * 1.2f);
                        Vector3 look = shot == 2 ? t.position - t.up * 3f : shot == 1 ? t.position - t.up * 3f : t.position + t.up * 0.45f;
                        testCam.transform.position = t.position + offset;
                        testCam.transform.rotation = Quaternion.LookRotation(look - testCam.transform.position, t.up);
                        cam.enabled = false;
                    }
                    if (shot == 3 && frame == 30)
                    {
                        var pumpInteraction = Resources.FindObjectsOfTypeAll<Content.WaterPumpInteraction>().FirstOrDefault();
                        if (pumpInteraction != null) pumpInteraction.HoseDown = true;
                        var tips = UnityEngine.Object.FindObjectOfType<UI.Interaction.InteractionTooltipsActivator>();
                        log.Msg($"[pump] interaction: {(pumpInteraction != null ? "present, hose " + (pumpInteraction.HoseDown ? "lowered" : "retracted") : "MISSING")}, tooltips: {(tips != null ? "present" : "MISSING")}");
                        if (pumpInteraction != null && tips != null)
                        {
                            AccessTools.Method(typeof(UI.Interaction.InteractionTooltipsActivator), "OnPartHover").Invoke(tips, new object[] { pumpInteraction });
                        }
                    }
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, $"test_pump{shot}.png"));
                yield return null;
                yield return null;
                var update = AccessTools.Method(typeof(Server.WaterPumpServer), "UpdateParts");
                for (int i = 0; i < 3; i++) update.Invoke(null, new object[] { ship, 2f });
                if (shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(shipId, partId), out var v))
                {
                    var pv = v.GameObject.GetComponent<Content.WaterPumpVisuals>();
                    log.Msg($"[pump] shot {shot}: hose {(pv != null ? Traverse.Create(pv).Field<float>("_extension").Value.ToString("0.00") + " m (target " + Traverse.Create(pv).Field<float>("_target").Value.ToString("0.00") + ", in water " + Traverse.Create(pv).Field<bool>("_inWater").Value + ")" : "no pump visuals")}");
                }
            }
            Audio.LoopEmitter.ListenerOverride = null;
            cam.enabled = true;
            UnityEngine.Object.Destroy(testCam.gameObject);
        }

        private static IEnumerator RunTankTest()
        {
            var log = Melon<MiningMod>.Logger;
            Camera cam = Camera.main;
            var transpose = Core.Services.ServiceLocator.GetService<WorldTracking.Interface.Services.IWorldTransposeClient>();
            var visualSpace = WorldTracking.Interface.Values.ClientOrigin.LocalVisualSpace;
            if (cam == null || transpose == null || visualSpace == null || Content.BigFluidTank.Get(Content.BigFluidTank.LargeId) == null)
            {
                log.Msg($"[tank] no camera/space/part (part: {(Content.BigFluidTank.Get(Content.BigFluidTank.LargeId) != null ? "present" : "MISSING")})");
                yield break;
            }
            PartSettings small = new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().FirstOrDefault(p => p.id == Content.BigFluidTank.TankPartId);
            var physicsSpace = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpace;
            Vector3 up = Core.Services.ServiceLocator.GetService<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.ProjectOnPlane(cam.transform.up, up).normalized;
            Vector3 right = Vector3.Cross(up, fwd);
            var shipsServer = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsServerTracker>();
            var shipsClient = Core.Services.ServiceLocator.GetService<Ships.Services.ShipsClientTracker>();

            uint Spawn(PartSettings part, Vector3 visualPos)
            {
                Vector3 pos = (Vector3)transpose.Position(visualSpace, physicsSpace, (Core.Values.Vector3Double)visualPos);
                Quaternion rot = transpose.Rotation(visualSpace, physicsSpace, Quaternion.LookRotation(-fwd, up));
                return Patches.ServerCreateShipPatch.CreateExcavatorShip(part,
                    new WorldTracking.Interface.Model.WorldTransform { SpaceId = WorldTracking.Interface.Values.ClientOrigin.LocalPhysicsSpaceId, Position = pos, Rotation = rot });
            }
            Vector3 basePos = cam.transform.position + fwd * 12f + up * 1f;
            uint bigId = Spawn(Content.BigFluidTank.Get(Content.BigFluidTank.LargeId), basePos);
            uint smallId = small != null ? Spawn(small, basePos - right * 4f) : 0;
            uint mediumId = Content.BigFluidTank.Get(218) != null ? Spawn(Content.BigFluidTank.Get(218), basePos - right * 8f) : 0;
            uint reactorId = Content.Reactor.Part != null ? Spawn(Content.Reactor.Part, basePos + right * 6f) : 0;
            Ships.TrackedShipServer reactorShip = shipsServer.GetShip(reactorId);
            uint thrusterId = Content.HydrogenThruster.Part != null ? Spawn(Content.HydrogenThruster.Part, basePos + right * 10f) : 0;
            Ships.TrackedShipServer big = shipsServer.GetShip(bigId), smallShip = shipsServer.GetShip(smallId);
            log.Msg($"[tank] ships: large {(big != null ? bigId.ToString() : "MISSING")}, small {(smallShip != null ? smallId.ToString() : "MISSING")}");
            if (big == null)
            {
                yield break;
            }
            yield return null;
            ushort bigPart = big.StatefulParts.First().Id;
            big.UpdatePartState(bigPart, new Ships.Interface.Model.Parts.State.FluidTankState().WithFluid(105, Content.BigFluidTank.Capacity(4) * 0.5f, 0f));
            if (smallShip != null)
            {
                smallShip.UpdatePartState(smallShip.StatefulParts.First().Id, new Ships.Interface.Model.Parts.State.FluidTankState().WithFluid(105, 50f, 0f));
            }
            if (reactorShip != null)
            {
                Ships.Interface.Model.Parts.StatefulPart rp = reactorShip.StatefulParts.First();
                reactorShip.UpdatePartState(rp.Id, ((Ships.Interface.Model.Parts.State.WoodPowerGeneratorState)rp.State) with { NormalizedProductionLevel = 1f });
            }
            var holdBig = big.PhysicsState;
            var holdSmall = smallShip != null ? smallShip.PhysicsState : holdBig;
            var testCam = new GameObject("TankTestCam").AddComponent<Camera>();
            testCam.CopyFrom(cam);
            var data = testCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;

            var holdReactor = reactorShip != null ? reactorShip.PhysicsState : holdBig;
            for (int shot = 0; shot < 7; shot++)
            {
                for (int frame = 0; frame < (shot == 4 ? 240 : 90); frame++)
                {
                    if (reactorShip != null) reactorShip.SetPhysicsState(new Ships.Interface.Model.State.ShipPhysicsState(holdReactor.SpaceId, holdReactor.Position, holdReactor.Rotation, Vector3.zero));
                    if (shot >= 4)
                    {
                        if (reactorShip != null && shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(reactorId, reactorShip.StatefulParts.First().Id), out var rv))
                        {
                            Transform rt = rv.GameObject.transform;
                            var reactorLook = rv.GameObject.GetComponentInChildren<Content.ReactorVisuals>();
                            if (reactorLook != null) Traverse.Create(reactorLook).Field("_target").SetValue(1f);
                            Vector3 rc = rt.position + rt.up * 0.8f;
                            Vector3 ro = shot == 4 ? (-rt.forward * 3.2f + rt.right * 1.6f + rt.up * 1.2f) : shot == 5 ? (rt.forward * 3.2f - rt.right * 1.4f + rt.up * 1.0f) : (rt.up * 3.2f - rt.forward * 1.2f);
                            testCam.transform.position = rc + ro;
                            testCam.transform.rotation = Quaternion.LookRotation(rc - testCam.transform.position, rt.up);
                            cam.enabled = false;
                            Audio.LoopEmitter.ListenerOverride = testCam;
                        }
                        yield return null;
                        continue;
                    }
                    big.SetPhysicsState(new Ships.Interface.Model.State.ShipPhysicsState(holdBig.SpaceId, holdBig.Position, holdBig.Rotation, Vector3.zero));
                    if (smallShip != null) smallShip.SetPhysicsState(new Ships.Interface.Model.State.ShipPhysicsState(holdSmall.SpaceId, holdSmall.Position, holdSmall.Rotation, Vector3.zero));
                    if (shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(bigId, bigPart), out var visual))
                    {
                        Transform t = visual.GameObject.transform;
                        Vector3 center = t.TransformPoint(Content.BigFluidTank.Get(Content.BigFluidTank.LargeId).part.GetComponent<Ships.Interface.Model.Parts.ShipPartBounds>().center);
                        Vector3 offset = shot == 0 ? (-t.forward * 9f - t.right * 3f + t.up * 1.5f)
                            : shot == 1 ? (t.forward * 9f + t.right * 4f + t.up * 2f)
                            : shot == 2 ? (-t.right * 9f + t.up * 6f)
                            : (t.right * 9f - t.forward * 2f);
                        testCam.transform.position = center + offset;
                        testCam.transform.rotation = Quaternion.LookRotation(center - testCam.transform.position, t.up);
                        cam.enabled = false;
                        if (frame == 0 && shot == 0)
                        {
                            var sb = new System.Text.StringBuilder();
                            DumpHierarchy(sb, t, 0);
                            File.WriteAllText(Path.Combine(OutDir, "test_tank_visual.txt"), sb.ToString());
                        }
                    }
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, $"test_tank{shot}.png"));
                yield return null;
                yield return null;
            }
            foreach (var (label, id) in new[] { ("reactor", reactorId), ("thruster", thrusterId), ("tank4", bigId), ("tank2", mediumId) })
            {
                var sh = shipsServer.GetShip(id);
                if (sh == null || !shipsClient.TryGetVisualPart(new Ships.Interface.Model.Parts.PartContext(id, sh.StatefulParts.First().Id), out var pv))
                {
                    log.Msg($"[sockets] {label}: no visuals");
                    continue;
                }
                Transform part = pv.GameObject.transform;
                Vector3 center = part.position + part.up * 0.7f;
                int mask = Tools.Common.RaycastMasks.BuildDetectionLayer | (1 << LayerMask.NameToLayer("Interactable"));
                var sockets = UnityEngine.Object.FindObjectsOfType<Ships.Cables.VisualObjects.SocketInteractionObject>().Where(s => s.ShipId == id).ToList();
                log.Msg($"[sockets] {label}: {sockets.Count} sockets");
                foreach (var socket in sockets)
                {
                    Vector3 target = socket.transform.position;
                    Vector3 outward = Vector3.ProjectOnPlane(target - center, part.up).normalized;
                    Vector3 from = target + outward * 1.2f + part.up * 0.25f;
                    bool ok = WorldTracking.Interface.Values.ClientScene.PhysicsScene.Raycast(from, (target - from).normalized, out RaycastHit hit, 3f, mask);
                    bool isSocket = ok && hit.collider.TryGetComponent(out Ships.Cables.VisualObjects.SocketInteractionObject hs) && hs == socket;
                    log.Msg($"[sockets] {label}: {socket.Type} {socket.Socket.SocketId} @ {part.InverseTransformPoint(target)} -> " +
                            (isSocket ? "OK" : ok ? $"BLOCKED by '{PathOf(hit.collider.transform)}'" : "nothing hit"));
                }
            }
            var storageUi = UnityEngine.Object.FindObjectOfType<UI.Inventory.StorageMenuActivator>(true);
            GameObject chestPrefab = storageUi != null ? Traverse.Create(storageUi).Field<GameObject>("smallStorageMenu").Value : null;
            if (chestPrefab != null)
            {
                cam.enabled = true;
                UnityEngine.Object.Destroy(testCam.gameObject);
                GameObject menu = UnityEngine.Object.Instantiate(chestPrefab, storageUi.transform);
                foreach (RectTransform rt in menu.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.GetComponent<UI.Inventory.InventorySlotUI>() != null || rt.parent?.GetComponent<UI.Inventory.InventorySlotUI>() != null) continue;
                    log.Msg($"[chest] rect {PathOf(rt)}: anchors {rt.anchorMin}-{rt.anchorMax} pivot {rt.pivot} pos {rt.anchoredPosition} size {rt.sizeDelta} rect {rt.rect.size}");
                }
                try
                {
                    Patches.BigChestMenuPatch.Expand(menu, 60, 20);
                }
                catch (Exception e)
                {
                    log.Msg($"[chest] menu error: {e.Message}");
                }
                yield return null;
                yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test_chest_menu.png"));
                yield return null;
                var sb = new System.Text.StringBuilder();
                DumpHierarchy(sb, menu.transform, 0);
                File.WriteAllText(Path.Combine(OutDir, "test_chest_menu.txt"), sb.ToString());
                log.Msg($"[chest] menu slots: {menu.GetComponentsInChildren<UI.Inventory.InventorySlotUI>(true).Length}");
                UnityEngine.Object.Destroy(menu);
                testCam = new GameObject("TankTestCam2").AddComponent<Camera>();
            }
            log.Msg($"[tank] large tank state: {big.StatefulParts.First().State}");
            if (reactorShip != null) log.Msg($"[tank] reactor state: {reactorShip.StatefulParts.First().State}");
            Audio.LoopEmitter.ListenerOverride = null;
            cam.enabled = true;
            UnityEngine.Object.Destroy(testCam.gameObject);
        }

        private static IEnumerator RunSeatTest()
        {
            var log = Melon<MiningMod>.Logger;
            PartSettings seat = new Ships.Interface.Services.PartSettingsProvider().GetPartSettingById(29);
            if (seat?.part == null)
            {
                log.Msg("[seat] part 29 missing");
                yield break;
            }
            foreach (var s in seat.part.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true))
            {
                log.Msg($"[seat] prefab socket {s.Id} {s.Type} active={s.gameObject.activeInHierarchy} at {seat.part.transform.InverseTransformPoint(s.transform.position)} " +
                        $"visual={(s.visualObject != null ? PathOf(s.visualObject.transform) + " active=" + s.visualObject.activeSelf : "-")} interactive={(s.interactive != null ? s.interactive.name : "-")}");
            }
            var instancer = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IShipPartInstancer>();
            if (instancer != null)
            {
                var holder = new GameObject("SeatTestPreview");
                GameObject inst = instancer.InstantiatePartPreview(seat, holder.transform);
                foreach (Renderer r in holder.GetComponentsInChildren<Renderer>(true))
                {
                    log.Msg($"[seat] preview renderer {PathOf(r.transform)} enabled={r.enabled} active={r.gameObject.activeInHierarchy} at {holder.transform.InverseTransformPoint(r.bounds.center)}");
                }
                UnityEngine.Object.Destroy(holder);
            }
            var thumbs = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IPartThumbnailProvider>();
            Texture2D large = thumbs?.GetLargeThumbnail(seat.id);
            yield return null;
            yield return null;
            large = thumbs?.GetLargeThumbnail(seat.id) ?? large;
            if (large != null) SaveTexture(large, Path.Combine(OutDir, "test_thumb_seat.png"));
            foreach (ushort id in new ushort[] { Content.HydrogenThruster.PartId, Content.RotorUnlock.RotorPartId, Content.Reactor.PartId, Content.Reactor.LargePartId }.Concat(Content.HydrogenEngines.Variants.Select(v => v.Id)))
            {
                Texture2D thumb = thumbs?.GetLargeThumbnail(id);
                yield return null;
                yield return null;
                thumb = thumbs?.GetLargeThumbnail(id) ?? thumb;
                if (thumb != null) SaveTexture(thumb, Path.Combine(OutDir, $"test_thumb_{id}.png"));
                log.Msg($"[seat] thumbnail {id}: {(thumb != null ? "saved" : "missing")}");
            }
            UI.BuildMenu.BuildMenuActivator activator = Resources.FindObjectsOfTypeAll<UI.BuildMenu.BuildMenuActivator>().FirstOrDefault(m => m.gameObject.scene.IsValid());
            log.Msg($"[seat] build menu activator: {(activator != null ? PathOf(activator.transform) : "missing")}");
            if (activator != null)
            {
                activator.Open();
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test_build_menu.png"));
                UI.BuildMenu.BuildMenu opened = UnityEngine.Object.FindObjectOfType<UI.BuildMenu.BuildMenu>();
                if (opened != null)
                {
                    foreach (RectTransform rt in opened.GetComponentsInChildren<RectTransform>(true).Where(r => r.GetComponentInParent<UI.BuildMenu.BuildPartButton>() == null && r.GetComponentInParent<UI.BuildMenu.TabButton>() == null))
                    {
                        string layout = string.Join(",", rt.GetComponents<Component>().Where(c => c is UnityEngine.UI.LayoutGroup || c is UnityEngine.UI.LayoutElement || c is UnityEngine.UI.ContentSizeFitter || c is UnityEngine.UI.ScrollRect).Select(c => c.GetType().Name));
                        log.Msg($"[menu] {PathOf(rt)}: anchors {rt.anchorMin}-{rt.anchorMax} pivot {rt.pivot} pos {rt.anchoredPosition} size {rt.sizeDelta} rect {rt.rect.size} {layout}");
                    }
                    foreach (UI.BuildMenu.TabButton tab in opened.GetComponentsInChildren<UI.BuildMenu.TabButton>(true).Take(1))
                    {
                        log.Msg($"[menu] tab rect {((RectTransform)tab.transform).rect.size}");
                    }
                    var config = Traverse.Create(opened).Field<UI.BuildMenu.BuildMenuTabConfig[]>("tabsConfig").Value;
                    int propulsion = Array.FindIndex(config, t => t.name == "Power");
                    if (propulsion >= 0)
                    {
                        Traverse.Create(opened).Method("SwitchTab", propulsion).GetValue();
                        yield return null;
                        yield return new WaitForSeconds(0.5f);
                        ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "test_build_menu2.png"));
                        yield return null;
                        yield return null;
                    }
                }
                yield return null;
                yield return null;
                activator.Close();
            }
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator RunDoorsTest()
        {
            var log = Melon<MiningMod>.Logger;
            var preview = UnityEngine.Object.FindObjectOfType<Tools.Build.PartPreview>(true);
            var thumbs = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IPartThumbnailProvider>();
            var select = AccessTools.Method(typeof(Tools.Build.PartPreview), "OnPartSelected");
            var unselect = AccessTools.Method(typeof(Tools.Build.PartPreview), "OnPartUnselected");
            log.Msg($"[doors] preview: {(preview != null ? "present" : "MISSING")}, thumbnails: {(thumbs != null ? "present" : "MISSING")}");
            foreach (PartSettings door in Content.BigDoors.All.Concat(new[] { new Ships.Interface.Services.PartSettingsProvider().GetPartSettingById(Content.BigDoors.DoorPartId) }))
            {
                if (door == null) continue;
                var dynamic = door.part.GetComponentInChildren<Ships.Parts.Door.DoorDynamicColliders>(true);
                GameObject leaf = dynamic != null ? Traverse.Create(dynamic).Field<GameObject>("doorCollider").Value : null;
                log.Msg($"[doors] {door.fullLabel}: dynamic={(dynamic != null ? PathOf(dynamic.transform) : "-")} leaf={(leaf != null ? leaf.name : "-")} " +
                        string.Join(" ", (dynamic != null ? dynamic.GetComponentsInChildren<BoxCollider>(true) : new BoxCollider[0]).Select(b =>
                        {
                            Vector3 c = door.part.transform.InverseTransformPoint(b.transform.TransformPoint(b.center));
                            Vector3 s = Vector3.Scale(b.size, b.transform.lossyScale);
                            return $"[{b.name} c={c} s={s}]";
                        })));
                for (int round = 0; round < 2; round++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        if (preview != null) select.Invoke(preview, new object[] { door.id });
                    }
                    catch (Exception e)
                    {
                        log.Msg($"[doors] {door.fullLabel}: select - error {e.InnerException?.Message ?? e.Message}");
                    }
                    double selectMs = sw.Elapsed.TotalMilliseconds;
                    sw.Restart();
                    Texture2D large = thumbs?.GetLargeThumbnail(door.id);
                    double thumbMs = sw.Elapsed.TotalMilliseconds;
                    int renderers = 0;
                    if (preview != null)
                    {
                        var obj = Traverse.Create(preview).Field<GameObject>("_objectPreview").Value;
                        renderers = obj != null ? obj.GetComponentsInChildren<Renderer>(true).Length : -1;
                    }
                    var instancer = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IShipPartInstancer>();
                    var hologram = Core.Services.ServiceLocator.GetService<Ships.Interface.Services.IShipHologramService>();
                    Type outlineType = Type.GetType("Outline, QuickOutline");
                    double instMs = -1, outlineMs = -1, holoMs = -1;
                    if (instancer != null)
                    {
                        var holder = new GameObject("DoorsTestPreview");
                        sw.Restart();
                        GameObject inst = instancer.InstantiatePartPreview(door, holder.transform);
                        instMs = sw.Elapsed.TotalMilliseconds;
                        renderers = holder.GetComponentsInChildren<Renderer>(true).Length;
                        sw.Restart();
                        if (outlineType != null) holder.AddComponent(outlineType);
                        outlineMs = sw.Elapsed.TotalMilliseconds;
                        sw.Restart();
                        hologram?.ChangeObjectStyleToHolographic(holder, true);
                        hologram?.ChangeObjectStyleToHolographic(holder, false);
                        holoMs = sw.Elapsed.TotalMilliseconds;
                        UnityEngine.Object.Destroy(holder);
                    }
                    log.Msg($"[doors] {door.fullLabel} (round {round + 1}): thumbnail {thumbMs:0.0} ms, visual copy {instMs:0.0} ms, outline {outlineMs:0.0} ms, hologram x2 {holoMs:0.0} ms, renderers {renderers}");
                    if (round == 0 && large != null) SaveTexture(large, Path.Combine(OutDir, $"test_thumb_{door.id}.png"));
                    yield return null;
                    yield return null;
                    try
                    {
                        if (preview != null) unselect.Invoke(preview, new object[] { door.id });
                    }
                    catch (Exception)
                    {
                    }
                    yield return null;
                }
            }
        }

        private static string PathOf(Transform tr)
        {
            string path = tr.name;
            for (int i = 0; i < 4 && tr.parent != null; i++) { tr = tr.parent; path = tr.name + "/" + path; }
            return path;
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

            foreach (string label in new[] { "Storage Chest", "Electrolyzer", "Wood-Fired Generator", "Research Table", "Floor", "Ethanol Distiller", "Fluid Tank", "Large Fluid Tank" })
            {
                PartSettings p = parts.FirstOrDefault(x => x.fullLabel == label);
                if (p?.part == null)
                {
                    continue;
                }
                sb.AppendLine($"== PREFAB {label} ==");
                DumpHierarchy(sb, p.part.transform, 0);
            }

            sb.AppendLine("== HULL ==");
            foreach (Ships.Visuals.ShipHullVisuals hull in UnityEngine.Object.FindObjectsOfType<Ships.Visuals.ShipHullVisuals>().Take(1))
            {
                foreach (Renderer r in hull.GetComponentsInChildren<Renderer>(true).Take(4))
                {
                    sb.AppendLine($"{r.name}: " + string.Join(" | ", r.sharedMaterials.Select(DescribeMaterial)));
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m != null && m.HasProperty("_Albedo") && m.GetTexture("_Albedo") is Texture tex)
                        {
                            SaveTexture(tex, Path.Combine(OutDir, "hull_" + tex.name + ".png"));
                        }
                    }
                }
            }

            sb.AppendLine("== PARTICLES ==");
            foreach (ParticleSystem ps in Resources.FindObjectsOfTypeAll<ParticleSystem>().GroupBy(x => x.name).Select(g => g.First()))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                Material m = r != null ? r.sharedMaterial : null;
                Transform root = ps.transform.root;
                sb.AppendLine($"{ps.name} (root {root.name}) mat={(m != null ? m.name : "-")} shader={(m != null ? m.shader.name : "-")} tex={(m != null && m.mainTexture != null ? m.mainTexture.name : "-")} " +
                              $"color={ps.main.startColor.color} size={ps.main.startSize.constant} lifetime={ps.main.startLifetime.constant}");
            }

            sb.AppendLine("== AUDIO ==");
            sb.AppendLine($"unity audio config: {AudioSettings.GetConfiguration().sampleRate} Hz, speakers {AudioSettings.GetConfiguration().speakerMode}");
            sb.AppendLine($"AudioListeners: {UnityEngine.Object.FindObjectsOfType<AudioListener>().Length}, FMOD listeners: {UnityEngine.Object.FindObjectsOfType<FMODUnity.StudioListener>().Length}");

            sb.AppendLine("== TERRAIN ==");
            foreach (Planets.Terrain.PlanetTerrain terrain in Resources.FindObjectsOfTypeAll<Planets.Terrain.PlanetTerrain>())
            {
                sb.AppendLine($"-- {terrain.transform.root.name}/{terrain.name} lod={terrain.numberOfLOD}");
                sb.AppendLine("config: " + JsonUtility.ToJson(terrain.terrainConfig));
                for (int i = 0; i < (terrain.materials?.Length ?? 0); i++)
                {
                    var m = terrain.materials[i];
                    sb.AppendLine($"material {i}: color={m.albedoColor} tex={(m.albedoTexture != null ? m.albedoTexture.name : "-")} normal={(m.normalMap != null ? m.normalMap.name : "-")} tile={m.tile} step={m.footstepSoundParam}");
                }
                foreach (Component c in terrain.GetComponents<Component>())
                {
                    sb.AppendLine("  component: " + c.GetType().FullName + " " + (c is MonoBehaviour ? JsonUtility.ToJson(c).Substring(0, Math.Min(600, JsonUtility.ToJson(c).Length)) : ""));
                }
                foreach (Renderer r in terrain.transform.root.GetComponentsInChildren<Renderer>(true).Take(3))
                {
                    sb.AppendLine($"  renderer {r.name}: mat={(r.sharedMaterial != null ? r.sharedMaterial.name : "-")} shader={(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-")}");
                }
            }
            foreach (Shader sh in Resources.FindObjectsOfTypeAll<Shader>().Where(x => x.name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0 || x.name.IndexOf("Planet", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                sb.AppendLine("shader: " + sh.name);
            }
            foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>().Where(x => x.shader != null && x.shader.name.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                sb.AppendLine($"terrain material: {mat.name} shader={mat.shader.name} props=[{string.Join(", ", mat.GetTexturePropertyNames())}]");
            }

            sb.AppendLine("== LAYERS ==");
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) sb.AppendLine($"{i}: {n}");
            }

            string path = Path.Combine(OutDir, "dump.txt");
            File.WriteAllText(path, sb.ToString());
            Melon<MiningMod>.Logger.Msg($"[dev] dump saved: {path}");
        }

        private static void SaveTexture(Texture tex, string path)
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
