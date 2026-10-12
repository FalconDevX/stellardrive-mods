using HarmonyLib;
using Ships.Physics;
using UnityEngine;

namespace ShipSafety.Patches
{
    // Gracz na pokładzie: gra dokłada do fizyki statku kapsułę w miejscu gracza (żeby statek "czuł" gracza).
    // Gdy statek stoi w hangarze innego statku, a gracz chodzi obok niego (np. ciągnąc z niego rurę), ta kapsuła
    // wchodzi w podłogę hangaru i podbija mały statek - skacze. Kapsuła nie zderza się więc z innymi statkami
    // (warstwa Ship); teren i obiekty działają jak dotąd.
    [HarmonyPatch(typeof(ShipServerPhysics), nameof(ShipServerPhysics.CreatePlayerCollider))]
    internal static class PlayerColliderPatch
    {
        private static void Postfix(GameObject __result)
        {
            if (!ShipSafetyMod.Enabled.Value || __result == null)
            {
                return;
            }
            int ship = LayerMask.GetMask("Ship");
            foreach (Collider collider in __result.GetComponentsInChildren<Collider>(true))
            {
                collider.excludeLayers |= ship;
            }
        }
    }
}
