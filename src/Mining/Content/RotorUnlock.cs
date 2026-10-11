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
                        StretchBase(_rotor);
                    }
                }
                return _rotor;
            }
        }

        internal static ushort MenuNextTo => GyroscopePartId;

        // Rozciąga w osi Y gałęzie z samym wyglądem; gałąź z gniazdem przechodzi głębiej (gniazdo zostaje, gdzie było).
        private static void Stretch(Transform node)
        {
            foreach (Transform child in node)
            {
                bool hasSocket = child.GetComponentInChildren<Ships.Interface.Model.Parts.Common.SocketObject>(true) != null;
                int renderers = child.GetComponentsInChildren<Renderer>(true).Length;
                Melon<MiningMod>.Logger.Msg($"Rotor prefab: {child.name} pos={child.localPosition} scale={child.localScale} renderers={renderers} socket={hasSocket}");
                if (renderers == 0)
                {
                    continue;
                }
                if (hasSocket)
                {
                    if (child.GetComponent<Renderer>() == null)
                    {
                        Stretch(child);
                    }
                    continue;
                }
                child.localPosition = Vector3.Scale(child.localPosition, new Vector3(1f, 2f, 1f));
                child.localScale = Vector3.Scale(child.localScale, new Vector3(1f, 2f, 1f));
            }
        }

        // Podstawa rotora w grze ma pół kratki wysokości, a talerz siedział w połowie kratki (nie pasował do siatki statku).
        // Talerz podnosi RotorPlateHeightPatch, a tu rozciągamy wygląd podstawy w górę x2, żeby wypełniała całą kratkę
        // (początek prefabu jest na dole, przy powierzchni, na której stoi rotor). Gniazda zostają na miejscu.
        private static void StretchBase(PartSettings rotor)
        {
            if (rotor.part == null)
            {
                return;
            }
            Stretch(rotor.part.transform);
        }
    }
}
