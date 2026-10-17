using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using UnityEngine;

namespace Odyssey.Blueprints
{
    [Serializable]
    internal class BlueprintData
    {
        public string name = "Blueprint";
        public int version = 1;
        public long created;
        public int sizeX, sizeY, sizeZ;
        public List<BlueprintHull> hull = new List<BlueprintHull>();
        public List<BlueprintPart> parts = new List<BlueprintPart>();
        public List<BlueprintCable> cables = new List<BlueprintCable>();

        public int PartCount => hull.Count + parts.Count;
    }

    [Serializable]
    internal class BlueprintHull
    {
        public int s, x, y, z, t, f, u;
        public int fr, fg, fb, br, bg, bb;
    }

    [Serializable]
    internal class BlueprintPart
    {
        public int id, s;
        public bool onHull;
        public float x, y, z;
        public int hx, hy, hz, ht;
        public int f, u;
    }

    [Serializable]
    internal class BlueprintCable
    {
        public int type, a, sa, b, sb;
    }

    internal struct BlueprintTransform
    {
        public int Turns;
        public Vector3Int Offset;

        public BlueprintTransform(int turns, Vector3Int offset)
        {
            Turns = ((turns % 4) + 4) % 4;
            Offset = offset;
        }

        private static readonly PartOrientation[] Orientations = (PartOrientation[])Enum.GetValues(typeof(PartOrientation));

        public Quaternion Rotation => Quaternion.Euler(0f, 90f * Turns, 0f);

        public static Vector3Int RotateDoubled(Vector3Int v, int turns)
        {
            switch (((turns % 4) + 4) % 4)
            {
                case 1: return new Vector3Int(v.z, v.y, -v.x);
                case 2: return new Vector3Int(-v.x, v.y, -v.z);
                case 3: return new Vector3Int(-v.z, v.y, v.x);
                default: return v;
            }
        }

        public HullPartPosition Apply(Vector3Int voxel, VoxelPartType type)
        {
            Vector3Int face = voxel * 2;
            switch (type)
            {
                case VoxelPartType.Floor: face += new Vector3Int(0, -1, 0); break;
                case VoxelPartType.ForwardWall: face += new Vector3Int(0, 0, 1); break;
                case VoxelPartType.RightWall: face += new Vector3Int(1, 0, 0); break;
            }
            Vector3Int r = RotateDoubled(face, Turns) + Offset * 2;
            if ((r.x & 1) != 0) return new HullPartPosition(new Vector3Int((r.x - 1) / 2, r.y / 2, r.z / 2), VoxelPartType.RightWall);
            if ((r.z & 1) != 0) return new HullPartPosition(new Vector3Int(r.x / 2, r.y / 2, (r.z - 1) / 2), VoxelPartType.ForwardWall);
            if ((r.y & 1) != 0) return new HullPartPosition(new Vector3Int(r.x / 2, (r.y + 1) / 2, r.z / 2), VoxelPartType.Floor);
            return new HullPartPosition(new Vector3Int(r.x / 2, r.y / 2, r.z / 2), VoxelPartType.Interior);
        }

        public Vector3 Apply(Vector3 point) => Rotation * point + Offset;

        public PartOrientation Apply(PartOrientation orientation)
        {
            if (Turns == 0)
            {
                return orientation;
            }
            Vector3 target = Rotation * orientation.GetVector3().normalized;
            PartOrientation best = orientation;
            float bestDot = float.MinValue;
            foreach (PartOrientation o in Orientations)
            {
                float dot = Vector3.Dot(o.GetVector3().normalized, target);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = o;
                }
            }
            return best;
        }

        public Vector3Int RotatedSize(BlueprintData data)
        {
            Vector3Int s = RotateDoubled(new Vector3Int(data.sizeX, data.sizeY, data.sizeZ), Turns);
            return new Vector3Int(Mathf.Abs(s.x), s.y, Mathf.Abs(s.z));
        }

        public Vector3Int RotatedMin(BlueprintData data)
        {
            Vector3Int a = RotateDoubled(Vector3Int.zero, Turns), b = RotateDoubled(new Vector3Int(data.sizeX - 1, data.sizeY - 1, data.sizeZ - 1), Turns);
            return Vector3Int.Min(a, b);
        }

