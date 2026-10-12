using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.Services;
using UnityEngine;

namespace PlanetUnlock.Patches
{
    [HarmonyPatch]
    internal static class LoadAllPlanetsPatch
    {
        private const ulong DemoPlanetId = 4;
        private static readonly MethodInfo GetId = AccessTools.PropertyGetter(typeof(Planet), nameof(Planet.Id));
        private static readonly MethodInfo IdForDemoCheck = AccessTools.Method(typeof(LoadAllPlanetsPatch), nameof(PlanetIdForDemoCheck));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlanetsServerTracker).BaseType, "LoadPlanets");
        }

        private static ulong PlanetIdForDemoCheck(Planet planet)
        {
            return PlanetUnlockMod.Enabled.Value ? DemoPlanetId : planet.Id;
        }

        private static void Prefix()
        {
            if (!PlanetUnlockMod.Enabled.Value || !PlanetUnlockMod.ActivateInactivePlanets.Value)
            {
                return;
            }
            foreach (Planet planet in Object.FindObjectsOfType<Planet>(true))
            {
                if (!planet.gameObject.activeInHierarchy)
                {
                    Melon<PlanetUnlockMod>.Logger.Msg($"Enabling disabled planet '{planet.name}' (id {planet.Id})");
                    Transform t = planet.transform;
                    while (t != null)
                    {
                        t.gameObject.SetActive(true);
                        t = t.parent;
                    }
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 0; i < code.Count - 1; i++)
            {
                CodeInstruction instruction = code[i];
                bool isGetId = (instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call) &&
                               instruction.operand is MethodInfo method && method == GetId;
                if (isGetId && IsLoadDemoId(code, i + 1))
                {
                    replaced++;
                    code[i] = new CodeInstruction(OpCodes.Call, IdForDemoCheck).WithLabels(instruction.labels);
                }
            }
            Melon<PlanetUnlockMod>.Logger.Msg($"Replaced {replaced} demo planet checks in LoadPlanets");
            return code;
        }

        private static bool IsLoadDemoId(List<CodeInstruction> code, int index)
        {
            CodeInstruction next = code[index];
            if (next.opcode == OpCodes.Ldc_I8 && next.operand is long value)
            {
                return value == (long)DemoPlanetId;
            }
            bool loadsFour = next.opcode == OpCodes.Ldc_I4_4 ||
                             (next.opcode == OpCodes.Ldc_I4_S && System.Convert.ToInt32(next.operand) == 4) ||
                             (next.opcode == OpCodes.Ldc_I4 && System.Convert.ToInt32(next.operand) == 4);
            return loadsFour && index + 1 < code.Count &&
                   (code[index + 1].opcode == OpCodes.Conv_I8 || code[index + 1].opcode == OpCodes.Conv_U8);
        }

        private static void Postfix(object __instance)
        {
            var planets = Traverse.Create(__instance).Field("_planets").GetValue() as System.Collections.IDictionary;
            Melon<PlanetUnlockMod>.Logger.Msg($"{__instance.GetType().Name}: loaded planets: {planets?.Count}");
            foreach (Planet planet in Object.FindObjectsOfType<Planet>(true))
            {
                Melon<PlanetUnlockMod>.Logger.Msg($"  '{planet.name}' id {planet.Id}, radius {planet.Radius / 1000f:0} km, " +
                                                  $"gravity {planet.PhysicalProperties.gravityInG:0.##} g, active {planet.gameObject.activeInHierarchy}");
            }
        }
    }
}
