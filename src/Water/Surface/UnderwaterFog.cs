using UnityEngine;

namespace Water.Surface
{
    internal static class UnderwaterFog
    {
        private static readonly int ColorId = Shader.PropertyToID("_FogColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int CamDepthId = Shader.PropertyToID("_CamDepth");
        private static readonly int UpId = Shader.PropertyToID("_Up");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly Color FogColor = new Color(0.1f, 0.36f, 0.4f, 1f);

        private static GameObject _object;
        private static Material _material;
        private static bool _failed;

        internal static void Clear()
        {
            if (_object != null) Object.Destroy(_object);
            _object = null;
        }

        internal static void Hide()
        {
            if (_object != null && _object.activeSelf) _object.SetActive(false);
        }

        internal static void Show(Camera cam, float depth, Vector3 upVisual, float exposure, float fade)
        {
            float visibility = WaterMod.UnderwaterVisibility.Value;
            if (visibility <= 0f || fade <= 0f || cam == null || !Ensure())
            {
                Hide();
                return;
            }
            if (_object.transform.parent != cam.transform)
            {
                _object.transform.SetParent(cam.transform, false);
                _object.transform.localPosition = Vector3.forward * (cam.nearClipPlane + 0.05f);
                _object.transform.localRotation = Quaternion.identity;
            }
            _material.SetColor(ColorId, FogColor);
            _material.SetFloat(DensityId, 3f / Mathf.Max(visibility, 0.5f) * fade * fade);
            _material.SetFloat(CamDepthId, Mathf.Max(depth, 0f));
            _material.SetVector(UpId, upVisual);
            _material.SetFloat(ExposureId, exposure);
            if (!_object.activeSelf) _object.SetActive(true);
        }

        private static bool Ensure()
        {
            if (_object != null) return true;
            if (_failed) return false;
            if (_material == null) _material = WaterMaterial.CreateFog();
            if (_material == null)
            {
                _failed = true;
                return false;
            }
            var mesh = new Mesh { name = "UnderwaterFog" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            _object = new GameObject("UnderwaterFog");
            Object.DontDestroyOnLoad(_object);
            _object.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _object.SetActive(false);
            return true;
        }
    }
}
