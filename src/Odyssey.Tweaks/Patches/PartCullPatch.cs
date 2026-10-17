using System.Collections.Generic;
using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Parts.Common.Model.Reactions;
using Ships.Visuals;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(ShipPartVisuals), nameof(ShipPartVisuals.AddStatefulPart))]
    internal static class PartCullPatch
    {
        private static readonly AccessTools.FieldRef<ShipPartVisuals, Dictionary<ushort, IShipPartVisual>> Parts =
            AccessTools.FieldRefAccess<ShipPartVisuals, Dictionary<ushort, IShipPartVisual>>("_floatingParts");
        private static readonly List<Renderer> Found = new List<Renderer>();
        private static readonly List<Renderer> Kept = new List<Renderer>();

        private static void Postfix(ShipPartVisuals __instance, StatefulPart part)
        {
            float size = TweaksMod.PartCullScreenSize.Value;
            if (size <= 0f || !Parts(__instance).TryGetValue(part.Id, out IShipPartVisual visual) || visual?.GameObject == null)
            {
                return;
            }
            GameObject go = visual.GameObject;
            if (go.GetComponentInChildren<LODGroup>(true) != null)
            {
                return;
            }
            go.GetComponentsInChildren(true, Found);
            Kept.Clear();
            foreach (Renderer r in Found)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer) Kept.Add(r);
            }
            Found.Clear();
            if (Kept.Count == 0)
            {
                return;
            }
            LODGroup group = go.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(size, Kept.ToArray()) });
            group.RecalculateBounds();
            Kept.Clear();
        }
    }
}
