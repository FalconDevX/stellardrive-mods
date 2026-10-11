using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Settings;
using UnityEngine;

namespace Mining.Content
{
    // Rotor jest w grze (obraca przyczepiony do siebie "podstatek" do kąta z sygnału), ale demo go nie udostępnia.
    // Odblokowujemy go badaniem "Mechanics" i zmieniamy koszt z aluminium (na Otrera b go nie ma) na żelazo i miedź.
    // Sygnał w stopniach i prędkość obrotu: Server/RotorDegreesPatch.
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
                        _rotor.description = "Rotates everything built on its plate to the angle (in degrees, -180 to 180) given by the signal on its socket - " +
                                             "e.g. type the angle into a Constant and connect it. Placed on a wall it works as a hinge.";
                        Melon<MiningMod>.Logger.Msg("Rotor: koszt 8 żelaza + 4 miedzi, sygnał w stopniach");
                    }
                }
                return _rotor;
            }
        }

        internal static ushort MenuNextTo => GyroscopePartId;
    }
}
