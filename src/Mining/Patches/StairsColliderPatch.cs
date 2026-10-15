using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model.Parts;
using Ships.Physics.Collision;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ShipStatefulColliders), "GenerateStatefulCollider")]
    internal static class StairsColliderPatch
    {
        private static void Postfix(StatefulPart part, Transform __result)
        {
            if (part?.Settings == null || part.Settings.id != Stairs.PartId || __result == null)
            {
                return;
            }
            BoxCollider box = __result.GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }
            PhysicsMaterial material = box.sharedMaterial;
            Object.DestroyImmediate(box);
            MeshCollider ramp = __result.gameObject.AddComponent<MeshCollider>();
            ramp.sharedMesh = Stairs.ColliderMesh();
            ramp.convex = true;
            ramp.sharedMaterial = material;
        }
    }
}
