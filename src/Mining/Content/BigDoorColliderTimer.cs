using System.Globalization;
using HarmonyLib;
using Ships.Parts.Door;
using UnityEngine;

namespace Mining.Content
{
    internal class BigDoorColliderTimer : MonoBehaviour
    {
        internal const string LeafPrefix = "Door|";

        private GameObject _leaf;
        private float _seconds = 1.6f;
        private float _until;
        private bool _armed;

        private GameObject Leaf
        {
            get
            {
                if (_leaf != null)
                {
                    return _leaf;
                }
                var dynamic = GetComponent<DoorDynamicColliders>();
                _leaf = dynamic != null ? Traverse.Create(dynamic).Field<GameObject>("doorCollider").Value : null;
                if (_leaf != null && _leaf.name.StartsWith(LeafPrefix) &&
                    float.TryParse(_leaf.name.Substring(LeafPrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
                {
                    _seconds = seconds;
                }
                return _leaf;
            }
        }

        internal void Apply(bool open)
        {
            GameObject leaf = Leaf;
            if (leaf == null)
            {
                return;
            }
            if (!open)
            {
                _armed = false;
                leaf.SetActive(true);
                return;
            }
            if (leaf.activeSelf && !_armed)
            {
                _armed = true;
                _until = Time.time + _seconds;
            }
        }

        private void Update()
        {
            if (_armed && Time.time >= _until)
            {
                _armed = false;
                Leaf?.SetActive(false);
            }
        }
    }
}
