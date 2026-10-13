using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Effects
{
    internal static class UnderwaterView
    {
        private static readonly Color Shallow = new Color(0.1f, 0.38f, 0.42f, 0.42f);
        private static readonly Color Deep = new Color(0.02f, 0.12f, 0.16f, 0.78f);
        private const float DeepAt = 10f;

        private static GameObject _quad;
        private static Material _material;
        private static Camera _camera;
        private static bool _underwater;
        private static bool _failed;
        private static float _underSince;

        internal static void Clear()
        {
            if (_quad != null)
            {
                Object.Destroy(_quad);
            }
            _quad = null;
            _camera = null;
            _underwater = false;
        }

        internal static void Update(Camera cam, float depth)
        {
            bool underwater = depth > 0f;
            if (underwater != _underwater)
            {
                _underwater = underwater;
                if (underwater)
                {
                    _underSince = Time.time;
                    LensDroplets.Submerge();
                }
                else if (Time.time - _underSince > 0.7f)
                {
                    LensDroplets.Emerge();
                }
            }
            if (!underwater || !WaterMod.UnderwaterTint.Value)
            {
                if (_quad != null) _quad.SetActive(false);
                return;
            }
            if (!Ensure(cam))
            {
                return;
            }
            _quad.SetActive(true);
            Color c = Color.Lerp(Shallow, Deep, Mathf.Clamp01(depth / DeepAt));
            float exposure = Surface.WaterSurface.Exposure;
            _material.SetColor("_BaseColor", new Color(c.r * exposure, c.g * exposure, c.b * exposure, c.a));
            float distance = cam.nearClipPlane * 1.5f;
            float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
            _quad.transform.localPosition = new Vector3(0f, 0f, distance);
            _quad.transform.localRotation = Quaternion.identity;
            _quad.transform.localScale = new Vector3(height * cam.aspect, height, 1f);
        }

        private static bool Ensure(Camera cam)
        {
            if (_failed)
            {
                return false;
            }
            if (_quad != null && _camera == cam)
            {
                return true;
            }
            if (_quad != null)
            {
                Object.Destroy(_quad);
            }
            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    _failed = true;
                    MelonLoader.Melon<WaterMod>.Logger.Warning("Underwater tint: URP Unlit shader not found");
                    return false;
                }
                _material = new Material(shader) { name = "UnderwaterTint" };
                _material.SetTexture("_BaseMap", Gradient());
                _material.SetFloat("_Surface", 1f);
                _material.SetFloat("_ZWrite", 0f);
                _material.SetFloat("_ZTest", (float)CompareFunction.Always);
                _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _material.SetFloat("_Cull", 0f);
                _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _material.SetOverrideTag("RenderType", "Transparent");
                _material.renderQueue = 3900;
            }
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "UnderwaterTint";
            Object.Destroy(_quad.GetComponent<Collider>());
            var renderer = _quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _quad.transform.SetParent(cam.transform, false);
            _camera = cam;
            return true;
        }

        private static Texture2D Gradient()
        {
            const int size = 64;
            var tex = new Texture2D(1, size, TextureFormat.RGBA32, false) { name = "UnderwaterGradient", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                float t = y / (size - 1f);
                float light = Mathf.Lerp(0.7f, 1.35f, t);
                tex.SetPixel(0, y, new Color(Mathf.Min(1f, light), Mathf.Min(1f, light), Mathf.Min(1f, light), Mathf.Lerp(1f, 0.85f, t)));
            }
            tex.Apply();
            return tex;
        }
    }
}
