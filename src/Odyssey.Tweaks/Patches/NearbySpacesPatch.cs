using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Ships;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Spaces;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(ActiveSpacesServerTracker), nameof(ActiveSpacesServerTracker.UpdateActiveSpaces))]
    internal static class NearbySpacesPatch
    {
        private const float RecheckSeconds = 1f;
        private static readonly HashSet<uint> Nearby = new HashSet<uint>();
        private static float _nextCheck;
        private static bool _loggedError;

        private static void Postfix(HashSet<uint> ____activeSpaces, List<uint> ____activeSpacesList)
        {
            if (!TweaksMod.KeepNearbyShipsLoaded.Value || ____activeSpaces == null || ____activeSpaces.Count == 0)
            {
                return;
            }
            try
            {
                if (Time.unscaledTime >= _nextCheck)
                {
                    _nextCheck = Time.unscaledTime + RecheckSeconds;
                    Refresh(____activeSpaces);
                }
                foreach (uint space in Nearby)
                {
                    if (____activeSpaces.Add(space)) ____activeSpacesList.Add(space);
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<TweaksMod>.Logger.Warning($"Nearby spaces: {e.Message}");
                }
            }
        }

        private static void Refresh(HashSet<uint> active)
        {
            Nearby.Clear();
            var ships = ServiceCache.Get<ShipsServerTracker>();
            var transpose = ServiceCache.Get<IWorldTransposeServer>();
            var spaces = ServiceCache.Get<ISpacesServerProvider>();
            if (ships == null || transpose == null || spaces == null)
            {
                return;
            }
            var anchors = active.ToList();
            foreach (TrackedShipServer ship in ships.Ships.OfType<TrackedShipServer>())
            {
                uint space = ship.PhysicsState.SpaceId;
                if (active.Contains(space) || Nearby.Contains(space) || !(spaces.GetSpace(space) is PlanetSurfaceSpace)) continue;
                foreach (uint anchor in anchors)
                {
                    if (!(spaces.GetSpace(anchor) is PlanetSurfaceSpace)) continue;
                    if (transpose.Position(space, anchor, ship.PhysicsState.Position).magnitude < TweaksMod.KeepNearbyShipsDistance.Value)
                    {
                        Nearby.Add(space);
                        break;
                    }
                }
            }
        }
    }
}
