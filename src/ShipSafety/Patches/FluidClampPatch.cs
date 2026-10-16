using System;
using HarmonyLib;
using MelonLoader;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using UnityEngine;

namespace ShipSafety.Patches
{
    internal static class FluidClamp
    {
        internal static readonly Type PartContainer = AccessTools.Inner(typeof(FluidGroupServerState), "PartFluidContainer");
        internal static readonly Type CableContainer = AccessTools.Inner(typeof(FluidGroupServerState), "CableFluidContainer");

        private static float _nextLogTime;

        internal static bool TryFix(object container, string kind, float quantity, float max, out float fixedQuantity)
        {
            fixedQuantity = quantity;
            if (!float.IsNaN(quantity) && quantity >= 0f && quantity <= max)
            {
                return false;
            }
            fixedQuantity = float.IsNaN(quantity) ? 0f : Mathf.Clamp(quantity, 0f, Mathf.Max(max, 0f));
            if (Time.realtimeSinceStartup >= _nextLogTime && Mathf.Abs(quantity - fixedQuantity) > 0.01f)
            {
                _nextLogTime = Time.realtimeSinceStartup + 1f;
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Fuel error in {kind} {container}: quantity {quantity:0.###} " +
                    $"(capacity {max:0.###})" + (ShipSafetyMod.Enabled.Value ? $" -> {fixedQuantity:0.###}" : ""));
            }
            return ShipSafetyMod.Enabled.Value;
        }
    }

    [HarmonyPatch]
    internal static class PartFluidClampPatch
    {
        private static readonly AccessTools.FieldRef<object, float> QuantityRef = AccessTools.FieldRefAccess<float>(FluidClamp.PartContainer, "_fluidQuantity");
        private static readonly AccessTools.FieldRef<object, StatefulPart> PartRef = AccessTools.FieldRefAccess<StatefulPart>(FluidClamp.PartContainer, "_part");
        private static readonly Func<object, float> MaxFluid = CreateMaxFluid();

        private static Func<object, float> CreateMaxFluid()
        {
            System.Reflection.MethodInfo getter = AccessTools.PropertyGetter(FluidClamp.PartContainer, "MaxFluid");
            if (getter == null || getter.ReturnType != typeof(float))
            {
                return null;
            }
            try
            {
                var instance = System.Linq.Expressions.Expression.Parameter(typeof(object));
                var call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Convert(instance, FluidClamp.PartContainer), getter);
                return System.Linq.Expressions.Expression.Lambda<Func<object, float>>(call, instance).Compile();
            }
            catch (Exception)
            {
                return target => (float)getter.Invoke(target, null);
            }
        }

        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(FluidClamp.PartContainer, "CommitChanges");

        private static void Prefix(object __instance)
        {
            ref float quantity = ref QuantityRef(__instance);
            if (!(PartRef(__instance)?.State is IStatefulPartWithFluidContainer state))
            {
                return;
            }
            float capacity = state.FluidCapacity;
            if (MaxFluid != null)
            {
                capacity = Mathf.Max(capacity, MaxFluid(__instance));
            }
            if (FluidClamp.TryFix(__instance, "part", quantity, capacity, out float fixedQuantity))
            {
                quantity = fixedQuantity;
            }
        }
    }

    [HarmonyPatch]
    internal static class CableFluidClampPatch
    {
        private static readonly AccessTools.FieldRef<object, float> QuantityRef = AccessTools.FieldRefAccess<float>(FluidClamp.CableContainer, "<FluidQuantity>k__BackingField");
        private static readonly AccessTools.FieldRef<object, float> MaxRef = AccessTools.FieldRefAccess<float>(FluidClamp.CableContainer, "<MaxFluid>k__BackingField");

        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(FluidClamp.CableContainer, "CommitChanges");

        private static void Prefix(object __instance)
        {
            ref float quantity = ref QuantityRef(__instance);
            if (FluidClamp.TryFix(__instance, "pipe", quantity, MaxRef(__instance), out float fixedQuantity))
            {
                quantity = fixedQuantity;
            }
        }
    }
}
