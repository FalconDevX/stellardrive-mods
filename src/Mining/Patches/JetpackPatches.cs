using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Audio.Interface;
using Core.Services;
using HarmonyLib;
using Items.Interface.Service;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Players;
using Players.Effects;
using Players.Interface.Model;
using Players.Inventory;
using Players.LocalClient.Controls.Model;
using Players.Network;
using Players.Network.Model;
using Players.Physics;
using TMPro;
using UI.Inventory;
using UnityEngine;
using UnityEngine.UI;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;
using Object = UnityEngine.Object;

namespace Mining.Patches
{
    // --- Kto jest teraz liczony: host liczy ruch każdego gracza, klient przewiduje swój ---
    [HarmonyPatch(typeof(TrackedPlayerServer), "CalculateNewPhysicsState")]
    internal static class JetpackServerContextPatch
    {
        private static void Prefix(TrackedPlayerServer __instance) => Jetpack.CurrentServerPlayer = __instance;
        private static void Finalizer() => Jetpack.CurrentServerPlayer = null;
    }

    [HarmonyPatch(typeof(LocalPlayerPhysicsPredictions), "ProcessPhysicsStep")]
    internal static class JetpackPredictionContextPatch
    {
        private static void Prefix() => Jetpack.CurrentIsLocalPrediction = true;
        private static void Finalizer() => Jetpack.CurrentIsLocalPrediction = false;
    }

    // --- Ciąg: w powietrzu, przy przytrzymanym skoku, z drewnem w slocie paliwa ---
    [HarmonyPatch(typeof(PlayerWalkingPhysics), "ProcessAirForces")]
    internal static class JetpackPhysicsPatch
    {
        private static void Postfix(ref PlayerPhysicsState __0, PlayerControlInput __1, Vector3 ____currentUp, Vector3 ____currentGravity, float ____deltaTime)
        {
            if (Jetpack.CurrentIsLocalPrediction)
            {
                Jetpack.LocalHasGravity = ____currentGravity != Vector3.zero;
            }
            if ((__1.VerticalInput <= 0f && !(Jetpack.TestForceUp && Jetpack.CurrentServerPlayer != null)) || ____currentGravity == Vector3.zero || (Jetpack.CurrentServerPlayer == null && !Jetpack.CurrentIsLocalPrediction))
            {
                return;
            }
            if (!Jetpack.IsUnlocked() || !Jetpack.HasFuel())
            {
                return;
            }
            float upSpeed = Vector3.Dot(__0.Velocity, ____currentUp);
            float maxUp = MiningMod.JetpackMaxSpeed.Value;
            if (upSpeed < maxUp)
            {
                // znosi grawitację i daje stałe przyspieszenie w górę
                float accel = ____currentGravity.magnitude + MiningMod.JetpackAcceleration.Value;
                __0.Velocity += ____currentUp * Mathf.Min(accel * ____deltaTime, maxUp - upSpeed + ____currentGravity.magnitude * ____deltaTime);
            }
            Jetpack.Burn(____deltaTime);
        }
    }

    // --- Włącznik (J): wyłączony jetpack = w powietrzu z grawitacją nie wysyłamy "skoku" (na ziemi skok działa normalnie) ---
    [HarmonyPatch(typeof(TrackedPlayerLocalClient), "SetInputs")]
    internal static class JetpackToggleInputPatch
    {
        private static void Prefix(TrackedPlayerLocalClient __instance, ref PlayerControlInput __0)
        {
            if (Jetpack.Enabled || __0.MoveInput.y <= 0f || !Jetpack.LocalHasGravity || __instance.PhysicsState.IsOnGround)
            {
                return;
            }
            __0.MoveInput.y = 0f;
        }
    }

