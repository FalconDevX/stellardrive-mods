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
    // Szybsze przeliczanie pomieszczeń (tlen) po zmianie kadłuba - to była główna przyczyna lagów przy budowaniu
    // (~60 ms na blok, zmierzone). Gra po każdej zmianie zalewała CAŁĄ pustą przestrzeń wokół statku (drzewo voxeli
    // 4 na metr) - dwa razy: przy czyszczeniu i przy szukaniu pomieszczeń. Ta wersja daje ten sam wynik, ale:
    //  - czyści tylko prawdziwe pomieszczenia (przestrzeń "na zewnątrz" i tak zostaje na zewnątrz),
    //  - szukając pomieszczenia, kończy, gdy tylko dotrze do granicy drzewa (czyli wyszło na zewnątrz),
    //    idąc najpierw przez największe węzły drzewa - z przestrzeni wokół statku wychodzi po kilku krokach.
    // Zamknięte pomieszczenie jest i tak przechodzone w całości (jak w grze), więc tlen liczy się tak samo.
    [HarmonyPatch(typeof(ShipRoomsVoxelUpdater), nameof(ShipRoomsVoxelUpdater.UpdateRooms))]
    [HarmonyPriority(Priority.Low)] // po RoomsDebouncePatch (ten może pominąć przeliczenie w trakcie budowania)
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
        private static readonly List<ShipRoomsVoxelsTreeNode> AllVisited = new List<ShipRoomsVoxelsTreeNode>();
        private static readonly HashSet<ushort> PreviousRooms = new HashSet<ushort>();
        private static readonly NodeHeap Heap = new NodeHeap();

        private static bool Prefix(ShipRoomsVoxelUpdater __instance, bool __runOriginal)
        {
            if (!__runOriginal)
            {
                return false; // RoomsDebouncePatch: jeszcze budujesz - przeliczymy później
            }
            if (_disabled)
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
            }
            catch (Exception e)
            {
                _disabled = true; // coś się nie zgadza z wersją gry - od teraz liczy gra
                Melon<MiningMod>.Logger.Warning($"Szybkie przeliczanie pomieszczeń wyłączone: {e}");
            }
            finally
            {
                PreviousRooms.Clear();
                Cleared.Clear();
                ClearedSet.Clear();
                OutsideTouched.Clear();
                AllVisited.Clear();
                SeedVisited.Clear();
                Stack.Clear();
                Heap.Clear();
                clear.Clear();
                create.Clear();
                removeWalls.Clear();
                addWalls.Clear();
            }
            return false;
        }

        // Jak ClearRoomsVoxels w grze, ale bez wchodzenia w przestrzeń na zewnątrz (RoomId == NoRoomId) - tam nie ma
        // czego czyścić. Ściany są jeszcze stare, więc zalewanie pomieszczenia nie wyjdzie poza nie.
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
                ShipRoomsVoxelsTreeNode node = Heap.Pop();
                if (node.Visited || node.RoomId > noRoom || node.WallCount > 0)
                {
                    continue;
                }
                node.Visited = true;
                SeedVisited.Add(node);
                AllVisited.Add(node);
                if (!ClearedSet.Contains(node))
                {
                    // dotąd "na zewnątrz" - gra nadałaby mu gaz atmosfery przy czyszczeniu
                    node.TmpGasContent = atmosphere;
                    OutsideTouched.Add(node);
                }
                Vector3Int c = node.Coord;
                Vector3Int end = c + Vector3Int.one * node.Bounds - Vector3Int.one;
                if (c.x <= lower.x || c.y <= lower.y || c.z <= lower.z || end.x >= upper.x || end.y >= upper.y || end.z >= upper.z)
                {
                    outside = true; // połączone z granicą drzewa = na zewnątrz; reszty nie trzeba przechodzić
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
                foreach (ShipRoomsVoxelsTreeNode node in SeedVisited) node.RoomId = 0;
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

        // Kolejka "największy węzeł najpierw" - duże węzły drzewa to otwarta przestrzeń, którą najszybciej dochodzi się do granicy.
        private sealed class NodeHeap
        {
            private readonly List<ShipRoomsVoxelsTreeNode> _items = new List<ShipRoomsVoxelsTreeNode>();
            public int Count => _items.Count;
            public void Clear() => _items.Clear();

            public void Push(ShipRoomsVoxelsTreeNode node)
            {
                if (node.Visited) return;
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

            public ShipRoomsVoxelsTreeNode Pop()
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
