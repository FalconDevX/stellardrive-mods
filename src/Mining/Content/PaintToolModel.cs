using System;
using System.Linq;
using Items.Model;
using MelonLoader;
using UnityEngine;

namespace Mining.Content
{
    internal static class PaintToolModel
    {
        private const string BodyName = "PaintTool";
        private static bool _done;

        internal static void Apply(ItemSettings[] items)
        {
            if (_done || items == null)
            {
                return;
            }
            GameObject prefab = items.FirstOrDefault(i => i != null && i.id == Blueprints.BlueprintToolItem.PaintToolItemId)?.toolSettings?.prefab;
            if (prefab == null)
            {
                return;
            }
            _done = true;
            try
            {
                MeshRenderer body = prefab.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == BodyName)
                                    ?? throw new Exception("paint tool has no body renderer");
                MeshFilter filter = body.GetComponent<MeshFilter>() ?? throw new Exception("paint tool body has no mesh");
                var material = new Material(body.sharedMaterial) { name = "PaintToolDetailed" };
                Texture2D palette = ModAssets.LoadTexture("reactor_palette.png");
                foreach (string property in new[] { "_Albedo", "_BaseMap", "_MainTex" })
                {
                    if (material.HasProperty(property)) material.SetTexture(property, palette);
                }
                foreach (string property in new[] { "_Normal", "_RoughnessMap" })
                {
                    if (material.HasProperty(property)) material.SetTexture(property, null);
                }
                if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                filter.sharedMesh = ModAssets.LoadMesh("paint_tool.bin");
                body.sharedMaterial = material;
                Melon<MiningMod>.Logger.Msg("Paint Tool: detailed model");
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Paint tool model: {e}");
            }
        }
    }
}
