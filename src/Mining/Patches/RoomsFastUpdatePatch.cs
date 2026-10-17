using System;
using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.State;
using Ships.Interface.Values;
using Ships.Rooms;
using Ships.Rooms.Model;
using UnityEngine;
using WorldProvision.Interface.Services;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ShipRoomsVoxelUpdater), nameof(ShipRoomsVoxelUpdater.UpdateRooms))]
    [HarmonyPriority(Priority.Low)]
    internal static class RoomsFastUpdatePatch
    {
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, HashSet<Vector3Int>> ClearRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, HashSet<Vector3Int>>("_clearRoomsVoxelsToScan");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, HashSet<Vector3Int>> CreateRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, HashSet<Vector3Int>>("_createRoomsVoxelsToScan");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, List<Vector3Int>> RemoveWallsRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, List<Vector3Int>>("_removeWallsVoxels");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, List<Vector3Int>> AddWallsRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, List<Vector3Int>>("_addWallsVoxels");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, ShipRoomsVoxelsTree> TreeRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, ShipRoomsVoxelsTree>("_tree");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, ShipRoomsGases> GasesRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, ShipRoomsGases>("_gases");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, IWorldEnvironmentProvider> EnvRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, IWorldEnvironmentProvider>("_envProvider");
        private static readonly AccessTools.FieldRef<ShipRoomsVoxelUpdater, IShipStateRead> ShipRef = AccessTools.FieldRefAccess<ShipRoomsVoxelUpdater, IShipStateRead>("_ship");

        private static bool _disabled;

        private static readonly Stack<ShipRoomsVoxelsTreeNode> Stack = new Stack<ShipRoomsVoxelsTreeNode>();
        private static readonly List<ShipRoomsVoxelsTreeNode> Cleared = new List<ShipRoomsVoxelsTreeNode>();
        private static readonly HashSet<ShipRoomsVoxelsTreeNode> ClearedSet = new HashSet<ShipRoomsVoxelsTreeNode>(ReferenceComparer.Instance);
        private static readonly List<ShipRoomsVoxelsTreeNode> OutsideTouched = new List<ShipRoomsVoxelsTreeNode>();
        private static readonly List<ShipRoomsVoxelsTreeNode> SeedVisited = new List<ShipRoomsVoxelsTreeNode>();
        private static readonly HashSet<ShipRoomsVoxelsTreeNode> KnownOutside = new HashSet<ShipRoomsVoxelsTreeNode>(ReferenceComparer.Instance);
        private static readonly List<ShipRoomsVoxelsTreeNode> AllVisited = new List<ShipRoomsVoxelsTreeNode>();
        private static readonly HashSet<ushort> PreviousRooms = new HashSet<ushort>();
        private static readonly NodeHeap Heap = new NodeHeap();

        private static bool Prefix(ShipRoomsVoxelUpdater __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false;
            }
            if (_disabled || !MiningMod.FastRooms.Value)
            {
                return true;
            }
            HashSet<Vector3Int> clear = ClearRef(__instance);
            HashSet<Vector3Int> create = CreateRef(__instance);
            List<Vector3Int> removeWalls = RemoveWallsRef(__instance);
            List<Vector3Int> addWalls = AddWallsRef(__instance);
            if (clear.Count == 0 && create.Count == 0 && removeWalls.Count == 0 && addWalls.Count == 0)
            {
                return false;
            }
            RoomsPairs.CancelPairs(removeWalls, addWalls);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int clearSeeds = clear.Count, createSeeds = create.Count, wallsAdded = addWalls.Count, wallsRemoved = removeWalls.Count;
            ShipRoomsVoxelsTree tree = TreeRef(__instance);
            ShipRoomsGases gases = GasesRef(__instance);
            try
            {
                float density = EnvRef(__instance).GetAtmosphereDensityAtPosition(ShipRef(__instance).RelativeSpaceId, Vector3.zero);
                var atmosphere = new GasState { GasItemId = ItemsConstants.OxygenItemId, GasDensity = density };

                ClearRooms(tree, gases, clear);
                tree.RemoveWalls(removeWalls);
                tree.AddWalls(addWalls);
                foreach (Vector3Int seed in create)
                {
                    CreateRoomFromSeed(tree, gases, seed, atmosphere);
                }

                foreach (ushort room in PreviousRooms)
                {
                    tree.RemoveRoomId(room);
                    gases.RemoveRoom(room);
                }
                var empty = new GasState { GasItemId = 0u, GasDensity = 0f };
                foreach (ShipRoomsVoxelsTreeNode node in Cleared) node.TmpGasContent = empty;
                foreach (ShipRoomsVoxelsTreeNode node in OutsideTouched) node.TmpGasContent = empty;
                foreach (ShipRoomsVoxelsTreeNode node in AllVisited) node.Visited = false;
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                if (ms > 50.0)
                {
                    Melon<MiningMod>.Logger.Msg($"[rooms] {ms:0} ms: walls +{wallsAdded} -{wallsRemoved}, cleared {Cleared.Count} cells from {clearSeeds} seeds, filled {AllVisited.Count} cells from {createSeeds} seeds");
                }
            }
            catch (Exception e)
            {
                _disabled = true;
                Melon<MiningMod>.Logger.Warning($"Fast room recalculation disabled: {e}");
            }
            finally
            {
                PreviousRooms.Clear();
                Cleared.Clear();
                ClearedSet.Clear();
                OutsideTouched.Clear();
                AllVisited.Clear();
                SeedVisited.Clear();
                KnownOutside.Clear();
                Stack.Clear();
                Heap.Clear();
                clear.Clear();
                create.Clear();
                removeWalls.Clear();
                addWalls.Clear();
            }
            return false;
        }

        private static void ClearRooms(ShipRoomsVoxelsTree tree, ShipRoomsGases gases, HashSet<Vector3Int> seeds)
        {
            ushort noRoom = ShipRoomsConstants.NoRoomId;
            foreach (Vector3Int seed in seeds)
            {
                Stack.Push(tree.GetLeaf(seed));
            }
            while (Stack.Count > 0)
            {
                ShipRoomsVoxelsTreeNode node = Stack.Pop();
                if (node.Visited || node.WallCount > 0 || node.RoomId == noRoom)
                {
                    continue;
                }
                node.Visited = true;
                Cleared.Add(node);
                ClearedSet.Add(node);
                PreviousRooms.Add(node.RoomId);
                PushAll(node.NeighborsLeft);
                PushAll(node.NeighborsRight);
                PushAll(node.NeighborsBottom);
                PushAll(node.NeighborsTop);
                PushAll(node.NeighborsBack);
                PushAll(node.NeighborsForward);
            }
            foreach (ShipRoomsVoxelsTreeNode node in Cleared)
            {
                node.TmpGasContent = gases.TryGetRoomGasContent(node.RoomId, out GasState gas) ? gas : new GasState { GasItemId = 0u, GasDensity = 0f };
                node.RoomId = noRoom;
                node.Visited = false;
            }
        }

        private static void PushAll(List<ShipRoomsVoxelsTreeNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++) Stack.Push(nodes[i]);
        }

        private static void CreateRoomFromSeed(ShipRoomsVoxelsTree tree, ShipRoomsGases gases, Vector3Int seed, GasState atmosphere)
        {
            ushort noRoom = ShipRoomsConstants.NoRoomId;
            Vector3Int lower = tree.LowerBounds;
            Vector3Int upper = tree.UpperBounds;
            SeedVisited.Clear();
            Heap.Clear();
            Heap.Push(tree.GetLeaf(seed));
            bool outside = false;
            while (Heap.Count > 0)
            {
                ShipRoomsVoxelsTreeNode node = Heap.PopAny();
                if (node.Visited)
                {
                    if (KnownOutside.Contains(node))
                    {
                        outside = true;
                        break;
                    }
                    continue;
                }
                if (node.RoomId > noRoom || node.WallCount > 0)
                {
                    continue;
                }
                node.Visited = true;
                SeedVisited.Add(node);
                AllVisited.Add(node);
                if (!ClearedSet.Contains(node))
                {
                    node.TmpGasContent = atmosphere;
                    OutsideTouched.Add(node);
                }
                Vector3Int c = node.Coord;
                Vector3Int end = c + Vector3Int.one * node.Bounds - Vector3Int.one;
                if (c.x <= lower.x || c.y <= lower.y || c.z <= lower.z || end.x >= upper.x || end.y >= upper.y || end.z >= upper.z)
                {
                    outside = true;
                    break;
                }
                PushAll(node.NeighborsTop);
                PushAll(node.NeighborsLeft);
                PushAll(node.NeighborsRight);
                PushAll(node.NeighborsBack);
                PushAll(node.NeighborsForward);
                PushAll(node.NeighborsBottom);
                while (Stack.Count > 0) Heap.Push(Stack.Pop());
            }
            Stack.Clear();
            if (SeedVisited.Count == 0)
            {
                return;
            }
            if (outside)
            {
                foreach (ShipRoomsVoxelsTreeNode node in SeedVisited)
                {
                    node.RoomId = 0;
                    KnownOutside.Add(node);
                }
                return;
            }
            ushort roomId = tree.RegisterNewRoomId();
            float volume = 0f;
            float gas = 0f;
            foreach (ShipRoomsVoxelsTreeNode node in SeedVisited)
            {
                float v = node.Bounds * node.Bounds * node.Bounds;
                volume += v;
                gas += node.TmpGasContent.GasDensity * v;
                node.RoomId = roomId;
            }
            gases.SetRoomGasContent(roomId, new RoomGasState
            {
                RoomVolume = volume * ShipRoomsConstants.GasVolumeCapacityPerVoxel,
                RoomScanSeedCoord = seed,
                GasState = new GasState { GasItemId = ItemsConstants.OxygenItemId, GasDensity = gas / volume },
            });
        }

        private sealed class NodeHeap
        {
            private readonly List<ShipRoomsVoxelsTreeNode> _items = new List<ShipRoomsVoxelsTreeNode>();
            public int Count => _items.Count;
            public void Clear() => _items.Clear();

            public void Push(ShipRoomsVoxelsTreeNode node)
            {
                if (node.Visited && !KnownOutside.Contains(node)) return;
                _items.Add(node);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_items[parent].Bounds >= _items[i].Bounds) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public ShipRoomsVoxelsTreeNode PopAny()
            {
                ShipRoomsVoxelsTreeNode top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, best = i;
                    if (l < _items.Count && _items[l].Bounds > _items[best].Bounds) best = l;
                    if (r < _items.Count && _items[r].Bounds > _items[best].Bounds) best = r;
                    if (best == i) break;
                    (_items[best], _items[i]) = (_items[i], _items[best]);
                    i = best;
                }
                return top;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<ShipRoomsVoxelsTreeNode>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ShipRoomsVoxelsTreeNode a, ShipRoomsVoxelsTreeNode b) => ReferenceEquals(a, b);
            public int GetHashCode(ShipRoomsVoxelsTreeNode n) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(n);
        }
    }
}

