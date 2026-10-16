using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Parts.Door;
using UnityEngine;

namespace Mining.Content
{
    internal class BigDoorLeafColliders : MonoBehaviour
    {
        internal const string DoorPrefix = "Door|";
        internal const string LeafPrefix = "Leaf|";
        private const float MinGap = 0.02f;

        private struct Leaf
        {
            public Transform T;
            public BoxCollider Box;
            public bool AxisX;
            public int Dir;
            public float Distance, Lo, Hi, ClipLo, ClipHi;
            public Vector3 Center, Size;
        }

        private readonly List<Leaf> _leaves = new List<Leaf>();
        private Transform _root;
        private GameObject _door;
        private float _seconds = 1.6f;
        private float _t, _target;
        private bool _ready;

        private void Init()
        {
            _ready = true;
            var dynamic = GetComponent<DoorDynamicColliders>();
            _door = dynamic != null ? Traverse.Create(dynamic).Field<GameObject>("doorCollider").Value : null;
            ShipPartBounds bounds = GetComponentInParent<ShipPartBounds>();
            _root = bounds != null ? bounds.transform : transform.root;
            if (_door == null)
            {
                return;
            }
            if (_door.name.StartsWith(DoorPrefix) &&
                float.TryParse(_door.name.Substring(DoorPrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            {
                _seconds = seconds;
            }
            foreach (Transform child in _door.transform)
            {
                string[] p = child.name.Split('|');
                BoxCollider box = child.GetComponent<BoxCollider>();
                if (p.Length != 8 || p[0] + "|" != LeafPrefix || box == null)
                {
                    continue;
                }
                _leaves.Add(new Leaf
                {
                    T = child,
                    Box = box,
                    AxisX = p[1] == "x",
                    Dir = int.Parse(p[2], CultureInfo.InvariantCulture),
                    Distance = Parse(p[3]),
                    Lo = Parse(p[4]),
                    Hi = Parse(p[5]),
                    ClipLo = Parse(p[6]),
                    ClipHi = Parse(p[7]),
                    Center = _root.InverseTransformPoint(child.TransformPoint(box.center)),
                    Size = Abs(_root.InverseTransformVector(child.TransformVector(box.size))),
                });
            }
        }

        private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        internal void SetOpen(bool open, bool instant)
        {
            if (!_ready)
            {
                Init();
            }
            _target = open ? 1f : 0f;
            if (instant || _leaves.Count == 0)
            {
                _t = _target;
            }
            Apply();
        }

        private void Update()
        {
            if (!_ready || Mathf.Approximately(_t, _target))
            {
                return;
            }
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime / _seconds);
            Apply();
        }

        private void Apply()
        {
            if (_door == null)
            {
                return;
            }
            bool open = _t >= 1f;
            if (_door.activeSelf == open)
            {
                _door.SetActive(!open);
            }
            if (open)
            {
                return;
            }
            float x = Mathf.Clamp01(_t);
            float k = x * x * x * (x * (x * 6f - 15f) + 10f);
            foreach (Leaf leaf in _leaves)
            {
                if (leaf.T == null)
                {
                    continue;
                }
                float shift = leaf.Dir * leaf.Distance * k;
                float a = Mathf.Max(leaf.Lo + shift, leaf.ClipLo), b = Mathf.Min(leaf.Hi + shift, leaf.ClipHi);
                bool solid = b - a > MinGap;
                if (leaf.Box.enabled != solid)
                {
                    leaf.Box.enabled = solid;
                }
                if (!solid)
                {
                    continue;
                }
                Vector3 c = leaf.Center, s = leaf.Size;
                if (leaf.AxisX) { c.x = (a + b) / 2; s.x = b - a; }
                else { c.y = (a + b) / 2; s.y = b - a; }
                leaf.Box.center = leaf.T.InverseTransformPoint(_root.TransformPoint(c));
                leaf.Box.size = Abs(leaf.T.InverseTransformVector(_root.TransformVector(s)));
            }
        }
    }
}
