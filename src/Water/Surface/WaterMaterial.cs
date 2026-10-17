using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Surface
{
    internal static class WaterMaterial
    {
        internal static readonly Color Shallow = new Color(0.24f, 0.69f, 0.71f, 0.92f);
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

        internal static Material CreateSimple(Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WaterInside" };
            MakeTransparent(m);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.9f);
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
                    Melon<WaterMod>.Logger.Warning($"Water shader not supported: {shader?.name}");
                    return null;
                }
                Texture2D[] textures = _bundle.LoadAllAssets<Texture2D>();
                Texture2D Tex(string name) => textures.FirstOrDefault(t => t.name == name);
                var m = new Material(shader) { name = "StylizedWater" };
                m.SetTexture("_CausticsTex", Tex("water_caustics"));
                m.SetTexture("_FoamTex", Tex("water_foam"));
                m.SetTexture("_NormalTex", Tex("water_normal"));
                m.SetColor("_ShallowColor", new Color(0.42f, 0.78f, 0.76f, 0.86f));
                m.SetColor("_MidColor", new Color(0.2f, 0.58f, 0.68f, 0.94f));
                m.SetColor("_DeepColor", new Color(0.1f, 0.38f, 0.55f, 1f));
                m.SetFloat("_MidDepth", 3.5f);
                m.SetFloat("_DeepDepth", 14f);
                m.SetFloat("_CausticsStrength", 0.45f);
                m.SetColor("_FresnelColor", new Color(0.66f, 0.86f, 0.93f, 1f));
                Melon<WaterMod>.Logger.Msg($"Water: shader {shader.name} from the bundle, textures: {string.Join(", ", textures.Select(t => t.name))}");
                return m;
            }
            catch (System.Exception e)
            {
                Melon<WaterMod>.Logger.Warning($"Could not load the water shader: {e.Message}");
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
