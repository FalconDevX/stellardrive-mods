using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Thruster;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Silnik wodorowy: kopia silnika chemicznego (ten sam stan ThrusterState, płomień, dźwięk, fizyka), ale z własnym
    // modelem i paliwem: wodór (106) jako paliwo + woda (105) do chłodzenia. Logika zużycia: Server/HydrogenThrusterServer.
    internal static class HydrogenThruster
    {
        internal const ushort PartId = 201;
        internal const uint HydrogenItemId = 106;
        internal const uint WaterItemId = 105;
        internal static readonly Vector3 NozzleTop = new Vector3(0f, 1.86f, 0f); // NOZZLE_TOP z AssetSource/build_hydrogen.py

        private const ushort ChemicalThrusterId = 19;
        private static ThrusterSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            var chemical = PartFactory.Find(gameParts, ChemicalThrusterId) as ThrusterSettings;
            if (chemical?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chemical);
                Melon<MiningMod>.Logger.Msg($"Dodano część '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować silnika wodorowego: {e}");
            }
            return _part;
        }

        private static ThrusterSettings Build(ThrusterSettings chemical)
        {
            GameObject root = Object.Instantiate(chemical.part, PartFactory.Holder);
            root.name = "HydrogenThruster";

            Mesh mesh = ModAssets.LoadMesh("hydrogen_thruster.bin");
            Texture2D albedo = ModAssets.LoadTexture("hydrogen_albedo.png");

            // Wygląd: wyłączamy model silnika chemicznego (bez płomienia i dźwięku), wstawiamy nasz.
            ThrusterVisual visual = root.GetComponentInChildren<ThrusterVisual>(true);
            ThrusterFlameVFX flame = root.GetComponentInChildren<ThrusterFlameVFX>(true);
            ThrusterSFX sound = root.GetComponentInChildren<ThrusterSFX>(true);
            MeshRenderer look = null;
            foreach (MeshRenderer r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                if ((flame != null && r.transform.IsChildOf(flame.transform)) || (sound != null && r.transform.IsChildOf(sound.transform)))
                {
                    continue;
                }
                if (look == null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Albedo"))
                {
                    look = r;
                }
                r.enabled = false;
            }
            if (look == null)
            {
                throw new InvalidOperationException("Brak materiału silnika chemicznego");
            }
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, albedo, "HydrogenThruster");
            PartFactory.CreateRenderer("Body", visual.transform, mesh, material, look);

            // Płomień i dźwięk na końcu dyszy (dysza = lokalna góra części, jak w silniku chemicznym).
            Vector3 nozzleWorld = root.transform.TransformPoint(NozzleTop);
            if (flame != null) flame.transform.position = nozzleWorld;
            if (sound != null) sound.transform.position = nozzleWorld;

            PartFactory.SetBounds(root, mesh.bounds);

            // Dwa wejścia płynów (0 i 1) na bokach bloku z tyłu: wodór i woda, w dowolnej kolejności.
            SocketObject[] fluids = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Fluid).OrderBy(s => s.Id).ToArray();
            for (int i = 0; i < fluids.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                fluids[i].transform.localPosition = new Vector3(side * 0.35f, 0.25f, 0f);
                fluids[i].transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : 270f, 0f);
            }

            var settings = ScriptableObject.CreateInstance<ThrusterSettings>();
            settings.name = "MiningHydrogenThruster";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Hydrogen Thruster";
            settings.internalStateClass = chemical.internalStateClass; // ThrusterState
            settings.snappingStyle = chemical.snappingStyle;
            settings.description = $"Burns hydrogen and is cooled by water - connect one pipe with hydrogen and one with water to the sockets on the base. " +
                                   $"{MiningMod.HydrogenThrustMultiplier.Value:0.#}x stronger than the chemical thruster. Make hydrogen in the Electrolyzer.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = chemical.thumbnailRotation;
            settings.mass = 45f;
            settings.size = new Vector3(1f, 2f, 1f);
            settings.thrustStrength = chemical.thrustStrength;
            settings.maxFuel = chemical.maxFuel;
            settings.fuelDurationPerUnit = chemical.fuelDurationPerUnit;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 20), (112, 10), (Items.Model.ItemsConstants.GlassItemId, 5));
            return settings;
        }
    }
}
