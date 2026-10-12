using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Surface
{
    // Materiał tafli: własny shader StellarDriveMods/StylizedWater z paczki water.bundle (zbudowanej w Unity 6000.3.24f1,
    // tej samej wersji co gra - UnityProject w tym modzie). Gdy paczki nie da się wczytać - przezroczysty URP/Lit.
    internal static class WaterMaterial
    {
        internal static readonly Color Shallow = new Color(0.24f, 0.69f, 0.71f, 0.55f);
        internal static bool Stylized { get; private set; }
        private static AssetBundle _bundle;

        internal static Material Create()
        {
            Material stylized = TryStylized();
            if (stylized != null)
            {
                Stylized = true;
                return stylized;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = "Water" };
            MakeTransparent(m);
            m.SetColor("_BaseColor", Shallow);
            m.SetFloat("_Smoothness", 0.85f);
            return m;
        }

        private static Material TryStylized()
        {
            try
            {
                if (_bundle == null)
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Water.water.bundle"))
                    {
                        if (s == null) return null;
                        var data = new byte[s.Length];
                        s.Read(data, 0, data.Length);
                        _bundle = AssetBundle.LoadFromMemory(data);
                    }
                }
                Shader shader = _bundle.LoadAllAssets<Shader>().FirstOrDefault();
                if (shader == null || !shader.isSupported)
                {
                    Melon<WaterMod>.Logger.Warning($"Shader wody nieobsługiwany: {shader?.name}");
                    return null;
                }
                Texture2D[] textures = _bundle.LoadAllAssets<Texture2D>();
                Texture2D Tex(string name) => textures.FirstOrDefault(t => t.name == name);
                var m = new Material(shader) { name = "StylizedWater" };
                m.SetTexture("_CausticsTex", Tex("water_caustics"));
                m.SetTexture("_FoamTex", Tex("water_foam"));
                m.SetTexture("_NormalTex", Tex("water_normal"));
                Melon<WaterMod>.Logger.Msg($"Woda: shader {shader.name} z paczki, tekstury: {string.Join(", ", textures.Select(t => t.name))}");
                return m;
            }
            catch (System.Exception e)
            {
                Melon<WaterMod>.Logger.Warning($"Nie udało się wczytać shadera wody: {e.Message}");
                return null;
            }
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
    }
}