    // --- Slot paliwa przyjmuje tylko drewno ---
    [HarmonyPatch]
    internal static class JetpackAddItemPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(PlayerInventoryTransactions), "InventoryTransaction"), "TryAddItem");

        // Dla plecaka (InventoryAccessor) i przedmiotów innych niż drewno: dokładamy jak gra, ale z pominięciem slotu paliwa.
        private static bool Prefix(object ____accessor, IItemSettingsProvider ____itemsList, ref ItemDrop itemDrop)
        {
            if (____accessor == null || ____accessor.GetType().Name != "InventoryAccessor" || itemDrop.ItemId == Jetpack.FuelItem || !Jetpack.IsUnlocked())
            {
                return true;
            }
            var accessor = Traverse.Create(____accessor);
            int count = accessor.Property("Count").GetValue<int>();
            ItemSettings settings = ____itemsList.GetItemSettingsById(itemDrop.ItemId);
            for (byte b = 0; b < count && itemDrop.Quantity > 0; b++)
            {
                if (b == Jetpack.FuelSlot)
                {
                    continue;
                }
                InventorySlot slot = accessor.Method("GetSlot", b).GetValue<InventorySlot>();
                if (!slot.HasItem)
                {
                    slot = new InventorySlot { HasItem = true, ItemId = itemDrop.ItemId, Quantity = Math.Min(itemDrop.Quantity, settings.maxStackSize) };
                    itemDrop.Quantity -= slot.Quantity;
                    accessor.Method("SetSlot", b, slot).GetValue();
                }
                else if (slot.ItemId == itemDrop.ItemId)
                {
                    byte add = (byte)(Math.Min(slot.Quantity + itemDrop.Quantity, settings.maxStackSize) - slot.Quantity);
                    if (add > 0)
                    {
                        slot.Quantity += add;
                        itemDrop.Quantity -= add;
                        accessor.Method("SetSlot", b, slot).GetValue();
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerInventoryTransactions))]
    internal static class JetpackMoveItemPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.MoveItem))]
        private static bool Move(PlayerInventoryTransactions __instance, ItemSlotId __0, ItemSlotId __1) => Allowed(__instance, __0, __1);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.MoveSplitItem))]
        private static bool MoveSplit(PlayerInventoryTransactions __instance, ItemSlotId __0, ItemSlotId __1) => Allowed(__instance, __0, __1);

        private static bool IsFuelSlot(ItemSlotId id) => id.Type == InventoryType.MainInventory && id.SlotId == Jetpack.FuelSlot;

        private static bool Allowed(PlayerInventoryTransactions inventory, ItemSlotId source, ItemSlotId destination)
        {
            if (!Jetpack.IsUnlocked())
            {
                return true;
            }
            ItemDrop moving = inventory.GetItemAtSlot(source);
            ItemDrop target = inventory.GetItemAtSlot(destination);
            if (IsFuelSlot(destination) && moving.Quantity > 0 && moving.ItemId != Jetpack.FuelItem)
            {
                return false; // do slotu paliwa tylko drewno
            }
            if (IsFuelSlot(source) && target.Quantity > 0 && target.ItemId != Jetpack.FuelItem)
            {
                return false; // zamiana wrzuciłaby coś innego do slotu paliwa
            }
            return true;
        }
    }

    // --- Slot paliwa pokazany osobno obok ekwipunku ---
    [HarmonyPatch(typeof(InventoryGrid), "Awake")]
    internal static class JetpackFuelSlotUiPatch
    {
        private static void Postfix(InventoryGrid __instance, InventoryType ___source, InventorySlotUI[] ____slots, Component ___stringEvent)
        {
            try
            {
                if (___source != InventoryType.MainInventory || ____slots == null || ____slots.Length <= Jetpack.FuelSlot || !Jetpack.IsUnlocked())
                {
                    return;
                }
                InventorySlotUI slot = ____slots[Jetpack.FuelSlot];
                var rect = (RectTransform)slot.transform;
                // Rozmiar na ekranie jak zwykłe sloty - kosz ma inną skalę niż siatka ekwipunku, więc liczymy w świecie.
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 worldSize = new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));

                // Osobno, poza oknem ekwipunku: nad koszem (kosz stoi obok okna), w tym samym kontenerze co kosz.
                var trash = __instance.transform.parent != null ? __instance.transform.root.GetComponentsInChildren<DestroySlotUI>(true)
                    .OrderBy(t => Vector3.Distance(t.transform.position, __instance.transform.position)).FirstOrDefault() : null;
                if (trash != null)
                {
                    var trashRect = (RectTransform)trash.transform;
                    rect.SetParent(trashRect.parent, false);
                    rect.localScale = Vector3.one;
                    Vector3 parentScale = trashRect.parent.lossyScale;
                    Vector2 size = new Vector2(worldSize.x / Mathf.Max(1e-4f, parentScale.x), worldSize.y / Mathf.Max(1e-4f, parentScale.y));
                    rect.anchorMin = trashRect.anchorMin;
                    rect.anchorMax = trashRect.anchorMax;
                    rect.pivot = trashRect.pivot;
                    rect.sizeDelta = size;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    // układ okna ustala się dopiero po otwarciu - pozycję nad koszem pilnujemy co klatkę
                    var follow = slot.gameObject.AddComponent<FuelSlotAboveTrash>();
                    follow.Trash = trashRect;
                }
                else
                {
                    (slot.gameObject.GetComponent<LayoutElement>() ?? slot.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    Vector3 parentScale = rect.parent.lossyScale;
                    rect.sizeDelta = new Vector2(worldSize.x / Mathf.Max(1e-4f, parentScale.x), worldSize.y / Mathf.Max(1e-4f, parentScale.y));
                    rect.anchoredPosition = new Vector2(60f, -40f);
                }
                rect.SetAsLastSibling();

                // podpis nad slotem (kopia napisu z okna ekwipunku, bez tłumaczenia)
                TMP_Text template = ___stringEvent != null ? ___stringEvent.GetComponent<TMP_Text>() : null;
                if (template != null)
                {
                    GameObject label = Object.Instantiate(template.gameObject, rect);
                    foreach (Component c in label.GetComponents<Component>().Where(c => c.GetType().Name.StartsWith("Localize")))
                    {
                        Object.DestroyImmediate(c);
                    }
                    var text = label.GetComponent<TMP_Text>();
                    text.text = "JETPACK FUEL";
                    text.enableWordWrapping = false;
                    // czcionka tej samej wielkości na ekranie co w oknie ekwipunku (inna skala kontenera)
                    float scale = template.transform.lossyScale.y / Mathf.Max(1e-4f, rect.lossyScale.y);
                    text.enableAutoSizing = false;
                    text.fontSize = Mathf.Clamp(template.fontSize * 0.5f, 12f, 20f) * scale;
                    text.alignment = TextAlignmentOptions.Bottom;
                    var labelRect = (RectTransform)label.transform;
                    labelRect.anchorMin = new Vector2(0.5f, 1f);
                    labelRect.anchorMax = new Vector2(0.5f, 1f);
                    labelRect.pivot = new Vector2(0.5f, 0f);
                    labelRect.localScale = Vector3.one;
                    labelRect.anchoredPosition = new Vector2(0f, 4f * scale);
                    labelRect.sizeDelta = new Vector2(200f * scale, 30f * scale);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Slot paliwa jetpacka: {e.Message}");
            }
        }
    }

    // Trzyma slot paliwa nad koszem (w świecie, więc niezależnie od skali i kotwic kontenera), z odstępem.
    internal sealed class FuelSlotAboveTrash : MonoBehaviour
    {
        internal RectTransform Trash;
        private readonly Vector3[] _corners = new Vector3[4];

        private void LateUpdate()
        {
            if (Trash == null)
            {
                return;
            }
            var rect = (RectTransform)transform;
            Trash.GetWorldCorners(_corners); // 0 lewy dół, 1 lewa góra, 2 prawa góra, 3 prawy dół
            Vector3 up = (_corners[1] - _corners[0]).normalized;
            Vector3 topCenter = (_corners[1] + _corners[2]) * 0.5f;
            float trashHeight = Vector3.Distance(_corners[0], _corners[1]);
            rect.GetWorldCorners(_corners);
            float slotHeight = Vector3.Distance(_corners[0], _corners[1]);
            // odstęp: miejsce na napis "JETPACK FUEL" nad koszem
            rect.position = topCenter + up * (slotHeight * 0.5f + Mathf.Max(trashHeight * 0.35f, slotHeight * 0.3f));
        }
    }

    // --- Płomienie z dysz kostiumu i dźwięk, plecak na kostiumie ---
    [HarmonyPatch(typeof(PlayerJetpackEffects), nameof(PlayerJetpackEffects.UpdateRcsEffects))]
    internal static class JetpackEffectsPatch
    {
        private static bool _loggedError;

        // Efekty to tylko wygląd - błąd w nich nie może przerwać aktualizacji gracza (zastyganie w powietrzu).
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && !_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Warning($"Efekty jetpacka: {__exception}");
            }
            return null;
        }

        private static readonly HashSet<PlayerJetpackEffects> Flying = new HashSet<PlayerJetpackEffects>();
        private static readonly HashSet<PlayerJetpackEffects> WithModel = new HashSet<PlayerJetpackEffects>();
        private static Mesh _mesh;
        private static Material _material;

        private static bool Prefix(PlayerJetpackEffects __instance, PlayerPhysicsState __0, IPlayerStateClientRead ____state, ISpacesClientProvider ____spaces,
            ParticleSystem ___leftHipRcs, ParticleSystem ___rightHipRcs, ParticleSystem ___leftShoulderRcs, ParticleSystem ___rightShoulderRcs)
        {
            if (____state == null || ____spaces == null)
            {
                return true;
            }
            bool unlocked = Jetpack.IsUnlocked();
            if (unlocked && !WithModel.Contains(__instance))
            {
                WithModel.Add(__instance);
                AttachModel(__instance, __0.Rotation, ___leftShoulderRcs, ___rightShoulderRcs, ___leftHipRcs, ___rightHipRcs);
            }
            ISpace space = ____spaces.GetSpace(__0.SpaceId);
            bool onPlanet = space is PlanetSurfaceSpace || space is ShipRelativeSpace;
            bool flying = unlocked && onPlanet && ____state.PressedKeys.Up && !__0.IsOnGround && ____state.ClientState.Type == PlayerStateType.Walking;
            var audio = ServiceLocator.GetService<IAudioPlayer>();
            if (unlocked)
            {
                JetpackFx fx = JetpackFx.For(__instance);
                fx.Flying = flying;
                fx.Up = __0.Rotation * Vector3.up;
            }
            if (flying)
            {
                if (Flying.Add(__instance) && !JetpackFx.HasCustomSound)
                {
                    audio?.PlayAudioEvent3D("Event:/Players/JetpackThruster", __instance.gameObject);
                }
                // Nie każdy model gracza ma dysze biodrowe - wtedy barkowe. Brak dysz = bez płomieni (nie wolno rzucić
                // wyjątkiem: przerwałby aktualizację gracza i postać zastygała w powietrzu).
                ParticleSystem left = ___leftHipRcs != null ? ___leftHipRcs : ___leftShoulderRcs;
                ParticleSystem right = ___rightHipRcs != null ? ___rightHipRcs : ___rightShoulderRcs;
                Flame(left, __0.Rotation);
                Flame(right, __0.Rotation);
                return false;
            }
            if (Flying.Remove(__instance))
            {
                audio?.StopAudioEvent3D("Event:/Players/JetpackThruster", __instance.gameObject);
                ___leftHipRcs?.Stop();
                ___rightHipRcs?.Stop();
            }
            return true;
        }

        private static void Flame(ParticleSystem vfx, Quaternion rotation)
        {
            if (vfx == null)
            {
                return;
            }
            vfx.transform.rotation = rotation * Quaternion.LookRotation(Vector3.down);
            if (!vfx.isPlaying)
            {
                vfx.Play();
            }
        }

        // Plecak między dyszami na plecach (dysze barkowe i biodrowe kostiumu wyznaczają plecy).
        private static void AttachModel(PlayerJetpackEffects effects, Quaternion bodyRotation, params ParticleSystem[] nozzles)
        {
            try
            {
                Transform[] points = nozzles.Where(n => n != null).Select(n => n.transform).ToArray();
                if (points.Length == 0)
                {
                    return;
                }
                if (_mesh == null)
                {
                    _mesh = ModAssets.LoadMesh("jetpack.bin");
                    Material template = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.shader != null && m.shader.name == "Custom/PlanetObject" && m.HasProperty("_Albedo"));
                    _material = template != null ? PartFactory.CreateMaterial(template, ModAssets.LoadTexture("reactor_albedo.png"), "Jetpack") : null;
                }
                if (_material == null)
                {
                    return;
                }
                // Jak plecak: przypięty do samej postaci (obiekt, który gra obraca razem z graczem), a nie do ruchomej
                // kości z dyszami - inaczej przekrzywiał się i "wjeżdżał" pod szyję.
                Transform root = effects.GetComponentInParent<Players.Visuals.PlayerSounds>()?.transform ?? effects.transform;
                Vector3[] local = points.Select(t => root.InverseTransformPoint(t.position)).ToArray();
                bool hasHips = nozzles.Length >= 4 && nozzles[2] != null && nozzles[3] != null;
                float y = local.Average(v => v.y) - (hasHips ? 0f : 0.24f); // same dysze barkowe = środek pleców niżej
                float z = local.Average(v => v.z);
                // plecy = strona dysz (zwykle -Z postaci); model: plecy gracza = +Z modelu
                float backSign = z <= 0f ? -1f : 1f;
                var go = new GameObject("MiningJetpack") { layer = effects.gameObject.layer };
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(0f, y, z + backSign * 0.03f);
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, backSign), Vector3.up);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Model jetpacka: {e.Message}");
            }
        }
    }
}
