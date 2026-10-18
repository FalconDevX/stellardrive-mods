using System.Collections.Generic;
using HarmonyLib;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Interface.Values;
using Ships.Physics;
using Ships.Physics.Collision;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(ShipCollisionDestruction), nameof(ShipCollisionDestruction.UpdateDestruction))]
    internal static class CollisionDestructionPatch
    {
        private static readonly AccessTools.FieldRef<ShipCollisionDestruction, List<ContactPoint>> ContactsRef =
            AccessTools.FieldRefAccess<ShipCollisionDestruction, List<ContactPoint>>("_contacts");
        private static readonly AccessTools.FieldRef<ShipCollisionDestruction, ShipRigidbody> RigidbodyRef =
            AccessTools.FieldRefAccess<ShipCollisionDestruction, ShipRigidbody>("shipRigidbody");
        private static readonly AccessTools.FieldRef<ShipCollisionDestruction, IShipStateWrite> ShipRef =
            AccessTools.FieldRefAccess<ShipCollisionDestruction, IShipStateWrite>("_ship");
        private static readonly AccessTools.FieldRef<ShipCollisionDestruction, IShipsServerProvider> ShipsRef =
            AccessTools.FieldRefAccess<ShipCollisionDestruction, IShipsServerProvider>("_ships");
        private static readonly Dictionary<PartSettings, float> PartHardness = new Dictionary<PartSettings, float>();
        private const float RescanSeconds = 2f;
        private const int UnusedFrames = 600;

        private sealed class CachedHardness
        {
            public int PartCount = -1;
            public float NextScan;
            public int Used;
            public float Value;
        }

        private static readonly Dictionary<uint, CachedHardness> ShipHardness = new Dictionary<uint, CachedHardness>();
        private static readonly List<uint> Stale = new List<uint>();

        private static void Prefix(ShipCollisionDestruction __instance)
        {
            if (!TweaksMod.FastCollisionDestruction.Value) return;
            List<ContactPoint> contacts = ContactsRef(__instance);
            if (contacts == null || contacts.Count == 0) return;
            ShipRigidbody rigidbody = RigidbodyRef(__instance);
            IShipStateWrite ship = ShipRef(__instance);
            IShipsServerProvider ships = ShipsRef(__instance);
            if (rigidbody == null || ship == null || ships == null) return;
            float threshold = MinHardness(ships, ((IShipStateRead)ship).Id) * rigidbody.Mass;
            if (!(threshold > 0f)) return;
            contacts.RemoveAll(c => c.otherCollider == null || c.impulse.magnitude < threshold);
        }

        private static float MinHardness(IShipsServerProvider ships, uint shipId)
        {
            int partCount = 0;
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(shipId))
            {
                if (data.Ship != null) partCount += data.Ship.PartCount;
            }
            if (ShipHardness.TryGetValue(shipId, out CachedHardness cached) && cached.PartCount == partCount && Time.time < cached.NextScan)
            {
                cached.Used = Time.frameCount;
                return cached.Value;
            }
            if (cached == null)
            {
                Prune();
                ShipHardness[shipId] = cached = new CachedHardness();
            }
            cached.PartCount = partCount;
            cached.NextScan = Time.time + RescanSeconds;
            cached.Used = Time.frameCount;
            cached.Value = ScanMinHardness(ships, shipId);
            return cached.Value;
        }

        private static void Prune()
        {
            int frame = Time.frameCount;
            foreach (KeyValuePair<uint, CachedHardness> pair in ShipHardness)
            {
                if (frame - pair.Value.Used > UnusedFrames) Stale.Add(pair.Key);
            }
            foreach (uint id in Stale)
            {
                ShipHardness.Remove(id);
            }
            Stale.Clear();
        }

        private static float ScanMinHardness(IShipsServerProvider ships, uint shipId)
        {
            float min = Mathf.Min(HullConstants.MetalHardness, Mathf.Min(HullConstants.GlassHardness, HullConstants.WoodHardness));
            foreach (RootRelativeShipData data in ships.GetRootConnectedShips(shipId))
            {
                if (data.Ship == null) continue;
                foreach (StatefulPart part in data.Ship.StatefulParts)
                {
                    float hardness = Hardness(part.Settings);
                    if (hardness < min) min = hardness;
                    if (min <= 0f) return 0f;
                }
            }
            return min;
        }

        private static float Hardness(PartSettings settings)
        {
            if (settings == null) return 0f;
            if (PartHardness.TryGetValue(settings, out float hardness)) return hardness;
            ShipPartBounds bounds = settings.part != null ? settings.part.GetComponent<ShipPartBounds>() : null;
            hardness = bounds != null ? bounds.bounds.x * bounds.bounds.y * bounds.bounds.z * 400f : 0f;
            PartHardness[settings] = hardness;
            return hardness;
        }
    }
}
