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
                        Rebuild(_rotor);
                    }
                }
                return _rotor;
            }
        }

        internal static ushort MenuNextTo => GyroscopePartId;

        internal static float PlateOffset;

        private static void Rebuild(PartSettings rotor)
        {
            Transform root = rotor.part != null ? rotor.part.transform : null;
            Transform visuals = root != null ? root.Find("Visuals") : null;
            MeshRenderer look = visuals != null ? visuals.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Albedo")) : null;
            if (look == null)
            {
                Melon<MiningMod>.Logger.Warning("Rotor: no visuals to replace");
                return;
            }
            foreach (MeshRenderer r in visuals.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = false;
            }
            Mesh mesh = BuildModel().Build("RotorBody");
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "RotorBody");
            PartFactory.CreateRenderer("Body", visuals, mesh, material, look);
            PlateOffset = 0.5f;
            Melon<MiningMod>.Logger.Msg("Rotor: new textured body, plate moved by 0.50");
        }

        private static MeshBuilder BuildModel()
        {
            const string Steel = "steel", Dark = "dark", Light = "light", Orange = "orange", Yellow = "yellow", Black = "black";
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, 0.04f, -0.015f), new Vector3(0.9f, 0.08f, 0.91f), Dark);
            b.Box(new Vector3(0f, 0.46f, -0.015f), new Vector3(0.84f, 0.76f, 0.91f), Steel);
            b.Box(new Vector3(0f, 0.62f, -0.015f), new Vector3(0.86f, 0.06f, 0.93f), Orange);
            b.Box(new Vector3(0f, 0.81f, -0.015f), new Vector3(0.88f, 0.06f, 0.95f), Dark);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int k = -1; k <= 1; k++)
                {
                    b.Box(new Vector3(side * 0.425f, 0.34f, -0.015f + k * 0.28f), new Vector3(0.02f, 0.44f, 0.06f), Dark);
                    if (side > 0f || k < 0) b.Box(new Vector3(k * 0.28f, 0.4f, -0.015f + side * 0.465f), new Vector3(0.06f, 0.32f, 0.02f), Dark);
                }
            }
            foreach (var (x, z) in new[] { (-0.4f, -0.42f), (0.4f, -0.42f), (-0.4f, 0.39f), (0.4f, 0.39f) })
            {
                b.Cylinder(new Vector3(x, 0.08f, z), new Vector3(x, 0.105f, z), 0.025f, Light, 8);
            }
            b.Box(new Vector3(0.125f, 0.1f, -0.475f), new Vector3(0.18f, 0.16f, 0.01f), Black);
            b.Box(new Vector3(0.125f, 0.19f, -0.477f), new Vector3(0.18f, 0.02f, 0.014f), Yellow);
            b.Cylinder(new Vector3(0f, 0.84f, 0f), new Vector3(0f, 0.9f, 0f), 0.4f, Black, 32);
            b.Cylinder(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0.985f, 0f), 0.43f, Light, 32);
            b.Cylinder(new Vector3(0f, 0.985f, 0f), new Vector3(0f, 0.998f, 0f), 0.16f, Dark, 24);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                var p = new Vector3(Mathf.Cos(a) * 0.34f, 0.985f, Mathf.Sin(a) * 0.34f);
                b.Cylinder(p, p + Vector3.up * 0.012f, 0.022f, Steel, 8);
            }
            b.Box(new Vector3(0f, 0.992f, -0.37f), new Vector3(0.05f, 0.014f, 0.1f), Yellow);
            return b;
        }
    }
}