namespace Mining.Patches
{
    internal static class RoomsPairs
    {
        private static readonly System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int> Counts = new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int>();

        internal static void CancelPairs(System.Collections.Generic.List<UnityEngine.Vector3Int> removed, System.Collections.Generic.List<UnityEngine.Vector3Int> added)
        {
            if (removed.Count == 0 || added.Count == 0) return;
            Counts.Clear();
            foreach (UnityEngine.Vector3Int v in added)
            {
                Counts.TryGetValue(v, out int c);
                Counts[v] = c + 1;
            }
            Cancel.Clear();
            foreach (UnityEngine.Vector3Int v in removed)
            {
                if (Counts.TryGetValue(v, out int c) && c > 0)
                {
                    Counts[v] = c - 1;
                    Cancel.TryGetValue(v, out int k);
                    Cancel[v] = k + 1;
                }
            }
            if (Cancel.Count > 0)
            {
                Drop(removed, new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int>(Cancel));
                Drop(added, Cancel);
            }
            Counts.Clear();
            Cancel.Clear();
        }

        private static readonly System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int> Cancel = new System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int>();

        private static void Drop(System.Collections.Generic.List<UnityEngine.Vector3Int> list, System.Collections.Generic.Dictionary<UnityEngine.Vector3Int, int> cancel)
        {
            int write = 0;
            for (int i = 0; i < list.Count; i++)
            {
                UnityEngine.Vector3Int v = list[i];
                if (cancel.TryGetValue(v, out int k) && k > 0)
                {
                    cancel[v] = k - 1;
                    continue;
                }
                list[write++] = v;
            }
            list.RemoveRange(write, list.Count - write);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Ships.Rooms.ShipRoomsGases), nameof(Ships.Rooms.ShipRoomsGases.LoadRoom))]
    internal static class RoomsLoadMissingPatch
    {
        private static int _skipped;

        private static bool Prefix(ushort __0)
        {
            if (__0 != Ships.Interface.Values.ShipRoomsConstants.NoRoomId)
            {
                return true;
            }
            if (++_skipped == 1)
            {
                MelonLoader.Melon<MiningMod>.Logger.Msg("Skipped saved rooms that no longer exist (they disappear on the next save)");
            }
            return false;
        }
    }
}
