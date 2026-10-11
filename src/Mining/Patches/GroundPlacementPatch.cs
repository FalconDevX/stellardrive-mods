using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using Items.Model;
using Managers.Interface.Model;
using Managers.Interface.Services;
using Managers.Server.Services;
using MelonLoader;
using Mining.Content;
using Players;
using Players.Interface.Services;
using Players.Services;
using Ships;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Network.Service;
using Ships.Services;
using Tools.Build;
using Tools.Build.Model;
using Tools.Common;
using UnityEngine;
using WorldTracking.Interface.Model;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Mining.Patches
{
    // W grze na gołej ziemi da się postawić tylko podłogę (zakłada nowy "statek"). Koparkę też można postawić prosto
    // na terenie: klient dopasowuje ją do nachylenia (teren pod czterema nogami), a serwer zakłada statek z samą koparką.
    internal static class GroundPlacement
    {
        // Pozycje stóp (x, z) względem środka koparki - LX, LZ z AssetSource/build_excavator.py.
        // Kolejność: lewy przód, prawy przód, lewy tył, prawy tył (przekątne 0-3 i 1-2).
        internal static readonly Vector2[] Feet = { new Vector2(-0.72f, -0.62f), new Vector2(0.72f, -0.62f), new Vector2(-0.72f, 0.62f), new Vector2(0.72f, 0.62f) };
    }

    [HarmonyPatch(typeof(PartPreview), "IsPartPlaceableInWorld")]
    internal static class PlaceableInWorldPatch
    {
        private static void Postfix(PartSettings ____actualPreviewSettings, bool isOnGround, ref bool __result)
        {
            if (!__result && isOnGround && Excavator.IsExcavator(____actualPreviewSettings))
            {
                __result = true;
            }
        }
    }

    // Hologram podglądu: na ziemi pokazujemy koparkę przechyloną zgodnie z terenem (tak, jak zostanie postawiona).
    // Przechylamy dzieci obiektu podglądu, bo jego własny obrót gra wykorzystuje w następnej klatce.
    [HarmonyPatch(typeof(PartPreview), "Update")]
    internal static class PreviewTiltPatch
    {
        private static void Postfix(PartSettings ____actualPreviewSettings, GameObject ____objectPreview, object ____snappedPlacement)
        {
            if (____objectPreview == null || !Excavator.IsExcavator(____actualPreviewSettings))
            {
                return;
            }
            Transform root = ____objectPreview.transform;
            Vector3 position = root.position;
            Quaternion rotation = root.rotation;
            bool tilt = false;
            if (____snappedPlacement == null)
            {
                Vector3 up = ServiceLocator.GetService<IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
                tilt = CreateShipWithExcavatorPatch.AlignToTerrain(ref position, ref rotation, up) == GroundPlacement.Feet.Length;
            }
            foreach (Transform child in root)
            {
                if (tilt)
                {
                    child.SetPositionAndRotation(position, rotation);
                }
                else
                {
                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.identity;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BuildToolLocalClient), "CreateShipWithPart")]
    internal static class CreateShipWithExcavatorPatch
    {
        private static bool Prefix(PreviewPlacement previewPlacement, IShipInteractionNetworkService ____interactionService)
        {
            if (!Excavator.IsExcavator(previewPlacement?.PartSettings))
            {
                return true;
            }
            try
            {
                Vector3 up = ServiceLocator.GetService<IPlayerUpProvider>()?.WorldPlayerUp ?? Vector3.up;
                Quaternion rotation = previewPlacement.Rotation;
                Vector3 position = previewPlacement.Position;
                AlignToTerrain(ref position, ref rotation, up);
                ____interactionService.CreateShip(new CreateShipNetworkRequest
                {
                    PartId = previewPlacement.PartSettings.id,
                    WorldTransform = new WorldTransform { SpaceId = ClientOrigin.LocalPhysicsSpaceId, Position = position, Rotation = rotation },
                });
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Nie udało się postawić koparki na ziemi: {e}");
            }
            return false;
        }

        // Teren pod każdą stopą (promień w dół wzdłuż "góry" gracza), z tego płaszczyzna: obrót i wysokość.
        // Zwraca, pod iloma stopami znaleziono teren.
        internal static int AlignToTerrain(ref Vector3 position, ref Quaternion rotation, Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward);
            var hits = new Vector3[GroundPlacement.Feet.Length];
            int found = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                Vector2 f = GroundPlacement.Feet[i];
                Vector3 above = position + right * f.x + forward * f.y + up * 3f;
                if (ClientScene.PhysicsScene.Raycast(above, -up, out RaycastHit hit, 8f, RaycastMasks.BuildDetectionLayer) && hit.collider.CompareTag("Planet"))
                {
                    hits[i] = hit.point;
                    found++;
                }
                else
                {
                    hits[i] = above - up * 3f;
                }
            }
            // Przekątne stóp (0-3 i 1-2) wyznaczają nachylenie.
            Vector3 normal = Vector3.Cross(hits[3] - hits[0], hits[1] - hits[2]).normalized;
            if (Vector3.Dot(normal, up) < 0f) normal = -normal;
            if (Vector3.Angle(normal, up) > 35f)
            {
                normal = Vector3.Slerp(up, normal, 35f / Vector3.Angle(normal, up)); // za stromo - ograniczamy przechył
            }
            Vector3 alignedForward = Vector3.ProjectOnPlane(forward, normal).normalized;
            rotation = Quaternion.LookRotation(alignedForward, normal);
            // Najwyżej położona stopa dotyka ziemi (pozostałe najwyżej kilka cm nad nią - fizyka dociśnie).
            float best = float.MinValue;
            Vector3 center = (hits[0] + hits[1] + hits[2] + hits[3]) / 4f;
            foreach (Vector3 h in hits) best = Mathf.Max(best, Vector3.Dot(h - center, normal));
            position = center + normal * (best + 0.03f);
            return found;
        }
    }

    // Serwer: CreateShip dla koparki - statek bez podłogi, z samą koparką w środku układu statku.
    [HarmonyPatch(typeof(ShipInteractionService), "RpcLogic___CreateShip_ServerRPC___2476380455")]
    internal static class ServerCreateShipPatch
    {
        // Parametry po pozycji (__0, __1) - nazwy w skompilowanej grze różnią się od tych z dekompilacji.
        private static bool Prefix(CreateShipNetworkRequest __0, FishNet.Connection.NetworkConnection __1)
        {
            CreateShipNetworkRequest P_0 = __0;
            FishNet.Connection.NetworkConnection P_1 = __1;
            var parts = new PartSettingsProvider();
            PartSettings settings;
            try
            {
                settings = parts.GetPartSettingById(P_0.PartId);
            }
            catch (ArgumentOutOfRangeException)
            {
                return true;
            }
            if (!Excavator.IsExcavator(settings))
            {
                return true;
            }
            try
            {
                var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
                var players = ServiceLocator.GetService<PlayersServerTracker>();
                if (P_1 == null || !spaces.HasSpace(P_0.WorldTransform.SpaceId))
                {
                    Melon<MiningMod>.Logger.Warning($"Koparka na ziemi: brak połączenia albo przestrzeni {P_0.WorldTransform.SpaceId}");
                    return false;
                }
                TrackedPlayerServer player = players.GetTrackedPlayerFromConnectionId(P_1.ClientId);
                if (player == null)
                {
                    Melon<MiningMod>.Logger.Warning($"Koparka na ziemi: nie znaleziono gracza {P_1.ClientId}");
                    return false;
                }
                if (!player.IsUsingCheatFreeBuild)
                {
                    List<ItemDrop> cost = settings.BuildingCost;
                    if (!player.HasItemsInInventory(cost))
                    {
                        Melon<MiningMod>.Logger.Msg("Koparka na ziemi: za mało surowców w ekwipunku");
                        return false;
                    }
                    player.RemoveItemsFromInventory(cost);
                }
                uint id = CreateExcavatorShip(settings, P_0.WorldTransform);
                Melon<MiningMod>.Logger.Msg($"Postawiono koparkę na ziemi (statek {id})");
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Serwer: nie udało się postawić koparki na ziemi: {e}");
            }
            return false;
        }

        internal static uint CreateExcavatorShip(PartSettings settings, WorldTransform transform)
        {
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            var ids = ServiceLocator.GetService<IWorldObjectsIdsServerProvider>();
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            uint id = ids.CreateUniqueIdForObject();
            var snapshot = new ShipStateSnapshot
            {
                Id = id,
                RelativeSpaceId = spaces.GetOrCreateShipRelativeSpace(id),
                SpaceId = transform.SpaceId,
                Position = transform.Position,
                Rotation = transform.Rotation,
                Velocity = Vector3.zero,
                RoomGasState = Array.Empty<RoomGasState>(),
                HullParts = Array.Empty<HullPart>(),
                StatefulParts = new[] { new StatefulPart(1, settings, new FloatingPartPlacement(Vector3.zero, PartOrientation.Forward, PartOrientation.Up)) },
            };
            ships.LoadShip(snapshot);
            ServiceLocator.GetService<ShipInteractionNotifier>()?.NotifyShipCreation(snapshot.SpaceId, snapshot.Position);
            return id;
        }
    }
}
