using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Players;
using Players.Inventory;
using Players.Services;
using Ships;
using Ships.Cables;
using Ships.Interface.Model;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Network.Service;
using Ships.Services;
using Core.Values;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;

namespace Mining.Blueprints
{
    internal static class ServerInventory
    {
        internal static bool IsHost => FishNet.InstanceFinder.ServerManager != null && FishNet.InstanceFinder.ServerManager.Started;

        internal static TrackedPlayerServer LocalPlayer()
        {
            var conn = FishNet.InstanceFinder.ClientManager?.Connection;
            if (conn == null || !IsHost)
            {
                return null;
            }
            return ServiceLocator.GetService<PlayersServerTracker>()?.GetTrackedPlayerFromConnectionId(conn.ClientId);
        }

        internal static int Count(TrackedPlayerServer player, uint itemId)
        {
            var reader = Traverse.Create(player).Field("_networkInventoryReader").GetValue<PlayerNetworkInventoryReader>();
            return reader != null ? reader.GetItemQuantity(itemId) : 0;
        }

        internal static List<(uint item, int need, int have)> Missing(TrackedPlayerServer player, Dictionary<uint, int> cost)
        {
            return cost.Select(kv => (kv.Key, kv.Value, Count(player, kv.Key))).Where(x => x.Item3 < x.Item2).ToList();
        }

        internal static void Remove(TrackedPlayerServer player, Dictionary<uint, int> cost)
        {
            foreach (var kv in cost)
            {
                for (int left = kv.Value; left > 0; left -= 255)
                {
                    player.RemoveItemsFromInventory(new[] { new ItemDrop { ItemId = kv.Key, Quantity = (byte)Mathf.Min(left, 255) } });
                }
            }
        }

        internal static void Add(Dictionary<uint, int> cost, IEnumerable<ItemDrop> drops, int times = 1)
        {
            foreach (ItemDrop d in drops)
            {
                cost.TryGetValue(d.ItemId, out int old);
                cost[d.ItemId] = old + d.Quantity * times;
            }
        }

        internal static string ItemName(uint id)
        {
            ItemSettings item = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i != null && i.id == id);
            return item != null ? item.itemName : $"item {id}";
        }

