using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Ships.Cables.Fluids;
using UnityEngine;

namespace ShipSafety.Patches
{
    [HarmonyPatch]
    internal static class FluidClampPatch
    {
        private static readonly Type PartContainer = AccessTools.Inner(typeof(FluidGroupServerState), "PartFluidContainer");
        private static readonly Type CableContainer = AccessTools.Inner(typeof(FluidGroupServerState), "CableFluidContainer");

        private static float _nextLogTime;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(PartContainer, "CommitChanges");
            yield return AccessTools.Method(CableContainer, "CommitChanges");
        }

        private static void Prefix(object __instance)
        {
            var container = Traverse.Create(__instance);
            float quantity = container.Property<float>("FluidQuantity").Value;
            float max = container.Property<float>("MaxFluid").Value;
            if (!float.IsNaN(quantity) && quantity >= 0f && quantity <= max)
            {
                return;
            }

            float fixedQuantity = float.IsNaN(quantity) ? 0f : Mathf.Clamp(quantity, 0f, Mathf.Max(max, 0f));
            if (Time.realtimeSinceStartup >= _nextLogTime && Mathf.Abs(quantity - fixedQuantity) > 0.01f)
            {
                _nextLogTime = Time.realtimeSinceStartup + 1f;
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Fuel error in {(__instance.GetType() == PartContainer ? "part" : "pipe")} {__instance}: quantity {quantity:0.###} " +
                    $"(capacity {max:0.###})" + (ShipSafetyMod.Enabled.Value ? $" -> {fixedQuantity:0.###}" : ""));
            }
            if (!ShipSafetyMod.Enabled.Value)
            {
                return;
            }

            if (__instance.GetType() == PartContainer)
            {
                container.Field<float>("_fluidQuantity").Value = fixedQuantity;
            }
            else
            {
                container.Property<float>("FluidQuantity").Value = fixedQuantity;
            }
        }
    }
}