        public static BlueprintTransform Centered(BlueprintData data, int turns, Vector3Int anchor)
        {
            var t = new BlueprintTransform(turns, Vector3Int.zero);
            Vector3Int min = t.RotatedMin(data), size = t.RotatedSize(data);
            t.Offset = anchor - min - new Vector3Int(size.x / 2, 0, size.z / 2);
            return t;
        }
    }

    internal sealed class BlueprintEntry
    {
        public string Path;
        public BlueprintData Data;
        private Texture2D _preview;

        public Texture2D Preview => _preview != null ? _preview : (_preview = BlueprintStore.BuildPreview(Data));

        public void ClearPreview()
        {
            if (_preview != null) UnityEngine.Object.Destroy(_preview);
            _preview = null;
        }
    }

    internal static class BlueprintStore
    {
        private static List<BlueprintEntry> _entries;

        internal static string Directory
        {
            get
            {
                string dir = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "Blueprints");
                System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        internal static IReadOnlyList<BlueprintEntry> Entries
        {
            get
            {
                if (_entries == null) Reload();
                return _entries;
            }
        }

        internal static void Reload()
        {
            if (_entries != null)
            {
                foreach (BlueprintEntry e in _entries) e.ClearPreview();
            }
            _entries = new List<BlueprintEntry>();
            foreach (string file in System.IO.Directory.GetFiles(Directory, "*.json"))
            {
                try
                {
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<BlueprintData>(File.ReadAllText(file));
                    if (data != null)
                    {
                        _entries.Add(new BlueprintEntry { Path = file, Data = data });
                    }
                }
                catch (Exception e)
                {
                    Melon<GearMod>.Logger.Warning($"Blueprint {file}: {e.Message}");
                }
            }
            _entries = _entries.OrderByDescending(e => e.Data.created).ToList();
        }

        internal static BlueprintEntry Save(BlueprintData data)
        {
            data.created = DateTime.Now.Ticks;
            string path = UniquePath(data.name, null);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(data));
            var entry = new BlueprintEntry { Path = path, Data = data };
            if (_entries == null) Reload();
            else _entries.Insert(0, entry);
            return _entries.FirstOrDefault(e => e.Path == path) ?? entry;
        }

        internal static void Rename(BlueprintEntry entry, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name == entry.Data.name)
            {
                return;
            }
            entry.Data.name = name;
            string path = UniquePath(name, entry.Path);
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(entry.Data));
            if (!string.Equals(path, entry.Path, StringComparison.OrdinalIgnoreCase) && File.Exists(entry.Path))
            {
                File.Delete(entry.Path);
            }
            entry.Path = path;
        }

        internal static void Delete(BlueprintEntry entry)
        {
            if (File.Exists(entry.Path)) File.Delete(entry.Path);
            entry.ClearPreview();
            _entries?.Remove(entry);
        }

        internal static string NextName()
        {
            var names = new HashSet<string>(Entries.Select(e => e.Data.name), StringComparer.OrdinalIgnoreCase);
            for (int i = 1; ; i++)
            {
                string name = $"Blueprint {i}";
                if (!names.Contains(name)) return name;
            }
        }

        private static string UniquePath(string name, string keep)
        {
            string safe = new string(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            if (safe.Length == 0) safe = "Blueprint";
            string path = System.IO.Path.Combine(Directory, safe + ".json");
            for (int i = 2; File.Exists(path) && !string.Equals(path, keep, StringComparison.OrdinalIgnoreCase); i++)
            {
                path = System.IO.Path.Combine(Directory, $"{safe} ({i}).json");
            }
            return path;
        }

        internal static Texture2D BuildPreview(BlueprintData data)
        {
            const int size = 160;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BlueprintPreview" };
            var px = new Color[size * size];
            var background = new Color(0.06f, 0.12f, 0.2f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = background;
            int w = Mathf.Max(1, data.sizeX), d = Mathf.Max(1, data.sizeZ), h = Mathf.Max(1, data.sizeY);
            float cell = Mathf.Min((size - 8f) / w, (size - 8f) / d);
            float ox = (size - cell * w) / 2f, oy = (size - cell * d) / 2f;
            var top = new Dictionary<(int, int), int>();
            foreach (BlueprintHull p in data.hull)
            {
                int y = p.t == (int)VoxelPartType.Floor ? p.y - 1 : p.y;
                if (!top.TryGetValue((p.x, p.z), out int old) || y > old) top[(p.x, p.z)] = y;
            }
            foreach (var kv in top)
            {
                float k = Mathf.Clamp01((kv.Value + 1f) / h);
                Fill(px, size, ox + kv.Key.Item1 * cell, oy + kv.Key.Item2 * cell, cell, cell, Color.Lerp(new Color(0.15f, 0.35f, 0.6f), new Color(0.6f, 0.85f, 1f), k));
            }
            foreach (BlueprintPart p in data.parts)
            {
                float x = p.onHull ? p.hx : p.x, z = p.onHull ? p.hz : p.z;
                float dot = Mathf.Max(2f, cell * 0.6f);
                Fill(px, size, ox + (x + 0.5f) * cell - dot / 2f, oy + (z + 0.5f) * cell - dot / 2f, dot, dot, new Color(1f, 0.6f, 0.2f));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static void Fill(Color[] px, int size, float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, size - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(x + w), 0, size);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, size - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(y + h), 0, size);
            for (int yy = y0; yy < y1; yy++)
            {
                for (int xx = x0; xx < x1; xx++)
                {
                    px[yy * size + xx] = c;
                }
            }
        }
    }
}
