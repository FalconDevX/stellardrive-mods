using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common.Model.Reactions;
using UnityEngine;

namespace Mining.Content
{
    internal sealed class ShapeColliders : MonoBehaviour, IShipPartDynamicCollider
    {
        private static PhysicsMaterial _material;

        public int Inflatable = -1;
        public Vector3 OpenCenter, OpenSize, ClosedCenter, ClosedSize;

        public GameObject GameObject => gameObject;

        internal static ShapeColliders Add(GameObject root, params Bounds[] boxes)
        {
            var go = new GameObject("ShapeColliders") { layer = LayerMask.NameToLayer("Ship") };
            go.transform.SetParent(root.transform, false);
            var shape = go.AddComponent<ShapeColliders>();
            _material = _material ?? new PhysicsMaterial("ModPartShape") { dynamicFriction = 0.6f, staticFriction = 0.6f };
            foreach (Bounds b in boxes)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
                box.sharedMaterial = _material;
            }
            return shape;
        }

        internal void SetInflatable(int index, Bounds open, Bounds closed)
        {
            Inflatable = index;
            OpenCenter = open.center;
            OpenSize = open.size;
            ClosedCenter = closed.center;
            ClosedSize = closed.size;
        }

        public void SetNetworkState(IStatefulPartState state) => Apply(state);

        public void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Apply(change.NewState);

        private void Apply(IStatefulPartState state)
        {
            if (Inflatable < 0)
            {
                return;
            }
            BoxCollider[] boxes = GetComponents<BoxCollider>();
            if (Inflatable >= boxes.Length)
            {
                return;
            }
            bool open = state is IDoorState door && door.IsOpen;
            boxes[Inflatable].center = open ? OpenCenter : ClosedCenter;
            boxes[Inflatable].size = open ? OpenSize : ClosedSize;
        }
    }
}
