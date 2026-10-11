using System.Collections.Generic;
using Audio.Interface;
using Core.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Mining.Content
{
    // Własny model dużych drzwi, budowany w kodzie dla każdego rozmiaru (rama zawsze tej samej grubości, nie rozciągnięta).
    // Kolory z palety modów (reactor_albedo.png, jak w lowpoly.py). Drzwi do 4 m szerokości: dwa skrzydła rozsuwane na
    // boki; szersze: brama podnoszona do góry (jak w hangarze).
    internal static class BigDoorModel
    {
        private static readonly string[] Names =
        {
            "yellow", "yellow_dark", "dark", "grey", "light", "orange", "steel", "screen", "black", "rubber", "olive", "white", "cyan", "blue",
        };

        internal const string ModelName = "BigDoorModel";
        internal const float Frame = 0.2f;
        internal const float Sill = 0.08f;

        internal static bool IsLift(float width) => width > 4.5f;

        internal static void Build(Transform root, Bounds b, Material material, MeshRenderer look)
        {
            var model = new GameObject(ModelName).transform;
            model.SetParent(root, false);
            Vector3 min = b.min, max = b.max;
            float w = b.size.x, h = b.size.y, z = b.center.z;
            float depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f); // grubość ściany (obrys drzwi z gry bywa na całe pole)

            // rama: słupy, nadproże, próg z pasami, pomarańczowe narożniki
            var frame = new MeshBuilder();
            frame.Box(new Vector3(min.x + Frame / 2, (min.y + max.y) / 2, z), new Vector3(Frame, h, depth), "dark");
            frame.Box(new Vector3(max.x - Frame / 2, (min.y + max.y) / 2, z), new Vector3(Frame, h, depth), "dark");
            frame.Box(new Vector3((min.x + max.x) / 2, max.y - Frame / 2, z), new Vector3(w, Frame, depth), "dark");
            frame.Box(new Vector3((min.x + max.x) / 2, min.y + Sill / 2, z), new Vector3(w - 2 * Frame, Sill, depth * 0.9f), "black");
            foreach (float sx in new[] { min.x + Frame / 2, max.x - Frame / 2 })
            {
                foreach (float sy in new[] { max.y - Frame / 2, min.y + 0.15f })
                {
                    frame.Box(new Vector3(sx, sy, z), new Vector3(Frame + 0.04f, 0.3f, depth + 0.04f), "orange");
                }
                for (float y = min.y + 0.5f; y < max.y - 0.5f; y += 0.6f)
                {
                    frame.Box(new Vector3(sx, y, z), new Vector3(Frame + 0.02f, 0.04f, depth + 0.02f), "black"); // żebra słupów
                }
            }
            for (float x = min.x + Frame; x < max.x - Frame - 0.01f; x += 0.3f)                                    // pasy na progu
            {
                float x1 = Mathf.Min(x + 0.15f, max.x - Frame);
                foreach (float side in new[] { -1f, 1f })
                {
                    frame.Box(new Vector3((x + x1) / 2, min.y + Sill / 2, z + side * depth * 0.45f), new Vector3(x1 - x, Sill * 0.9f, 0.01f), "yellow");
                }
            }
            frame.Box(new Vector3((min.x + max.x) / 2, max.y - Frame - 0.04f, z), new Vector3(w - 2 * Frame, 0.08f, depth * 0.7f), "grey"); // prowadnica
            PartFactory.CreateRenderer("Frame", model, frame.ToMesh("BigDoorFrame"), material, look);

            float ix0 = min.x + Frame, ix1 = max.x - Frame, iy0 = min.y + Sill, iy1 = max.y - Frame - 0.08f;
            float leafDepth = Mathf.Min(0.14f, depth * 0.5f);
            if (IsLift(w))
            {
                var panel = new MeshBuilder();
                float pw = ix1 - ix0, ph = iy1 - iy0;
                var c = new Vector3((ix0 + ix1) / 2, (iy0 + iy1) / 2, z);
                panel.Box(c, new Vector3(pw, ph, leafDepth), "light");
                for (float y = iy0 + 0.5f; y < iy1 - 0.1f; y += 0.5f)                                           // segmenty bramy
                {
                    panel.Box(new Vector3(c.x, y, z), new Vector3(pw, 0.04f, leafDepth + 0.02f), "grey");
                }
                float wy = iy1 - 0.45f;                                                                         // rząd okienek
                for (float x = ix0 + 0.6f; x < ix1 - 0.5f; x += 1.2f)
                {
                    panel.Box(new Vector3(x, wy, z), new Vector3(0.6f, 0.22f, leafDepth + 0.03f), "screen");
                }
                for (float x = ix0; x < ix1 - 0.01f; x += 0.4f)                                                // pasy przy dolnej krawędzi
                {
                    float x1 = Mathf.Min(x + 0.2f, ix1);
                    panel.Box(new Vector3((x + x1) / 2, iy0 + 0.12f, z), new Vector3(x1 - x, 0.18f, leafDepth + 0.025f), "yellow");
                    float x2 = Mathf.Min(x1 + 0.2f, ix1);
                    if (x2 > x1) panel.Box(new Vector3((x1 + x2) / 2, iy0 + 0.12f, z), new Vector3(x2 - x1, 0.18f, leafDepth + 0.025f), "black");
                }
                panel.Box(new Vector3(c.x, iy0 + 0.45f, z), new Vector3(0.8f, 0.08f, leafDepth + 0.08f), "dark"); // uchwyt
                PartFactory.CreateRenderer("LeafUp", model, panel.ToMesh("BigDoorPanel"), material, look);
            }
            else
            {
                float half = (ix1 - ix0) / 2;
                for (int side = 0; side < 2; side++)
                {
                    float x0 = side == 0 ? ix0 : ix0 + half, x1 = x0 + half;
                    float edge = side == 0 ? x1 : x0;                    // krawędź przy środku drzwi
                    float ph = iy1 - iy0;
                    var leaf = new MeshBuilder();
                    var c = new Vector3((x0 + x1) / 2, (iy0 + iy1) / 2, z);
                    leaf.Box(c, new Vector3(half - 0.01f, ph, leafDepth), "light");
                    for (float y = iy0 + 0.6f; y < iy1 - 0.2f; y += 0.9f)
                    {
                        leaf.Box(new Vector3(c.x, y, z), new Vector3(half - 0.01f, 0.03f, leafDepth + 0.02f), "grey");  // szwy
                    }
                    if (ph > 2.2f)
                    {
                        leaf.Box(new Vector3(c.x, iy1 - 0.6f, z), new Vector3(half * 0.5f, 0.4f, leafDepth + 0.03f), "screen"); // okienko
                    }
                    float sx = edge + (side == 0 ? -0.06f : 0.06f);
                    for (float y = iy0; y < iy1 - 0.01f; y += 0.3f)                                            // pasy na krawędzi
                    {
                        float y1 = Mathf.Min(y + 0.15f, iy1);
                        leaf.Box(new Vector3(sx, (y + y1) / 2, z), new Vector3(0.1f, y1 - y, leafDepth + 0.025f), "yellow");
                    }
                    float hx = edge + (side == 0 ? -0.25f : 0.25f);
                    leaf.Box(new Vector3(hx, iy0 + Mathf.Min(1.1f, ph * 0.45f), z), new Vector3(0.06f, 0.5f, leafDepth + 0.1f), "dark"); // uchwyt
                    leaf.Box(new Vector3(c.x, iy0 + 0.06f, z), new Vector3(half - 0.01f, 0.12f, leafDepth + 0.02f), "orange"); // listwa
                    PartFactory.CreateRenderer(side == 0 ? "LeafL" : "LeafR", model, leaf.ToMesh("BigDoorLeaf"), material, look);
                }
            }
        }

        private static Vector2 Uv(string color)
        {
            int i = System.Array.IndexOf(Names, color);
            return new Vector2((i % 5 + 0.5f) / 5f, 1f - (i / 5 + 0.5f) / 5f);
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _t = new List<int>();

            public void Box(Vector3 center, Vector3 size, string color)
            {
                Vector2 uv = Uv(color);
                Vector3 half = size * 0.5f;
                foreach (Vector3 n in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                {
                    Vector3 v = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                    Vector3 u = Vector3.Cross(n, v); // przy takim u i v ścianka jest zgodna z ruchem wskazówek patrząc z zewnątrz
                    Vector3 c = center + Vector3.Scale(n, half);
                    Vector3 du = u * Mathf.Abs(Vector3.Dot(u, half)), dv = v * Mathf.Abs(Vector3.Dot(v, half));
                    int start = _v.Count;
                    _v.Add(c - du - dv); _v.Add(c - du + dv); _v.Add(c + du + dv); _v.Add(c + du - dv);
                    for (int k = 0; k < 4; k++) { _n.Add(n); _uv.Add(uv); }
                    _t.Add(start); _t.Add(start + 1); _t.Add(start + 2);
                    _t.Add(start); _t.Add(start + 2); _t.Add(start + 3);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetUVs(0, _uv);
                var palette = new Vector2[_v.Count];
                for (int i = 0; i < palette.Length; i++) palette[i] = ModAssets.PaletteUv;
                mesh.uv2 = palette;
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    // Animacja skrzydeł: rozsuwanie na boki albo podnoszenie bramy; stan (otwarte/zamknięte) jak w drzwiach gry.
    internal class BigDoorVisuals : DefaultShipPartVisuals
    {
        private const float Seconds = 1.4f;
        private readonly List<(Transform leaf, Vector3 closed, Vector3 open)> _leaves = new List<(Transform, Vector3, Vector3)>();
        private float _t;
        private float _target;

        private void Awake()
        {
            Transform root = GetComponentInParent<ShipPartBounds>()?.transform;
            Transform model = root != null ? root.Find(BigDoorModel.ModelName) : null;
            if (model == null)
            {
                return;
            }
            foreach (Transform leaf in model)
            {
                Bounds b = leaf.GetComponent<MeshFilter>()?.sharedMesh?.bounds ?? default;
                Vector3 offset = leaf.name == "LeafL" ? Vector3.left * b.size.x * 0.96f
                    : leaf.name == "LeafR" ? Vector3.right * b.size.x * 0.96f
                    : leaf.name == "LeafUp" ? Vector3.up * b.size.y * 0.94f
                    : Vector3.zero;
                if (offset != Vector3.zero)
                {
                    _leaves.Add((leaf, leaf.localPosition, leaf.localPosition + offset));
                }
            }
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _target = _t = ((IDoorState)state).IsOpen ? 1f : 0f;
            Apply();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            bool wasOpen = ((IDoorState)change.PreviousState).IsOpen, open = ((IDoorState)change.NewState).IsOpen;
            if (wasOpen != open)
            {
                ServiceLocator.GetService<IAudioPlayer>()?.PlayAudioEvent3D(open ? "Event:/Ships/DoorOpen" : "Event:/Ships/DoorClose", gameObject);
            }
            _target = open ? 1f : 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target))
            {
                return;
            }
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime / Seconds);
            Apply();
        }

        private void Apply()
        {
            float k = Mathf.SmoothStep(0f, 1f, _t);
            foreach (var (leaf, closed, open) in _leaves)
            {
                if (leaf != null) leaf.localPosition = Vector3.Lerp(closed, open, k);
            }
        }
    }
}
