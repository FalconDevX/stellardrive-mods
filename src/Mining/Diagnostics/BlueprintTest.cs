using System.Collections;
using System.IO;
using System.Linq;
using Core.Services;
using Items.Model;
using MelonLoader;
using Mining.Blueprints;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Mining.Diagnostics
{
    internal static class BlueprintTest
    {
        internal static IEnumerator Run()
        {
            var log = Melon<MiningMod>.Logger;
            Camera cam = Camera.main;
            var ships = ServiceCache.Get<ShipsServerTracker>();
            var transpose = ServiceCache.Get<IWorldTransposeClient>();
            log.Msg($"[bp] item: {(BlueprintToolItem.Item != null ? "present" : "MISSING")}, tool prefab: {(BlueprintToolItem.Item?.toolSettings?.prefab?.GetComponent<BlueprintTool>() != null ? "ok" : "MISSING")}, workbench: {(Workbench.Part != null ? "present" : "MISSING")}");
            var player = ServerInventory.LocalPlayer();
            log.Msg($"[bp] host: {ServerInventory.IsHost}, player: {(player != null ? "present" : "MISSING")}, tool shader: {BlueprintToolModel.Shader}");
            var dump = new System.Text.StringBuilder();
            foreach (ushort id in new ushort[] { 23, Content.BigFluidTank.LargeId })
            {
                Ships.Interface.Settings.PartSettings tank = new Ships.Interface.Services.PartSettingsProvider().GetPartSettingById(id);
                dump.AppendLine($"== {tank.fullLabel}");
                DevTools.DumpHierarchy(dump, tank.part.transform, 0);
                foreach (var f in tank.part.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null) continue;
                    Bounds b = f.sharedMesh.bounds;
                    dump.AppendLine($"MESH {f.name} verts {f.sharedMesh.vertexCount} local bounds {b.center} {b.size} world center {tank.part.transform.InverseTransformPoint(f.transform.TransformPoint(b.center))} lossy {f.transform.lossyScale}");
                }
                foreach (var so in tank.part.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true))
                {
                    dump.AppendLine($"SOCKET {so.Id} {so.Type} at {tank.part.transform.InverseTransformPoint(so.transform.position)} rot {so.transform.rotation.eulerAngles} scale {so.transform.lossyScale}");
                }
            }
            File.WriteAllText(Path.Combine(DevTools.OutDir, "test_tank_dump.txt"), dump.ToString());
            var thumbs = ServiceCache.Get<Items.Interface.Service.IItemThumbnailProvider>();
            Texture2D thumb = thumbs?.GetThumbnail(BlueprintToolItem.ItemId);
            yield return null;
            thumb = thumbs?.GetThumbnail(BlueprintToolItem.ItemId) ?? thumb;
            if (thumb != null) DevTools.SaveTexture(thumb, Path.Combine(DevTools.OutDir, "test_bp_tool_icon.png"));
            Texture2D paintThumb = thumbs?.GetThumbnail(BlueprintToolItem.PaintToolItemId);
            yield return null;
            if (paintThumb != null) DevTools.SaveTexture(paintThumb, Path.Combine(DevTools.OutDir, "test_bp_paint_icon.png"));
            if (cam == null || ships == null || player == null)
            {
                yield break;
            }

            player.AddItemToInventory(new ItemDrop { ItemId = ItemsConstants.IronItemId, Quantity = 100 });
            player.AddItemToInventory(new ItemDrop { ItemId = 112, Quantity = 100 });
            player.AddItemToInventory(new ItemDrop { ItemId = ItemsConstants.GlassItemId, Quantity = 50 });
            yield return null;
            int ironBefore = ServerInventory.Count(player, ItemsConstants.IronItemId);
            bool crafted = Workbench.Craft(BlueprintToolItem.ItemId, out string craftMsg);
            yield return null;
            log.Msg($"[bp] craft: {crafted} '{craftMsg}', tools now {ServerInventory.Count(player, BlueprintToolItem.ItemId)}, iron {ironBefore} -> {ServerInventory.Count(player, ItemsConstants.IronItemId)}");

            TrackedShipServer source = ships.Ships.OfType<TrackedShipServer>().Where(s => s.GetHullParts().Count >= 8 && s.GetHullParts().Count <= 400)
                .OrderByDescending(s => s.StatefulParts.Count()).FirstOrDefault();
            if (source == null)
            {
                log.Msg("[bp] no source ship with hull parts");
                yield break;
            }
            var coords = source.GetHullParts().Keys.Select(k => k.VoxelCoord).ToList();
            Vector3Int min = coords.Aggregate(Vector3Int.Min), max = coords.Aggregate(Vector3Int.Max);
            BlueprintData data = BlueprintServer.Capture(source.Id, min, max, out string error);
            log.Msg($"[bp] capture ship {source.Id} {min}..{max}: {(data != null ? $"hull {data.hull.Count}/{source.GetHullParts().Count}, parts {data.parts.Count}/{source.StatefulParts.Count()}, cables {data.cables.Count}" : error)}");
            if (data == null)
            {
                yield break;
            }
            data.name = "zz_test_blueprint";
            BlueprintEntry entry = BlueprintStore.Save(data);
            BlueprintStore.Reload();
            BlueprintEntry loaded = BlueprintStore.Entries.FirstOrDefault(e => e.Path == entry.Path);
            log.Msg($"[bp] saved {Path.GetFileName(entry.Path)}, reloaded: {(loaded != null ? $"hull {loaded.Data.hull.Count}, parts {loaded.Data.parts.Count}, cables {loaded.Data.cables.Count}" : "MISSING")}");
            var cost = BlueprintServer.TotalCost(data);
            log.Msg($"[bp] cost: {string.Join(", ", cost.Select(kv => $"{kv.Key}x{kv.Value}"))}");
            foreach (var kv in cost)
            {
                for (int left = kv.Value * 2 + 10; left > 0; left -= 100)
                {
                    player.AddItemToInventory(new ItemDrop { ItemId = kv.Key, Quantity = (byte)Mathf.Min(left, 100) });
                }
            }
            yield return null;

            Vector3 up = ServiceCache.Get<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
            Vector3 visualPos = cam.transform.position + fwd * 30f + up * 6f;
            Vector3 physicsPos = transpose.Position(ClientOrigin.LocalVisualSpaceId, ClientOrigin.LocalPhysicsSpaceId, visualPos);
            Quaternion physicsRot = transpose.Rotation(ClientOrigin.LocalVisualSpaceId, ClientOrigin.LocalPhysicsSpaceId, Quaternion.LookRotation(fwd, up));
            var before = ships.Ships.Select(s => s.Id).ToList();
            var newTarget = new BlueprintTarget
            {
                NewShip = true,
                Transform = BlueprintTransform.Centered(data, 0, Vector3Int.zero),
                World = new WorldTransform { SpaceId = ClientOrigin.LocalPhysicsSpaceId, Position = physicsPos, Rotation = physicsRot },
            };
            bool built = BlueprintServer.Build(data, newTarget, out string msg);
            yield return null;
            TrackedShipServer created = ships.Ships.OfType<TrackedShipServer>().FirstOrDefault(s => !before.Contains(s.Id));
            int newCables = created != null ? ServiceCache.Get<Ships.Cables.CablesServerTracker>().GetShipCables(created.Id).Count : -1;
            log.Msg($"[bp] new ship: {built} '{msg}', ship {(created != null ? $"{created.Id}: hull {created.GetHullParts().Count}, parts {created.StatefulParts.Count()}, cables {newCables}" : "MISSING")}");

            int hullBefore = source.GetHullParts().Count, partsBefore = source.StatefulParts.Count();
            var rotated = new BlueprintTransform(1, new Vector3Int(0, max.y - min.y + 3, 0) + min);
            bool builtOn = BlueprintServer.Build(data, new BlueprintTarget { ShipId = source.Id, Transform = rotated }, out string msg2);
            yield return null;
            int expected = data.hull.Count(h => source.GetHullParts().ContainsKey(rotated.Apply(new Vector3Int(h.x, h.y, h.z), (VoxelPartType)h.t)));
            log.Msg($"[bp] on ship (rotated): {builtOn} '{msg2}', hull {hullBefore} -> {source.GetHullParts().Count}, parts {partsBefore} -> {source.StatefulParts.Count()}, rotated hull positions found {expected}/{data.hull.Count}");

            BlueprintGhosts.Add(data, new BlueprintTarget
            {
                NewShip = true,
                Transform = BlueprintTransform.Centered(data, 0, Vector3Int.zero),
                World = new WorldTransform { SpaceId = ClientOrigin.LocalPhysicsSpaceId, Position = transpose.Position(ClientOrigin.LocalVisualSpaceId, ClientOrigin.LocalPhysicsSpaceId, cam.transform.position - Vector3.Cross(up, fwd) * 25f + fwd * 15f), Rotation = physicsRot },
            });
            for (int i = 0; i < 60; i++) yield return null;
            BlueprintGhost ghost = BlueprintGhosts.All.LastOrDefault();
            if (ghost?.Body != null)
            {
                cam.transform.rotation = Quaternion.LookRotation(ghost.Body.position - cam.transform.position, up);
            }
            for (int i = 0; i < 10; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_ghost.png"));
            yield return null;
            Camera.main.transform.rotation = Quaternion.LookRotation(fwd, up);
            bool aimed = ghost != null && BlueprintGhosts.Aimed(new Ray(cam.transform.position, (ghost.Body.position - cam.transform.position).normalized), 200f) == ghost;
            log.Msg($"[bp] ghost visible: {(ghost?.Body != null && ghost.Body.gameObject.activeInHierarchy)}, aim test: {aimed}, placed: {BlueprintGhosts.All.Count}");
            if (ghost != null) BlueprintGhosts.Remove(ghost);

            GameObject toolView = Object.Instantiate(BlueprintToolItem.Item.toolSettings.prefab);
            toolView.SetActive(true);
            foreach (var b in toolView.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            Vector3 viewUp = cam.transform.up;
            toolView.transform.position = cam.transform.position + cam.transform.forward * 0.8f;
            toolView.transform.rotation = Quaternion.LookRotation(cam.transform.right, viewUp);
            for (int i = 0; i < 5; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_tool_side.png"));
            yield return null;
            toolView.transform.rotation = Quaternion.LookRotation(cam.transform.forward, viewUp);
            toolView.transform.position = cam.transform.position + cam.transform.forward * 0.6f + cam.transform.right * 0.15f - viewUp * 0.1f;
            for (int i = 0; i < 3; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_tool_back.png"));
            yield return null;
            Object.Destroy(toolView);
            if (Workbench.Part != null)
            {
                GameObject bench = Object.Instantiate(Workbench.Part.part);
                bench.SetActive(true);
                Vector3 benchUp = ServiceCache.Get<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
                Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, benchUp).normalized;
                bench.transform.position = cam.transform.position + flat * 3.2f - benchUp * 1.6f;
                bench.transform.rotation = Quaternion.LookRotation(flat, benchUp);
                cam.transform.rotation = Quaternion.LookRotation(bench.transform.position + benchUp * 0.7f - cam.transform.position, benchUp);
                var barriers = new System.Collections.Generic.List<GameObject>();
                if (Content.Barrier.Part != null)
                {
                    Vector3 benchSide = Vector3.Cross(benchUp, flat);
                    for (int i = 0; i < 3; i++)
                    {
                        GameObject barrier = Object.Instantiate(Content.Barrier.Part.part);
                        barrier.SetActive(true);
                        barrier.transform.rotation = Quaternion.LookRotation(flat, benchUp);
                        barrier.transform.position = bench.transform.position + benchSide * (1.6f + i) - flat * 0.3f;
                        barriers.Add(barrier);
                    }
                }
                for (int i = 0; i < 20; i++) yield return null;
                int hiddenPosts = barriers.Count(g => g.transform.Find(Content.BarrierVisuals.LeftPost) is Transform t1 && !t1.gameObject.activeSelf
                    || g.GetComponentsInChildren<Transform>(true).Any(t => t.name == Content.BarrierVisuals.LeftPost && !t.gameObject.activeSelf));
                log.Msg($"[bp] barrier: {(Content.Barrier.Part != null ? "present" : "MISSING")}, hidden left posts {hiddenPosts}/{barriers.Count}");
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_bench.png"));
                yield return null;
                Object.Destroy(bench);
                foreach (GameObject barrier in barriers) Object.Destroy(barrier);
                cam.transform.rotation = Quaternion.LookRotation(fwd, up);
            }
            Ships.Interface.Settings.PartSettings bigTank = Content.BigFluidTank.Get(Content.BigFluidTank.LargeId);
            if (bigTank != null)
            {
                GameObject tankView = Object.Instantiate(bigTank.part);
                tankView.SetActive(true);
                Vector3 tankUp = ServiceCache.Get<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
                Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, tankUp).normalized;
                Vector3 side = Vector3.Cross(tankUp, flat);
                tankView.transform.rotation = Quaternion.LookRotation(flat, tankUp);
                tankView.transform.position = cam.transform.position + flat * 6f - tankUp * 1.6f - side * 1.5f;
                yield return null;
                foreach (var r in tankView.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds wb = r.bounds;
                    log.Msg($"[bp] tank renderer {r.name} enabled {r.enabled} local min {tankView.transform.InverseTransformPoint(wb.min)} max {tankView.transform.InverseTransformPoint(wb.max)}");
                }
                foreach (var so in tankView.GetComponentsInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true))
                {
                    log.Msg($"[bp] tank socket {so.Id} at {tankView.transform.InverseTransformPoint(so.transform.position)}");
                }
                Vector3 frontCenter = tankView.transform.TransformPoint(new Vector3(1.5f, 0.3f, -0.6f));
                Vector3 savedPos = cam.transform.position;
                for (int k = 0; k < 2; k++)
                {
                    Vector3 target = tankView.transform.TransformPoint(new Vector3(k == 0 ? 0f : 3f, 0.3f, -0.5f));
                    cam.transform.position = target - flat * 1.6f + tankUp * 0.7f + side * (k == 0 ? -0.6f : 0.6f);
                    cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, tankUp);
                    for (int i = 0; i < 4; i++) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, $"test_bp_tank{k}.png"));
                    yield return null;
                }
                cam.transform.position = frontCenter - flat * 4.5f + tankUp * 1.2f;
                cam.transform.rotation = Quaternion.LookRotation(frontCenter - cam.transform.position, tankUp);
                for (int i = 0; i < 4; i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_tank_front.png"));
                yield return null;
                cam.transform.position = savedPos;
                cam.transform.rotation = Quaternion.LookRotation(fwd, up);
                Object.Destroy(tankView);
            }
            BlueprintCatalog.Toggle();
            for (int i = 0; i < 5; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_catalog.png"));
            yield return null;
            yield return null;
            BlueprintCatalog.Toggle();
            var activator = Object.FindAnyObjectByType<UI.Research.GenericPartOpenableMenuActivator>(FindObjectsInactive.Include);
            GameObject prefab = activator != null ? HarmonyLib.Traverse.Create(activator).Field<GameObject>("researchMenuPrefab").Value : null;
            if (prefab != null)
            {
                WorkbenchUi.Pending = true;
                GameObject menu = Object.Instantiate(prefab, activator.transform);
                WorkbenchUi.Pending = false;
                var canvas = menu.GetComponentInChildren<UI.Research.ResearchCanvas>(true);
                for (int i = 0; i < 5; i++) yield return null;
                canvas.SelectResearch(WorkbenchUi.Recipes[0]);
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_workbench.png"));
                yield return null;
                yield return null;
                int buildTools = ServerInventory.Count(player, 0);
                canvas.UnlockSelectedResearch();
                yield return null;
                log.Msg($"[bp] workbench ui: buttons {menu.GetComponentsInChildren<UI.Research.ResearchSelectButton>(true).Length}, craft build tool {buildTools} -> {ServerInventory.Count(player, 0)}");
                Object.Destroy(menu);
            }
            else
            {
                log.Msg("[bp] workbench ui: research menu prefab MISSING");
            }
            var net = HarmonyLib.Traverse.Create(player).Property("NetworkState").GetValue<Players.Network.PlayerNetworkState>();
            int toolSlot = -1;
            for (int i = 0; i < net.Toolbar.Count; i++)
            {
                if (net.Toolbar[i].HasItem && net.Toolbar[i].ItemId == BlueprintToolItem.ItemId) toolSlot = i;
            }
            if (toolSlot < 0)
            {
                net.ServerSetToolbarSlot(4, new Items.Model.InventorySlot { HasItem = true, ItemId = BlueprintToolItem.ItemId, Quantity = 1 });
                toolSlot = 4;
            }
            var belt = Object.FindAnyObjectByType<Players.Inventory.ToolBelt>();
            yield return null;
            belt?.SelectTool(toolSlot + 1);
            for (int i = 0; i < 60; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_tool_fp.png"));
            yield return null;
            belt?.ClearSelection();
            for (int i = 0; i < 20; i++) yield return null;
            var shipsClient = ServiceCache.Get<ShipsClientTracker>();
            var engineShips = new System.Collections.Generic.List<(string label, uint id)>();
            int slot = 0;
            foreach (var variant in Content.HydrogenEngines.Variants)
            {
                Ships.Interface.Settings.PartSettings engine = Content.HydrogenEngines.Get(variant.Id);
                if (engine == null) continue;
                Vector3 pos = cam.transform.position + fwd * 14f + Vector3.Cross(up, fwd) * (slot++ * 6f - 6f) + up * 4f;
                uint id = Patches.ServerCreateShipPatch.CreateExcavatorShip(engine, new WorldTransform
                {
                    SpaceId = ClientOrigin.LocalPhysicsSpaceId,
                    Position = transpose.Position(ClientOrigin.LocalVisualSpaceId, ClientOrigin.LocalPhysicsSpaceId, pos),
                    Rotation = transpose.Rotation(ClientOrigin.LocalVisualSpaceId, ClientOrigin.LocalPhysicsSpaceId, Quaternion.LookRotation(fwd, up)),
                });
                engineShips.Add((variant.Label, id));
            }
            cam.transform.rotation = Quaternion.LookRotation(fwd, up);
            for (int frame = 0; frame < 120; frame++)
            {
                foreach (var (_, id) in engineShips)
                {
                    TrackedShipServer ship = ships.GetShip(id);
                    if (ship == null) continue;
                    var part = ship.StatefulParts.First();
                    ship.UpdatePartState(part.Id, (Ships.Interface.Model.Parts.State.ThrusterState)part.State with { ThrusterStrength = 1f });
                    ship.SetPhysicsState(new Ships.Interface.Model.State.ShipPhysicsState(ship.PhysicsState.SpaceId, ship.PhysicsState.Position, ship.PhysicsState.Rotation, Vector3.zero));
                    if (shipsClient.TryGetTrackedShip(id, out TrackedShipClient visualShip) && visualShip.IsVisualLoaded)
                    {
                        foreach (var vfx in visualShip.VisualObject.GetComponentsInChildren<Ships.Parts.Thruster.ThrusterFlameVFX>(true)) vfx.SetIntensity(1f);
                    }
                }
                yield return null;
            }
            foreach (var (label, id) in engineShips)
            {
                int systems = 0, emitting = 0;
                if (shipsClient.TryGetTrackedShip(id, out TrackedShipClient tracked) && tracked.IsVisualLoaded)
                {
                    foreach (var ps in tracked.VisualObject.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (ps.GetComponentInParent<Content.FlameMirror>() == null && ps.GetComponentInParent<Ships.Parts.Thruster.ThrusterFlameVFX>() == null) continue;
                        systems++;
                        if (ps.particleCount > 0) emitting++;
                    }
                }
                log.Msg($"[bp] engine {label}: flame systems {systems}, emitting {emitting}");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_engines.png"));
            yield return null;
            var paint = ServiceCache.Get<UI.Interface.Services.IPaintMenuActivator>();
            if (paint != null)
            {
                paint.Open();
                for (int i = 0; i < 5; i++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_paint.png"));
                yield return null;
                yield return null;
                paint.Close();
            }
            BlueprintStore.Delete(loaded ?? entry);
            log.Msg("[bp] done");
        }
    }
}
