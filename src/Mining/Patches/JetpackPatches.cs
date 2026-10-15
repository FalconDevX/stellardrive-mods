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

    [HarmonyPatch(typeof(PlayerWalkingPhysics), "ProcessAirForces")]
    internal static class JetpackPhysicsPatch
    {
        private const float HoldWalkSpeed = 4f;
        private const float HoldSprintSpeed = 8f;
        private const float HoldHorizontalAccel = 8f;
        private const float HoldVerticalAccel = 10f;
        private const float HoldDescendSpeed = 3f;

        private static void Postfix(ref PlayerPhysicsState __0, PlayerControlInput __1, Vector3 ____currentUp, Vector3 ____currentGravity, float ____deltaTime)
        {
            bool server = Jetpack.CurrentServerPlayer != null;
            if (!server && !Jetpack.CurrentIsLocalPrediction)
            {
                return;
            }
            if (Jetpack.CurrentIsLocalPrediction)
            {
                Jetpack.LocalHasGravity = ____currentGravity != Vector3.zero;
            }
            if (____currentGravity == Vector3.zero)
            {
                return;
            }
            bool hold = Jetpack.IsHoldInput(__1.RollInput);
            bool up = __1.VerticalInput > 0f || (Jetpack.TestForceUp && server);
            if ((!hold && !up) || !Jetpack.IsUnlocked() || !Jetpack.HasFuel())
            {
                return;
            }
            if (hold)
            {
                HoldAssist(ref __0, __1, up, ____currentUp, ____currentGravity, ____deltaTime);
            }
            if (up)
            {
                float upSpeed = Vector3.Dot(__0.Velocity, ____currentUp);
                float maxUp = MiningMod.JetpackMaxSpeed.Value;
                if (upSpeed < maxUp)
                {
                    float accel = ____currentGravity.magnitude + MiningMod.JetpackAcceleration.Value;
                    __0.Velocity += ____currentUp * Mathf.Min(accel * ____deltaTime, maxUp - upSpeed + ____currentGravity.magnitude * ____deltaTime);
                }
            }
            Jetpack.Burn(____deltaTime);
        }

        private static void HoldAssist(ref PlayerPhysicsState state, PlayerControlInput input, bool up, Vector3 upDir, Vector3 gravity, float deltaTime)
        {
            float vertical = Vector3.Dot(state.Velocity, upDir);
            Vector3 horizontal = state.Velocity - upDir * vertical;
            Vector3 move = Vector3.ClampMagnitude(input.HorizontalMoveInput, 1f);
            Vector3 wish = Vector3.ProjectOnPlane(input.CameraRotation * move, upDir);
            wish = wish.sqrMagnitude > 1e-6f ? wish.normalized * (move.magnitude * (input.IsSprinting ? HoldSprintSpeed : HoldWalkSpeed)) : Vector3.zero;
            horizontal = Vector3.MoveTowards(horizontal, wish, HoldHorizontalAccel * deltaTime);
            if (!up)
            {
                vertical -= Vector3.Dot(gravity, upDir) * deltaTime;
                float target = input.VerticalInput < 0f ? -HoldDescendSpeed : 0f;
                vertical = Mathf.MoveTowards(vertical, target, HoldVerticalAccel * deltaTime);
            }
            state.Velocity = horizontal + upDir * vertical;
        }
    }

    [HarmonyPatch(typeof(PlayerPhysicsCommon), nameof(PlayerPhysicsCommon.CalculateFloatingBodyRotation))]
    internal static class JetpackRollStripPatch
    {
        private static void Prefix(ref float __2) => __2 = Jetpack.StripHold(__2);
    }

    [HarmonyPatch(typeof(TrackedPlayerLocalClient), "SetInputs")]
    internal static class JetpackToggleInputPatch
    {
        private static TrackedPlayerLocalClient _player;

        private static void Prefix(TrackedPlayerLocalClient __instance, ref PlayerControlInput __0)
        {
            Jetpack.LocalHoldActive = false;
            if (!Jetpack.IsUnlocked())
            {
                return;
            }
            if (_player != __instance)
            {
                _player = __instance;
                Jetpack.LocalState = Traverse.Create(__instance).Property("NetworkState").GetValue<PlayerNetworkState>();
            }
            bool grounded = __instance.PhysicsState.IsOnGround;
            if (Jetpack.Hold && !grounded)
            {
                Jetpack.HoldAirborne = true;
            }
            if (Jetpack.Hold && Jetpack.HoldAirborne && grounded && Jetpack.LocalState != null && Jetpack.LocalState.ClientState.Type == PlayerStateType.Walking)
            {
                Jetpack.Hold = false;
                Jetpack.HoldAirborne = false;
                MiningMod.ShowMessage("Jetpack hold OFF (landed)", 1.5f);
            }
            if (!Jetpack.Enabled && __0.MoveInput.y > 0f && Jetpack.LocalHasGravity && !grounded)
            {
                __0.MoveInput.y = 0f;
            }
            if (Jetpack.Hold && Jetpack.Enabled && Jetpack.LocalHasGravity)
            {
                __0.RollInput = Jetpack.EncodeHold(__0.RollInput);
                Jetpack.LocalHoldActive = !grounded;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerNetworkState), nameof(PlayerNetworkState.ClientSetKeyPresses))]
    internal static class JetpackHoldKeysPatch
    {
        private static void Prefix(PlayerNetworkState __instance, ref PlayerPressedKeysState __0)
        {
            if (Jetpack.LocalHoldActive && ReferenceEquals(__instance, Jetpack.LocalState))
            {
                __0.Up = true;
                __0.Down = true;
            }
        }
    }

    [HarmonyPatch]
    internal static class JetpackAddItemPatch
    {
        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(PlayerInventoryTransactions), "InventoryTransaction"), "TryAddItem");

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
    internal static class JetpackShiftFuelPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.ShiftItem))]
        private static bool Shift(PlayerInventoryTransactions __instance, ItemSlotId __0, PlayerNetworkState ____playerState, IItemSettingsProvider ____items) =>
            !TryFuel(__instance, __0, ____playerState, ____items, false);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerInventoryTransactions.ShiftSplitItem))]
        private static bool ShiftSplit(PlayerInventoryTransactions __instance, ItemSlotId __0, PlayerNetworkState ____playerState, IItemSettingsProvider ____items) =>
            !TryFuel(__instance, __0, ____playerState, ____items, true);

        private static bool TryFuel(PlayerInventoryTransactions inventory, ItemSlotId source, PlayerNetworkState state, IItemSettingsProvider items, bool split)
        {
            try
            {
                if (!Jetpack.IsUnlocked() || state == null || state.ServerState.Type == PlayerStateType.InteractingWithChest)
                {
                    return false;
                }
                if (source.Type != InventoryType.MainInventory && source.Type != InventoryType.ToolBar)
                {
                    return false;
                }
                var fuel = new ItemSlotId { Type = InventoryType.MainInventory, SlotId = Jetpack.FuelSlot };
                if (source.Type == fuel.Type && source.SlotId == fuel.SlotId)
                {
                    return false;
                }
                ItemDrop moving = inventory.GetItemAtSlot(source);
                if (moving.Quantity == 0 || moving.ItemId != Jetpack.FuelItem)
                {
                    return false;
                }
                ItemDrop current = inventory.GetItemAtSlot(fuel);
                if (current.Quantity > 0 && (current.ItemId != Jetpack.FuelItem || current.Quantity >= items.GetItemSettingsById(Jetpack.FuelItem).maxStackSize))
                {
                    return false;
                }
                if (split)
                {
                    inventory.MoveSplitItem(source, fuel);
                }
                else
                {
                    inventory.MoveItem(source, fuel);
                }
                return true;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Jetpack shift to fuel slot: {e.Message}");
                return false;
            }
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
                return false;
            }
            if (IsFuelSlot(source) && target.Quantity > 0 && target.ItemId != Jetpack.FuelItem)
            {
                return false;
            }
            return true;
        }
    }

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
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 worldSize = new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));

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
                Melon<MiningMod>.Logger.Warning($"Jetpack fuel slot: {e.Message}");
            }
        }
    }

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
            Trash.GetWorldCorners(_corners);
            Vector3 up = (_corners[1] - _corners[0]).normalized;
            Vector3 topCenter = (_corners[1] + _corners[2]) * 0.5f;
            float trashHeight = Vector3.Distance(_corners[0], _corners[1]);
            rect.GetWorldCorners(_corners);
            float slotHeight = Vector3.Distance(_corners[0], _corners[1]);
            rect.position = topCenter + up * (slotHeight * 0.5f + Mathf.Max(trashHeight * 0.35f, slotHeight * 0.3f));
        }
    }

    [HarmonyPatch(typeof(PlayerJetpackEffects), nameof(PlayerJetpackEffects.UpdateRcsEffects))]
    internal static class JetpackEffectsPatch
    {
        private static bool _loggedError;

        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && !_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Warning($"Jetpack effects: {__exception}");
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
            bool local = ReferenceEquals(____state, Jetpack.LocalState);
            bool wants = unlocked && ____state.PressedKeys.Up && !__0.IsOnGround;
            bool fuel = !wants || !local || HasLocalFuel();
            bool flying = wants && onPlanet && fuel && ____state.ClientState.Type == PlayerStateType.Walking;
            if (wants && local)
            {
                Diagnose(flying ? "flying" : !fuel ? "no wood in the fuel slot" : !onPlanet ? $"not on a planet ({space?.GetType().Name})" :
                    $"player state {____state.ClientState.Type}", !fuel);
            }
            var audio = ServiceLocator.GetService<IAudioPlayer>();
            JetpackFx fx = null;
            if (unlocked)
            {
                fx = JetpackFx.For(__instance);
                fx.Flying = flying;
                fx.Up = __0.Rotation * Vector3.up;
            }
            ParticleSystem left = ___leftHipRcs != null ? ___leftHipRcs : ___leftShoulderRcs;
            ParticleSystem right = ___rightHipRcs != null ? ___rightHipRcs : ___rightShoulderRcs;
            bool ownFlames = fx != null && fx.UpdateNozzles(left != null ? left.transform : null, right != null ? right.transform : null, __0.Rotation);
            if (flying)
            {
                if (Flying.Add(__instance) && !JetpackFx.HasCustomSound)
                {
                    audio?.PlayAudioEvent3D("Event:/Players/JetpackThruster", __instance.gameObject);
                }
                if (ownFlames)
                {
                    if (left != null) left.Stop();
                    if (right != null) right.Stop();
                }
                else
                {
                    Flame(left, __0.Rotation);
                    Flame(right, __0.Rotation);
                }
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

        private static string _lastReason;
        private static float _lastMessage = -100f;

        private static bool HasLocalFuel()
        {
            var inventory = ServiceLocator.GetService<Players.Interface.Services.IPlayerMainInventoryProvider>();
            InventorySlot slot = inventory != null ? inventory.GetSlot(Jetpack.FuelSlot) : default;
            return slot.HasItem && slot.ItemId == Jetpack.FuelItem && slot.Quantity > 0;
        }

        private static void Diagnose(string reason, bool noFuel)
        {
            if (reason != _lastReason)
            {
                _lastReason = reason;
                Melon<MiningMod>.Logger.Msg($"Jetpack: {reason}");
            }
            if (noFuel && Time.time - _lastMessage > 4f)
            {
                _lastMessage = Time.time;
                MiningMod.ShowMessage("Jetpack: no wood", 2f);
            }
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
                    _material = template != null ? PartFactory.CreateMaterial(template, ModAssets.LoadTexture("reactor_parts.png"), "Jetpack") : null;
                }
                if (_material == null)
                {
                    return;
                }
                Transform root = effects.GetComponentInParent<Players.Visuals.PlayerSounds>()?.transform ?? effects.transform;
                Vector3[] local = points.Select(t => root.InverseTransformPoint(t.position)).ToArray();
                bool hasHips = nozzles.Length >= 4 && nozzles[2] != null && nozzles[3] != null;
                float y = local.Average(v => v.y) - (hasHips ? 0f : 0.24f);
                float z = local.Average(v => v.z);
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
                Melon<MiningMod>.Logger.Warning($"Jetpack model: {e.Message}");
            }
        }
    }
}
