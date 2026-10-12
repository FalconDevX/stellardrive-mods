using System;
using System.Reflection;
using HarmonyLib;
using Mining.Content;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Parts.Common.Gauge;
using Ships.Parts.FuelTank;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch]
    internal static class BigFluidTankCapacityPatch
    {
        private static FieldInfo _partField;

        private static MethodBase TargetMethod()
        {
            Type container = AccessTools.Inner(typeof(FluidGroupServerState), "PartFluidContainer");
            _partField = AccessTools.Field(container, "_part");
            return AccessTools.PropertyGetter(container, "MaxFluid");
        }

        private static void Postfix(object __instance, ref float __result)
        {
            if (_partField.GetValue(__instance) is StatefulPart part && BigFluidTank.Is(part.Settings))
            {
                __result = BigFluidTank.Capacity;
            }
        }
    }

    [HarmonyPatch(typeof(FluidTankVisuals), "UpdateState")]
    internal static class BigFluidTankVisualsPatch
    {
        private static void Postfix(FluidTankVisuals __instance, FluidTankState state, Gauge ___gauge, ref float ____targetDensity)
        {
            if (__instance.GetComponent<BigFluidTankMarker>() == null)
            {
                return;
            }
            ___gauge.UpdateGauge(state.FluidQuantity, BigFluidTank.Capacity);
            if (state.FluidQuantity > 0f)
            {
                ____targetDensity = 0.1f + Mathf.Clamp01(state.FluidQuantity / BigFluidTank.Capacity) / 0.9f;
            }
        }
    }
}
