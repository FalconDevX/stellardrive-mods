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

        // O ile dalej niż w grze zawiesić talerz (Patches/RotorPlateHeightPatch), żeby leżał na wierzchu podstawy.
        internal static float PlateOffset;

        // Podstawa rotora w grze ma tylko pół kratki wysokości. Rozciągamy sam wygląd ("Visuals") od jego spodu
        // na pełną kratkę - wymiary bierzemy z siatek modelu, gniazdo sygnału zostaje bez zmian.
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
            Melon<MiningMod>.Logger.Msg($"Rotor: podstawa od y={min:F2} do y={max:F2}");
            if (height < 0.1f || height > 0.95f)
            {
                return; // nie wygląda na połowę kratki - zostawiamy jak w grze
            }
            float scale = 1f / height;
            // skalowanie w osi Y względem spodu podstawy (spód zostaje na podłodze)
            Vector3 p = visuals.localPosition;
            visuals.localPosition = new Vector3(p.x, p.y * scale + min * (1f - scale), p.z);
            visuals.localScale = Vector3.Scale(visuals.localScale, new Vector3(1f, scale, 1f));
            // gra zawiesza talerz 0,5 nad środkiem części; ma być na nowym wierzchu podstawy (min + 1)
            PlateOffset = min + 1f - 0.5f;
            Melon<MiningMod>.Logger.Msg($"Rotor: podstawa rozciągnięta x{scale:F2}, talerz przesunięty o {PlateOffset:F2}");
        }
    }
}
