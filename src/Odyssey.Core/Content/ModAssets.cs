using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.Content
{
    internal static class ModAssets
    {
        internal static byte[] Read(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Odyssey."))
                {
                    continue;
                }
                using (Stream s = asm.GetManifestResourceStream("Odyssey.Assets." + name))
                {
                    if (s == null)
                    {
                        continue;
                    }
                    var ms = new MemoryStream();
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

        internal static readonly Vector2 PaletteUv = new Vector2(0.11f, 0.02f);

        internal static Mesh LoadMesh(string name)
        {
            byte[] data = Read(name) ?? throw new FileNotFoundException(name);
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (new string(r.ReadChars(4)) != "MSH1")
                {
                    throw new InvalidDataException(name);
                }
                int vertCount = r.ReadInt32();
                int indexCount = r.ReadInt32();
                var pos = new Vector3[vertCount];
                var nrm = new Vector3[vertCount];
                var uv = new Vector2[vertCount];
                for (int i = 0; i < vertCount; i++)
                {
                    pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                }
                var idx = new int[indexCount];
                for (int i = 0; i < indexCount; i++)
                {
                    idx[i] = r.ReadInt32();
                }
                var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(name), indexFormat = vertCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = pos;
                mesh.normals = nrm;
                mesh.uv = uv;
                var palette = new Vector2[vertCount];
                for (int i = 0; i < vertCount; i++) palette[i] = PaletteUv;
                mesh.uv2 = palette;
                mesh.triangles = idx;
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }

        internal static Texture2D LoadTexture(string name, bool linear = false)
        {
            byte[] data = Read(name) ?? throw new FileNotFoundException(name);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = Path.GetFileNameWithoutExtension(name) };
            var loadImage = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            if (loadImage == null || !(bool)loadImage.Invoke(null, new object[] { tex, data, false }))
            {
                throw new InvalidDataException(name);
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;
            tex.Apply(true, true);
            return tex;
        }
    }
}
