using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model.Parts;
using Ships.Physics.Collision;
using UnityEngine;

namespace Mining.Patches
{
    internal sealed class BarrierCollider : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(ShipStatefulColliders), "GenerateStatefulCollider")]
    internal static class StairsColliderPatch
    {
        private static void Postfix(StatefulPart part, Transform __result)
        {
            if (part?.Settings == null || __result == null)
            {
                return;
            }
            if (part.Settings.id == Barrier.PartId && __result.GetComponent<BoxCollider>() is BoxCollider wall)
            {
                wall.center = new Vector3(0f, 0.6f, 0f);
                wall.size = new Vector3(Barrier.HalfLength * 2f, 1.2f, 0.1f);
                __result.gameObject.AddComponent<BarrierCollider>();
                return;
            }
            if (part.Settings.id != Stairs.PartId)
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
            for (int side = 0; side < 2; side++)
            {
                MeshCollider rail = __result.gameObject.AddComponent<MeshCollider>();
                rail.sharedMesh = Stairs.RailColliderMesh(side);
                rail.convex = true;
                rail.sharedMaterial = material;
            }
            __result.gameObject.AddComponent<StairsCollider>();
        }
    }
}
