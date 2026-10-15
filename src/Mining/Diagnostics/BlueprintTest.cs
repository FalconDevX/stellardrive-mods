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
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            log.Msg($"[bp] item: {(BlueprintToolItem.Item != null ? "present" : "MISSING")}, tool prefab: {(BlueprintToolItem.Item?.toolSettings?.prefab?.GetComponent<BlueprintTool>() != null ? "ok" : "MISSING")}, workbench: {(Workbench.Part != null ? "present" : "MISSING")}");
            var player = ServerInventory.LocalPlayer();
            log.Msg($"[bp] host: {ServerInventory.IsHost}, player: {(player != null ? "present" : "MISSING")}");
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

            Vector3 up = ServiceLocator.GetService<Players.Interface.Services.IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
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
            int newCables = created != null ? ServiceLocator.GetService<Ships.Cables.CablesServerTracker>().GetShipCables(created.Id).Count : -1;
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

            BlueprintCatalog.Toggle();
            for (int i = 0; i < 5; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_catalog.png"));
            yield return null;
            yield return null;
            BlueprintCatalog.Toggle();
            WorkbenchMenu.Open();
            for (int i = 0; i < 5; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(DevTools.OutDir, "test_bp_workbench.png"));
            yield return null;
            yield return null;
            WorkbenchMenu.Hide();
            BlueprintStore.Delete(loaded ?? entry);
            log.Msg("[bp] done");
        }
    }
}
