using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Settings;
using UnityEngine;

namespace Mining.Content
{
    internal static class RotorUnlock
    {
        internal const ushort RotorPartId = 55;
        internal const ushort ResearchId = 203;
        private const ushort GyroscopePartId = 53;
        private static PartSettings _rotor;

        internal static PartSettings Rotor
        {
            get
            {
                if (_rotor == null)
                {
                    _rotor = Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(p => p.id == RotorPartId);
                    if (_rotor != null)
                    {
                        PartFactory.SetCost(_rotor, (Items.Model.ItemsConstants.IronItemId, 8), (112, 4));
                        _rotor.description = "Rotates everything built on its plate to the angle given by the signal on its socket. A lever works directly: 0 = start, 1 = 90 degrees. " +
                                             "Bigger signals are degrees (-180 to 180), e.g. type 45 into a Constant. Placed on a wall it works as a hinge.";
                        Melon<MiningMod>.Logger.Msg("Rotor: costs 8 iron + 4 copper, signal in degrees");
                        StretchBase(_rotor);
                    }
                }
                return _rotor;
            }
        }

        internal static ushort MenuNextTo => GyroscopePartId;

        internal static float PlateOffset;

        private static void StretchBase(PartSettings rotor)
        {
            Transform root = rotor.part != null ? rotor.part.transform : null;
            Transform visuals = root != null ? root.Find("Visuals") : null;
            if (visuals == null)
            {
                return;
            }
            float min = float.MaxValue, max = float.MinValue;
            foreach (MeshFilter filter in visuals.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    float y = root.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
            }
            float height = max - min;
            Melon<MiningMod>.Logger.Msg($"Rotor: base from y={min:F2} to y={max:F2}");
            if (height < 0.1f || height > 0.95f)
            {
                return;
            }
            float scale = 1f / height;
            Vector3 p = visuals.localPosition;
            visuals.localPosition = new Vector3(p.x, p.y * scale + min * (1f - scale), p.z);
            visuals.localScale = Vector3.Scale(visuals.localScale, new Vector3(1f, scale, 1f));
            PlateOffset = min + 1f - 0.5f;
            Melon<MiningMod>.Logger.Msg($"Rotor: base stretched x{scale:F2}, plate moved by {PlateOffset:F2}");
        }
    }
}
