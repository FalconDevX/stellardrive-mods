using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Constant;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class PowerMeter
    {
        internal const ushort PartId = 243;
        internal const byte SignalSocket = 0, PowerSocket = 1;
        private const ushort GeneratorPartId = 60;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings constant = parts.FirstOrDefault(p => p?.part != null && p.part.GetComponentInChildren<ConstantVisuals>(true) != null);
            PartSettings generator = PartFactory.Find(parts, GeneratorPartId);
            if (constant == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(constant, generator);
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build Power Meter: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings constant, PartSettings generator)
        {
            GameObject root = Object.Instantiate(constant.part, PartFactory.Holder);
            root.name = "MiningPowerMeter";
            PartFactory.RemoveInteract(root);

            SocketObject signal = root.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Signal).OrderBy(s => s.Id).FirstOrDefault();
            if (signal == null)
            {
                throw new Exception("constant has no signal socket");
            }
            Transform parent = signal.transform.parent;
            Vector3 pos = signal.transform.localPosition;
            Vector3 mirrored = new Vector3(Mathf.Abs(pos.x) > 0.05f ? -pos.x : pos.x + 0.25f, pos.y, Mathf.Abs(pos.x) > 0.05f ? pos.z : -pos.z);
            SocketObject power = PartFactory.CloneSocket(PartFactory.FindSocket(generator, CableType.Power), parent, PowerSocket, mirrored, 0f);
            power.transform.localRotation = Mathf.Abs(pos.x) > 0.05f ? Quaternion.Euler(0f, 180f, 0f) * signal.transform.localRotation : signal.transform.localRotation;
            Melon<MiningMod>.Logger.Msg($"Power Meter: signal socket at {pos}, power socket at {mirrored}");

            PartSettings settings = Object.Instantiate(constant);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Power Meter";
            settings.description = "Connect its power socket to a power network and its signal socket to an info panel or other logic. " +
                                   "It sends the network load in percent: power needed divided by what the generators can give (100 = fully used, above 100 = overloaded). " +
                                   "The meter itself uses no power.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 2), (112, 3));
            return settings;
        }
    }
}
