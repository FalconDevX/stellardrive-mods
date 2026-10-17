using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Services;
using Core.Values;
using MelonLoader;
using Odyssey.Content;
using Odyssey.Patches;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Odyssey.Diagnostics
{
    internal static class Showcase
    {
        private const ushort FloorPartId = 70;
        private const float MaxFootprint = 10f, RowWidth = 19f, Gap = 0.5f, MaxHatch = 4f, LakeSearch = 4000f;

        internal static IEnumerator Run()
        {
            var log = Melon<DevMod>.Logger;
            Camera cam = Camera.main;
            var transpose = ServiceCache.Get<IWorldTransposeClient>();
            if (cam == null || transpose == null || !(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace surface))
            {
                log.Msg("[showcase] no camera/planet");
                yield break;
            }
            var provider = new PartSettingsProvider();
            var floor = provider.GetPartSettingById(FloorPartId) as HullPartSettings;
            List<PartSettings> parts = ModParts.All.Select(p => p.get()).Concat(new[] { RotorUnlock.Rotor })
                .Where(p => p != null && p.Bounds.size.x <= MaxFootprint && p.Bounds.size.z <= MaxFootprint).Distinct().ToList();
            var hatches = parts.Where(p => BigDoors.IsHatch(p) && p.Bounds.size.x <= MaxHatch).OrderBy(p => p.Bounds.size.x).ToList();
            parts.RemoveAll(p => BigDoors.IsHatch(p) && !hatches.Contains(p));
            var doors = parts.Where(p => p is HullPartSettings && !BigDoors.IsHatch(p)).OrderBy(p => p.Bounds.size.x).ToList();
            var floating = parts.Except(hatches).Except(doors).OrderBy(p => Mathf.Round(p.Bounds.size.y * 2f)).ThenByDescending(p => p.Bounds.size.x).ToList();
            log.Msg($"[showcase] {floating.Count} parts, {doors.Count} doors, {hatches.Count} hatches, floor {(floor != null ? "ok" : "MISSING")}");

            var stateful = new List<StatefulPart>();
            ushort nextId = 1;
            float x = 0f, z = 0f, rowDepth = 0f, width = 0f;
            foreach (PartSettings p in floating)
            {
                Bounds b = p.Bounds;
                float w = Mathf.Ceil(b.size.x) + Gap, d = Mathf.Ceil(b.size.z) + Gap;
                if (x > 0f && x + w > RowWidth)
                {
                    z += rowDepth;
                    x = 0f;
                    rowDepth = 0f;
                }
                var center = new Vector3(x - 0.5f + w * 0.5f, 0f, z - 0.5f + d * 0.5f);
                var coord = new Vector3(center.x - b.center.x, -0.46f - b.min.y, center.z - b.center.z);
                stateful.Add(new StatefulPart(nextId++, p, new FloatingPartPlacement(coord, PartOrientation.Forward, PartOrientation.Up)));
                x += w;
                width = Mathf.Max(width, x);
                rowDepth = Mathf.Max(rowDepth, d);
            }
            int maxX = Mathf.CeilToInt(width), maxZ = Mathf.CeilToInt(z + rowDepth);

            int doorX = 0;
            foreach (PartSettings door in doors)
            {
                int w = Mathf.RoundToInt(door.Bounds.size.x);
                if (doorX + w > maxX) break;
                stateful.Add(new StatefulPart(nextId++, door, new HullPartPlacement(new Vector3Int(doorX, 0, maxZ), VoxelPartType.ForwardWall, PartOrientation.Forward, PartOrientation.Up)));
                doorX += w + 1;
            }

            var holes = new HashSet<Vector3Int>();
            int hatchX = 0, minZ = 0;
            foreach (PartSettings hatch in hatches)
            {
                int n = Mathf.RoundToInt(Mathf.Max(hatch.Bounds.size.x, hatch.Bounds.size.z));
                if (hatchX + n > maxX) break;
                int hz = -2 - n;
                minZ = Mathf.Min(minZ, hz - 1);
                var placement = new HullPartPlacement(new Vector3Int(hatchX, 0, hz), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Right);
                foreach (Vector3Int c in placement.CalculateAllCoords(hatch.size)) holes.Add(c);
                stateful.Add(new StatefulPart(nextId++, hatch, placement));
                hatchX += n + 1;
            }

            var hull = new List<HullPart>();
            for (int vx = 0; vx <= maxX; vx++)
            {
                for (int vz = minZ; vz <= maxZ; vz++)
                {
                    if (holes.Contains(new Vector3Int(vx, 0, vz))) continue;
                    hull.Add(new HullPart(floor, new HullPartPlacement(new Vector3Int(vx, 0, vz), VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Right), HullPartColor.Default));
                }
            }

            var planetSpace = new PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Vector3Double.Zero };
            Vector3 camPlanet = (Vector3)transpose.Position(surface, planetSpace, (Vector3Double)cam.transform.position);
            Vector3 up = camPlanet.normalized;
            Vector3 look = Vector3.ProjectOnPlane(transpose.Rotation(surface, planetSpace, cam.transform.rotation) * Vector3.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, look);
            var centerLocal = new Vector3(maxX * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            float reach = Mathf.Max(maxX, maxZ - minZ) * 0.75f + 4f;
            Vector3 lake = Vector3.zero;
            float best = float.MaxValue;
            for (float dx = -LakeSearch; dx <= LakeSearch; dx += 50f)
            {
                for (float dy = -LakeSearch; dy <= LakeSearch; dy += 50f)
                {
                    if (dx * dx + dy * dy >= best) continue;
                    Vector3 c = (camPlanet + right * dx + look * dy).normalized * camPlanet.magnitude;
                    bool water = true;
                    for (int k = 0; k < 9 && water; k++)
                    {
                        Vector3 probe = (c + right * reach * (k % 3 - 1) + look * reach * (k / 3 - 1)).normalized * c.magnitude;
                        water = WaterPump.TryProbe(surface.PlanetId, probe, out _, out bool w) && w;
                    }
                    if (water)
                    {
                        best = dx * dx + dy * dy;
                        lake = c.normalized;
                    }
                }
            }
            if (best == float.MaxValue)
            {
                log.Msg("[showcase] no lake big enough nearby");
                yield break;
            }
            var tracked = Worlds.FindTrackedPlanet(surface.PlanetId);
            Vector3 originDir = (lake * camPlanet.magnitude - right * centerLocal.x - look * centerLocal.z).normalized;
            float ground = tracked.Planet.Radius + Worlds.SeaLevel + 0.5f;
            log.Msg($"[showcase] lake {Mathf.Sqrt(best):0} m from the camera");
            Vector3 originPlanet = originDir * (ground + 0.7f);
            var physicsSpace = ClientOrigin.LocalPhysicsSpace;
            var position = (Vector3)transpose.Position(planetSpace, physicsSpace, (Vector3Double)originPlanet);
            Quaternion rotation = transpose.Rotation(planetSpace, physicsSpace, Quaternion.LookRotation(look, up));

            var ships = ServiceCache.Get<ShipsServerTracker>();
            var spaces = ServiceCache.Get<WorldTracking.Interface.Services.ISpacesServerProvider>();
            var worldIds = ServiceCache.Get<WorldTracking.Interface.Services.IWorldObjectsIdsServerProvider>();
            uint shipId = worldIds.CreateUniqueIdForObject();
            var snapshot = new ShipStateSnapshot
            {
                Id = shipId,
                RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(shipId),
                SpaceId = ClientOrigin.LocalPhysicsSpaceId,
                Position = position,
                Rotation = rotation,
                Velocity = Vector3.zero,
                RoomGasState = System.Array.Empty<Ships.Interface.Model.Gases.RoomGasState>(),
                HullParts = hull.ToArray(),
                StatefulParts = stateful.ToArray(),
            };
            ships.LoadShip(snapshot);
            TrackedShipServer ship = ships.GetShip(shipId);
            log.Msg($"[showcase] ship {shipId}: {hull.Count} floor tiles, {stateful.Count} parts, platform {maxX + 1} x {maxZ - minZ + 1} m, loaded {(ship != null ? "yes" : "NO")}");
            if (ship == null)
            {
                yield break;
            }
            var hold = ship.PhysicsState;
            var shipsClient = ServiceCache.Get<IShipsClientProvider>();
            var shotCam = new GameObject("ShowcaseCam").AddComponent<Camera>();
            shotCam.CopyFrom(cam);
            shotCam.fieldOfView = 50f;
            var data = shotCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToList();
            System.Reflection.FieldInfo waterCamera = HarmonyLib.AccessTools.Field(HarmonyLib.AccessTools.TypeByName("Water.Surface.WaterSurface"), "OverrideCamera");
            waterCamera?.SetValue(null, shotCam);

            Vector3 extent = new Vector3(maxX + 1, 0f, maxZ - minZ + 1);
            float size = Mathf.Max(extent.x, extent.z);
            Vector3[] views =
            {
                new Vector3(-0.55f, 0.36f, -0.78f),
                new Vector3(0.5f, 0.3f, -0.8f),
                new Vector3(-0.1f, 0.5f, -0.85f),
                Vector3.zero,
            };
            MelonPreferences_Entry<bool> fps = MelonPreferences.GetEntry<bool>("FpsCounter", "ShowFps");
            bool fpsShown = fps != null && fps.Value;
            if (fps != null) fps.Value = false;
            for (int shot = 0; shot < views.Length; shot++)
            {
                for (int frame = 0; frame < (shot == 0 ? 900 : 120); frame++)
                {
                    ship.SetPhysicsState(new ShipPhysicsState(hold.SpaceId, hold.Position, hold.Rotation, Vector3.zero));
                    if (shipsClient.TryGetShipVisualTransform(shipId, out Transform visual))
                    {
                        Vector3 target = visual.TransformPoint(centerLocal + new Vector3(0f, 0.6f, 0f));
                        Vector3 offset = visual.TransformDirection(views[shot]) * size * 0.85f;
                        if (views[shot] == Vector3.zero)
                        {
                            target = visual.TransformPoint(new Vector3(centerLocal.x, -1.5f, minZ - 18f));
                            offset = visual.TransformPoint(new Vector3(centerLocal.x - 4f, 2.5f, minZ - 1f)) - target;
                        }
                        shotCam.transform.position = target + offset;
                        shotCam.transform.rotation = Quaternion.LookRotation(target - shotCam.transform.position, visual.up);
                        cam.enabled = false;
                        foreach (Canvas c in hidden) if (c != null) c.enabled = false;
                    }
                    yield return null;
                }
                string path = Path.Combine(DevTools.OutDir, $"showcase{shot}.png");
                ScreenCapture.CaptureScreenshot(path, 2);
                yield return null;
                yield return null;
                log.Msg($"[showcase] shot {shot}: {path}");
            }
            foreach (Canvas c in hidden) if (c != null) c.enabled = true;
            if (fps != null) fps.Value = fpsShown;
            waterCamera?.SetValue(null, null);
            cam.enabled = true;
            Object.Destroy(shotCam.gameObject);
        }
    }
}
