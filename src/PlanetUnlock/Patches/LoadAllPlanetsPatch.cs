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
    // Demo w PlanetsTracker.LoadPlanets ma na sztywno "if (planet.Id == 4)" - wczytuje tylko Otrera b.
    // Transpiler podmienia w tej metodzie odczyt planet.Id na funkcję, która zwraca 4 dla każdej planety,
    // więc warunek przepuszcza wszystkie planety ze sceny.
    [HarmonyPatch]
    internal static class LoadAllPlanetsPatch
    {
        private const ulong DemoPlanetId = 4;
        private static readonly MethodInfo GetId = AccessTools.PropertyGetter(typeof(Planet), nameof(Planet.Id));
        private static readonly MethodInfo IdForDemoCheck = AccessTools.Method(typeof(LoadAllPlanetsPatch), nameof(PlanetIdForDemoCheck));

        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Klasa jest generyczna z typem referencyjnym, więc Mono współdzieli kod metody między wersją
            // serwera i klienta - patch jednej obejmuje obie (w logu Postfix pokaże obie klasy).
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
                    Melon<PlanetUnlockMod>.Logger.Msg($"Włączam wyłączoną planetę '{planet.name}' (id {planet.Id})");
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
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call) && instruction.operand is MethodInfo method && method == GetId)
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, IdForDemoCheck).WithLabels(instruction.labels);
                    continue;
                }
                yield return instruction;
            }
            Melon<PlanetUnlockMod>.Logger.Msg($"Podmieniono {replaced} sprawdzeń planety demo w LoadPlanets");
        }

        private static void Postfix(object __instance)
        {
            var planets = Traverse.Create(__instance).Field("_planets").GetValue() as System.Collections.IDictionary;
            Melon<PlanetUnlockMod>.Logger.Msg($"{__instance.GetType().Name}: wczytane planety: {planets?.Count}");
            foreach (Planet planet in Object.FindObjectsOfType<Planet>(true))
            {
                Melon<PlanetUnlockMod>.Logger.Msg($"  '{planet.name}' id {planet.Id}, promień {planet.Radius / 1000f:0} km, " +
                                                  $"grawitacja {planet.PhysicalProperties.gravityInG:0.##} g, aktywna {planet.gameObject.activeInHierarchy}");
            }
        }
    }
}
