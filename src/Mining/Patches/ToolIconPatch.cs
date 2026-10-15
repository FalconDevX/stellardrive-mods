using System;
using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using Items.Services;
using MelonLoader;
using Rendering.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ItemThumbnailTextureCache), nameof(ItemThumbnailTextureCache.GetThumbnail))]
    internal static class ToolIconPatch
    {
        private static readonly HashSet<uint> Angled = new HashSet<uint> { Blueprints.BlueprintToolItem.PaintToolItemId, Blueprints.BlueprintToolItem.ItemId };

        private static readonly Color[] Blobs = { new Color(0.92f, 0.22f, 0.2f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.88f, 0.2f), new Color(0.3f, 0.8f, 0.3f), new Color(0.2f, 0.55f, 1f), new Color(0.65f, 0.3f, 0.9f) };

        internal static Texture2D PaintIcon(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PaintToolIcon", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            var wood = new Color(0.86f, 0.7f, 0.48f);
            var woodEdge = new Color(0.55f, 0.4f, 0.24f);
            var blobCenters = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Lerp(150f, -20f, i / 5f) * Mathf.Deg2Rad;
                blobCenters[i] = new Vector2(0.47f + Mathf.Cos(a) * 0.27f, 0.47f + Mathf.Sin(a) * 0.22f);
            }
            float aa = 1.5f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    Color c = Color.clear;
                    Vector2 q = new Vector2((p.x - 0.47f) / 0.42f, (p.y - 0.47f) / 0.34f);
                    float palette = (q.magnitude - 1f) * 0.36f;
                    float hole = 0.07f - Vector2.Distance(p, new Vector2(0.3f, 0.36f));
                    float notch = 0.1f - Vector2.Distance(p, new Vector2(0.2f, 0.22f));
                    float shape = Mathf.Max(palette, Mathf.Max(hole, notch));
                    if (shape < aa)
                    {
                        Color body = shape > -0.025f ? Color.Lerp(woodEdge, wood, Mathf.InverseLerp(-0.006f, -0.025f, shape) + 0.35f) : wood;
                        for (int i = 0; i < 6; i++)
                        {
                            float d = Vector2.Distance(p, blobCenters[i]) - 0.075f;
                            if (d < 0f) body = Color.Lerp(body, Blobs[i], Mathf.Clamp01(-d / aa));
                        }
                        body.a = Mathf.Clamp01((aa - shape) / (2f * aa));
                        c = body;
                    }
                    Vector2 a0 = new Vector2(0.62f, 0.28f), a1 = new Vector2(0.95f, 0.02f);
                    Vector2 dir = (a1 - a0).normalized;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a0, dir) / Vector2.Distance(a0, a1));
                    float dist = Vector2.Distance(p, a0 + dir * t * Vector2.Distance(a0, a1));
                    float width = Mathf.Lerp(0.012f, 0.03f, t);
                    if (dist < width + aa)
                    {
                        Color brush = t < 0.15f ? new Color(0.2f, 0.55f, 1f) : t < 0.3f ? new Color(0.75f, 0.75f, 0.8f) : new Color(0.45f, 0.28f, 0.15f);
                        brush.a = Mathf.Clamp01((width + aa - dist) / (2f * aa));
                        c = Color.Lerp(c, new Color(brush.r, brush.g, brush.b, 1f), brush.a);
                        c.a = Mathf.Max(c.a, brush.a);
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static bool Prefix(ItemThumbnailTextureCache __instance, uint itemId, Dictionary<uint, Texture2D> ____thumbnails, ObjectThumbnailRenderer ____thumbnailRenderer, ItemSettingsList ___items, ref Texture2D __result)
        {
            if (!Angled.Contains(itemId) || ____thumbnails == null || ____thumbnailRenderer == null)
            {
                return true;
            }
            if (____thumbnails.TryGetValue(itemId, out Texture2D cached))
            {
                __result = cached;
                return false;
            }
            if (itemId == Blueprints.BlueprintToolItem.PaintToolItemId)
            {
                var size = Traverse.Create(__instance).Field("_thumbnailsResolutionProvider").Method("GetItemSmallThumbnailResolution").GetValue<int>();
                __result = PaintIcon(Mathf.Max(64, size));
                ____thumbnails[itemId] = __result;
                return false;
            }
            ItemSettings item = ___items != null ? ___items.GetItemSettingsById(itemId) : null;
            if (item?.toolSettings?.prefab == null)
            {
                return true;
            }
            try
            {
                var resolution = Traverse.Create(__instance).Field("_thumbnailsResolutionProvider").Method("GetItemSmallThumbnailResolution").GetValue<int>();
                Texture2D texture = Traverse.Create(typeof(ItemThumbnailTextureCache)).Method("SetupThumbnailTexture", resolution).GetValue<Texture2D>();
                ____thumbnails[itemId] = texture;
                GameObject view = Object.Instantiate(item.toolSettings.prefab);
                view.layer = LayerMask.NameToLayer("PartThumbnailRender");
                Quaternion rotation = Quaternion.AngleAxis(32f, Vector3.forward) * Quaternion.AngleAxis(-28f, Vector3.up) * Quaternion.LookRotation(Vector3.right, Vector3.up);
                ____thumbnailRenderer.Render(view, rotation, texture);
                __result = texture;
                return false;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Tool icon {itemId}: {e.Message}");
                ____thumbnails.Remove(itemId);
                return true;
            }
        }
    }
}