        internal static string Describe(IEnumerable<(uint item, int need, int have)> missing) =>
            string.Join(", ", missing.Select(m => $"{ItemName(m.item)} {m.have}/{m.need}"));
    }

    internal sealed class BlueprintTarget
    {
        public bool NewShip;
        public uint ShipId;
        public BlueprintTransform Transform;
        public WorldTransform World;
    }

    internal static class BlueprintServer
    {
        private static PartSettings Settings(int id)
        {
            try
            {
                return new PartSettingsProvider().GetPartSettingById((ushort)id);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        internal static BlueprintData Capture(uint shipId, Vector3Int a, Vector3Int b, out string error)
        {
            error = null;
            TrackedShipServer ship = ServiceLocator.GetService<ShipsServerTracker>()?.GetShip(shipId);
            if (ship == null)
            {
                error = "Ship not found";
                return null;
            }
            Vector3Int min = Vector3Int.Min(a, b), max = Vector3Int.Max(a, b);
            bool Inside(Vector3Int v) => v.x >= min.x && v.y >= min.y && v.z >= min.z && v.x <= max.x && v.y <= max.y && v.z <= max.z;
            var data = new BlueprintData { sizeX = max.x - min.x + 1, sizeY = max.y - min.y + 1, sizeZ = max.z - min.z + 1 };
            Vector3 faceLo = (Vector3)min - Vector3.one * 0.501f, faceHi = (Vector3)max + Vector3.one * 0.501f;
            foreach (var kv in ship.GetHullParts())
            {
                Vector3 face = PartPositionHelper.GetHullPartInteriorPos(kv.Key);
                if (face.x < faceLo.x || face.y < faceLo.y || face.z < faceLo.z || face.x > faceHi.x || face.y > faceHi.y || face.z > faceHi.z) continue;
                Vector3Int v = kv.Key.VoxelCoord - min;
                HullPartColor c = kv.Value.Color;
                data.hull.Add(new BlueprintHull
                {
                    s = kv.Value.PartId, x = v.x, y = v.y, z = v.z, t = (int)kv.Key.Type,
                    f = (int)kv.Value.ForwardOrientation, u = (int)kv.Value.UpOrientation,
                    fr = c.Front.x, fg = c.Front.y, fb = c.Front.z, br = c.Back.x, bg = c.Back.y, bb = c.Back.z,
                });
            }
            var ids = new HashSet<ushort>();
            Vector3 lo = (Vector3)min - Vector3.one * 0.5f, hi = (Vector3)max + Vector3.one * 0.5f;
            foreach (StatefulPart part in ship.StatefulParts)
            {
                if (part.Placement is HullPartPlacement hp)
                {
                    if (!Inside(hp.Coord)) continue;
                    Vector3Int v = hp.Coord - min;
                    data.parts.Add(new BlueprintPart
                    {
                        id = part.Id, s = part.Settings.id, onHull = true, hx = v.x, hy = v.y, hz = v.z, ht = (int)hp.Position.Type,
                        f = (int)hp.ForwardOrientation, u = (int)hp.UpOrientation,
                    });
                }
                else if (part.Placement is FloatingPartPlacement fp)
                {
                    Vector3 p = fp.Coord;
                    if (p.x < lo.x || p.y < lo.y || p.z < lo.z || p.x > hi.x || p.y > hi.y || p.z > hi.z) continue;
                    Vector3 r = p - min;
                    data.parts.Add(new BlueprintPart
                    {
                        id = part.Id, s = part.Settings.id, x = r.x, y = r.y, z = r.z,
                        f = (int)fp.ForwardOrientation, u = (int)fp.UpOrientation,
                    });
                }
                else
                {
                    continue;
                }
                ids.Add(part.Id);
            }
            var cables = ServiceLocator.GetService<CablesServerTracker>();
            if (cables != null)
            {
                foreach (CableData cable in cables.GetShipCables(shipId))
                {
                    CablePlugSocket pa = cable.State.PlugA, pb = cable.State.PlugB;
                    if (pa.IsHeldByPlayer || pb.IsHeldByPlayer || pa.EntityId != shipId || pb.EntityId != shipId) continue;
                    if (!ids.Contains(pa.PartSocket.PartId) || !ids.Contains(pb.PartSocket.PartId)) continue;
                    data.cables.Add(new BlueprintCable
                    {
                        type = (int)cable.State.Type, a = pa.PartSocket.PartId, sa = pa.PartSocket.SocketId, b = pb.PartSocket.PartId, sb = pb.PartSocket.SocketId,
                    });
                }
            }
            if (data.PartCount == 0)
            {
                error = "Nothing to save in the selected area";
                return null;
            }
            return data;
        }

        private sealed class Plan
        {
            public readonly List<HullPart> Hull = new List<HullPart>();
            public readonly List<(int oldId, PartSettings settings, IPartPlacement placement)> Stateful = new List<(int, PartSettings, IPartPlacement)>();
            public readonly Dictionary<uint, int> Cost = new Dictionary<uint, int>();
            public int Skipped;
        }

        private static Plan MakePlan(BlueprintData data, BlueprintTarget target, TrackedShipServer ship, out string error)
        {
            error = null;
            var plan = new Plan();
            BlueprintTransform t = target.Transform;
            var existingHull = ship != null ? new HashSet<HullPartPosition>(ship.GetHullParts().Keys) : new HashSet<HullPartPosition>();
            var existingStateful = ship != null ? ship.StatefulParts.ToList() : new List<StatefulPart>();
            foreach (BlueprintHull h in data.hull)
            {
                if (!(Settings(h.s) is HullPartSettings settings))
                {
                    error = $"Unknown hull part {h.s}";
                    return null;
                }
                HullPartPosition pos = t.Apply(new Vector3Int(h.x, h.y, h.z), (VoxelPartType)h.t);
                if (existingHull.Contains(pos))
                {
                    plan.Skipped++;
                    continue;
                }
                var placement = new HullPartPlacement(pos, t.Apply((PartOrientation)h.f), t.Apply((PartOrientation)h.u));
                var color = new HullPartColor(new Vector3Byte((byte)h.fr, (byte)h.fg, (byte)h.fb), new Vector3Byte((byte)h.br, (byte)h.bg, (byte)h.bb));
                plan.Hull.Add(new HullPart(settings, placement, color));
                ServerInventory.Add(plan.Cost, settings.BuildingCost);
            }
            foreach (BlueprintPart p in data.parts)
            {
                PartSettings settings = Settings(p.s);
                if (settings == null)
                {
                    error = $"Unknown part {p.s}";
                    return null;
                }
                IPartPlacement placement;
                if (p.onHull)
                {
                    HullPartPosition pos = t.Apply(new Vector3Int(p.hx, p.hy, p.hz), (VoxelPartType)p.ht);
                    if (existingStateful.Any(e => e.Placement is HullPartPlacement ep && ep.Position == pos))
                    {
                        plan.Skipped++;
                        continue;
                    }
                    placement = new HullPartPlacement(pos, t.Apply((PartOrientation)p.f), t.Apply((PartOrientation)p.u));
                }
                else
                {
                    Vector3 coord = t.Apply(new Vector3(p.x, p.y, p.z));
                    if (existingStateful.Any(e => e.Settings.id == p.s && e.Placement is FloatingPartPlacement ep && (ep.Coord - coord).sqrMagnitude < 0.0625f))
                    {
                        plan.Skipped++;
                        continue;
                    }
                    placement = new FloatingPartPlacement(coord, t.Apply((PartOrientation)p.f), t.Apply((PartOrientation)p.u));
                }
                plan.Stateful.Add((p.id, settings, placement));
                ServerInventory.Add(plan.Cost, settings.BuildingCost);
            }
            return plan;
        }

        internal static Dictionary<uint, int> TotalCost(BlueprintData data)
        {
            var cost = new Dictionary<uint, int>();
            foreach (BlueprintHull h in data.hull)
            {
                PartSettings s = Settings(h.s);
                if (s != null) ServerInventory.Add(cost, s.BuildingCost);
            }
            foreach (BlueprintPart p in data.parts)
            {
                PartSettings s = Settings(p.s);
                if (s != null) ServerInventory.Add(cost, s.BuildingCost);
            }
            return cost;
        }

        internal static bool Build(BlueprintData data, BlueprintTarget target, out string message)
        {
            TrackedPlayerServer player = ServerInventory.LocalPlayer();
            if (player == null)
            {
                message = "Only the host can build blueprints";
                return false;
            }
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            TrackedShipServer ship = target.NewShip ? null : ships.GetShip(target.ShipId);
            if (!target.NewShip && ship == null)
            {
                message = "The ship of this blueprint is gone";
                return false;
            }
            Plan plan = MakePlan(data, target, ship, out string error);
            if (plan == null)
            {
                message = error;
                return false;
            }
            if (plan.Hull.Count + plan.Stateful.Count == 0)
            {
                message = "Everything from this blueprint is already built";
                return true;
            }
            if (!player.IsUsingCheatFreeBuild)
            {
                var missing = ServerInventory.Missing(player, plan.Cost);
                if (missing.Count > 0)
                {
                    message = "Not enough materials: " + ServerInventory.Describe(missing);
                    return false;
                }
                ServerInventory.Remove(player, plan.Cost);
            }
            var ids = new Dictionary<int, ushort>();
            uint shipId;
            if (target.NewShip)
            {
                var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
                var worldIds = ServiceLocator.GetService<IWorldObjectsIdsServerProvider>();
                shipId = worldIds.CreateUniqueIdForObject();
                var stateful = new List<StatefulPart>();
                ushort next = 1;
                foreach (var s in plan.Stateful)
                {
                    ids[s.oldId] = next;
                    stateful.Add(new StatefulPart(next++, s.settings, s.placement));
                }
                var snapshot = new ShipStateSnapshot
                {
                    Id = shipId,
                    RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(shipId),
                    SpaceId = target.World.SpaceId,
                    Position = target.World.Position,
                    Rotation = target.World.Rotation,
                    Velocity = Vector3.zero,
                    RoomGasState = Array.Empty<RoomGasState>(),
                    HullParts = plan.Hull.ToArray(),
                    StatefulParts = stateful.ToArray(),
                };
                ships.LoadShip(snapshot);
                ServiceLocator.GetService<ShipInteractionNotifier>()?.NotifyShipCreation(snapshot.SpaceId, snapshot.Position);
            }
            else
            {
                shipId = ship.Id;
                foreach (HullPart h in plan.Hull)
                {
                    ship.AddHullPart(h.HullSettings, h.HullPlacement);
                    if (!h.Color.Equals(HullPartColor.Default))
                    {
                        ship.UpdateHullColor(h.HullPlacement.Position, h.Color);
                    }
                }
                foreach (var s in plan.Stateful)
                {
                    ids[s.oldId] = ship.AddStatefulPart(s.settings, s.placement);
                }
            }
            int cablesAdded = AddCables(data, shipId, ids);
            message = $"Built '{data.name}': {plan.Hull.Count + plan.Stateful.Count} parts, {cablesAdded} cables" + (plan.Skipped > 0 ? $" ({plan.Skipped} already there)" : "");
            Melon<MiningMod>.Logger.Msg(message);
            return true;
        }

        private static int AddCables(BlueprintData data, uint shipId, Dictionary<int, ushort> ids)
        {
            var cables = ServiceLocator.GetService<CablesServerTracker>();
            var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            if (cables == null)
            {
                return 0;
            }
            int added = 0;
            foreach (BlueprintCable c in data.cables)
            {
                if (!ids.TryGetValue(c.a, out ushort a) || !ids.TryGetValue(c.b, out ushort b)) continue;
                var sa = new EntityPartSocket(shipId, a, (byte)c.sa);
                var sb = new EntityPartSocket(shipId, b, (byte)c.sb);
                if (connections != null && (connections.IsSocketPhysicallyOccupied(sa) || connections.IsSocketPhysicallyOccupied(sb))) continue;
                cables.AddCable(new CableState
                {
                    Type = (CableType)c.type,
                    PlugA = new CablePlugSocket { EntityPartSocket = sa },
                    PlugB = new CablePlugSocket { EntityPartSocket = sb },
                });
                added++;
            }
            return added;
        }
    }
}
