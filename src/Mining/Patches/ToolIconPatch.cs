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

        private static readonly Color[] Rainbow = { new Color(0.92f, 0.22f, 0.2f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.88f, 0.2f), new Color(0.3f, 0.8f, 0.3f), new Color(0.2f, 0.55f, 1f), new Color(0.65f, 0.3f, 0.9f) };
        private static readonly Color Outline = new Color(0.12f, 0.12f, 0.14f), Metal = new Color(0.72f, 0.74f, 0.78f), Dark = new Color(0.2f, 0.22f, 0.26f), Fitting = new Color(0.35f, 0.36f, 0.4f), Accent = new Color(0.95f, 0.55f, 0.12f);
        private const float IconScale = 0.86f;

        internal static Texture2D PaintIcon(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PaintToolIcon", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float aa = 1.5f / size / IconScale;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(((x + 0.5f) / size - 0.5f) / IconScale + 0.53f, ((y + 0.5f) / size - 0.5f) / IconScale + 0.5f);
                    Color c = Color.clear;
                    Paint(ref c, Segment(p, new Vector2(0.4f, 0.46f), new Vector2(0.31f, 0.14f), 0.075f), Dark, Outline, aa);
                    Paint(ref c, Segment(p, new Vector2(0.52f, 0.44f), new Vector2(0.49f, 0.33f), 0.022f), Fitting, Outline, aa);
                    Paint(ref c, RoundBox(p, new Vector2(0.5f, 0.53f), new Vector2(0.3f, 0.085f), 0.06f), Metal, Outline, aa);
                    Paint(ref c, RoundBox(p, new Vector2(0.5f, 0.5f), new Vector2(0.26f, 0.018f), 0.01f), Accent, Accent, aa);
                    Paint(ref c, RoundBox(p, new Vector2(0.86f, 0.55f), new Vector2(0.07f, 0.04f), 0.015f), Fitting, Outline, aa);
                    Color band = Rainbow[Mathf.Clamp((int)((p.x - 0.35f) / 0.3f * 6f), 0, 5)];
                    Paint(ref c, RoundBox(p, new Vector2(0.5f, 0.8f), new Vector2(0.15f, 0.16f), 0.06f), band, Outline, aa);
                    Paint(ref c, RoundBox(p, new Vector2(0.5f, 0.955f), new Vector2(0.1f, 0.025f), 0.015f), Fitting, Outline, aa);
                    Paint(ref c, RoundBox(p, new Vector2(0.5f, 0.64f), new Vector2(0.05f, 0.03f), 0f), Fitting, Outline, aa);
                    Paint(ref c, Vector2.Distance(p, new Vector2(0.95f, 0.65f)) - 0.022f, Rainbow[0], Rainbow[0], aa);
                    Paint(ref c, Vector2.Distance(p, new Vector2(0.965f, 0.55f)) - 0.025f, Rainbow[2], Rainbow[2], aa);
                    Paint(ref c, Vector2.Distance(p, new Vector2(0.95f, 0.45f)) - 0.022f, Rainbow[4], Rainbow[4], aa);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D LoadPaintIcon()
        {
            try
            {
                return Content.ModAssets.LoadTexture("paint_tool_icon.png");
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Paint tool icon: {e.Message}");
                return null;
            }
        }

        private static void Paint(ref Color c, float d, Color fill, Color outline, float aa)
        {
            float a = Mathf.Clamp01((aa - d) / (2f * aa));
            if (a <= 0f) return;
            Color col = Color.Lerp(fill, outline, Mathf.Clamp01((d + 0.022f) / 0.008f));
            c = new Color(Mathf.Lerp(c.r, col.r, a), Mathf.Lerp(c.g, col.g, a), Mathf.Lerp(c.b, col.b, a), Mathf.Max(c.a, a));
        }

        private static float RoundBox(Vector2 p, Vector2 center, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * t).magnitude - r;
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
                __result = LoadPaintIcon() ?? PaintIcon(Mathf.Max(64, size));
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
