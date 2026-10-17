using System;
using System.Reflection;
using HarmonyLib;
using Odyssey.Content;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Parts.Common.Gauge;
using Ships.Parts.FuelTank;
using UnityEngine;

namespace Odyssey.Patches
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
            if (_partField.GetValue(__instance) is StatefulPart part && BigFluidTank.TryGetCapacity(part.Settings, out float capacity))
            {
                __result = capacity;
            }
        }
    }

    [HarmonyPatch(typeof(FluidTankVisuals), "UpdateState")]
    internal static class BigFluidTankVisualsPatch
    {
        private static void Postfix(FluidTankVisuals __instance, FluidTankState state, Gauge ___gauge, ref float ____targetDensity)
        {
            var marker = __instance.GetComponent<BigFluidTankMarker>();
            if (marker == null)
            {
                return;
            }
            ___gauge.UpdateGauge(state.FluidQuantity, marker.Capacity);
            if (state.FluidQuantity > 0f)
            {
                ____targetDensity = 0.1f + Mathf.Clamp01(state.FluidQuantity / marker.Capacity) / 0.9f;
            }
        }
    }
}
