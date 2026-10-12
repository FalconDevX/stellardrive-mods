using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common.Gauge;
using Ships.Parts.FuelTank;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Duży zbiornik 4x4x4: kopia zbiornika na płyny z gry (FluidTankState, okno z płynem, wskaźnik, sygnał ilości)
    // z przeskalowanym wyglądem i kolizjami. Wskaźnik i gniazda zostają w zwykłym rozmiarze, przeniesione na ścianki
    // dużego zbiornika; 4 gniazda na rury. Pojemność 64 razy większa (objętość) - Patches/BigFluidTankPatches.
    internal static class BigFluidTank
    {
        internal const ushort PartId = 215;
        internal const ushort ResearchId = 207;
        internal const ushort TankPartId = 23;
        internal const float Scale = 4f;
        internal const float Capacity = 100f * Scale * Scale * Scale;
        private const int FluidSockets = 4;

        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            PartSettings tank = PartFactory.Find(gameParts, TankPartId);
            if (tank?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(tank);
                Melon<MiningMod>.Logger.Msg($"Dodano część '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować dużego zbiornika: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings tank)
        {
            GameObject root = Object.Instantiate(tank.part, PartFactory.Holder);
            root.name = "MiningBigFluidTank";
            ScaleLook(root, tank.size);
            AddFluidSockets(root);
            foreach (FluidTankVisuals v in root.GetComponentsInChildren<FluidTankVisuals>(true))
            {
                v.gameObject.AddComponent<BigFluidTankMarker>();
            }

            PartSettings settings = Object.Instantiate(tank);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Large Fluid Tank";
            settings.description = $"A 4 x 4 x 4 fluid tank: holds {Capacity:0} units (64 normal tanks). Four pipe sockets, " +
                                   "gauge and fill-level signal like the normal tank.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = tank.size * Scale;
            settings.part = root;
            settings.mass = tank.mass * 8f;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 60), (Items.Model.ItemsConstants.GlassItemId, 10));
            return settings;
        }

        // Jak w dużych drzwiach (BigDoors.Scale): gra kopiuje osobno każdy obiekt z korzenia części (wygląd, kolizje,
        // interakcje) bez rodzica, więc skalujemy wewnątrz każdego z nich. Zbiornik z gry jest mniejszy niż pole (ok.
        // 0.86 x 0.8 m), więc skalujemy względem środka pola: powiększony stoi na środku swojego bloku pól 4x4x4
        // (część zajmuje pola od pola położenia w +X, +Y, +Z), z dnem na podłodze.
        // Wyjątki: gniazda i wskaźnik - tylko przesunięte, z tą samą odległością od ścianki co w małym zbiorniku.
        private static void ScaleLook(GameObject root, Vector3 size)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c0 = bounds.center, b0 = bounds.bounds;
            Vector3 b1 = b0 * Scale;
            var cell = new Vector3(0f, size.y * 0.5f, 0f);                 // środek pola małego zbiornika
            Vector3 block = cell + (size * Scale - size) * 0.5f;            // środek bloku pól dużego
            Vector3 c1 = block + (c0 - cell) * Scale;
            bounds.center = c1;
            bounds.bounds = b1;

            Vector3 MapKeepSize(Vector3 p)
            {
                // punkt przy ściance zostaje w tej samej odległości od nowej ścianki, reszta skaluje się
                var r = new Vector3();
                for (int k = 0; k < 3; k++)
                {
                    float d = p[k] - c0[k], h0 = b0[k] * 0.5f, h1 = b1[k] * 0.5f;
                    r[k] = Mathf.Abs(d) > h0 - 0.15f ? c1[k] + Mathf.Sign(d) * (h1 - (h0 - Mathf.Abs(d))) : c1[k] + d * Scale;
                }
                return r;
            }

            var s = Vector3.one * Scale;
            foreach (Transform unit in root.transform.Cast<Transform>().ToArray())
            {
                if (unit.GetComponent<SocketObject>() != null)
                {
                    unit.localPosition = MapKeepSize(unit.localPosition);
                    continue;
                }
                // wyjątki wewnątrz obiektu: położenie w układzie części przed skalowaniem
                var keep = unit.GetComponentsInChildren<SocketObject>(true).Select(x => x.transform)
                    .Concat(unit.GetComponentsInChildren<Gauge>(true).Select(x => x.transform))
                    .Distinct().Select(t => (t, pos: root.transform.InverseTransformPoint(t.position))).ToList();

                Vector3 offset = c1 - Vector3.Scale(s, c0) + Vector3.Scale(s - Vector3.one, unit.localPosition);
                var scaled = new GameObject("Scaled").transform;
                scaled.SetParent(unit, false);
                foreach (Transform child in unit.Cast<Transform>().Where(t => t != scaled).ToArray())
                {
                    child.SetParent(scaled, false);
                }
                scaled.localPosition = offset;
                scaled.localScale = s;
                foreach (BoxCollider box in unit.GetComponents<BoxCollider>())
                {
                    box.center = offset + Vector3.Scale(s, box.center);
                    box.size = Vector3.Scale(s, box.size);
                }
                foreach (var (t, pos) in keep)
                {
                    t.position = root.transform.TransformPoint(MapKeepSize(pos));
                    t.localScale /= Scale;
                }
                if (unit.GetComponents<Collider>().Any(c => !(c is BoxCollider)))
                {
                    Melon<MiningMod>.Logger.Warning($"Duży zbiornik: {unit.name} ma kolizję inną niż BoxCollider - zostaje w małym rozmiarze");
                }
            }
        }

        // Gniazda rur: te z małego zbiornika (przeniesione na ścianki) i kopie na wolnych bokach - razem 4.
        // Jeśli w małym zbiorniku gniazda rur są połączone (relayedSockets), łączymy wszystkie 4.
        private static void AddFluidSockets(GameObject root)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c = bounds.center, half = bounds.bounds * 0.5f;
            SocketObject[] all = root.GetComponentsInChildren<SocketObject>(true);
            List<SocketObject> fluids = all.Where(x => x.Type == CableType.Fluid).OrderBy(x => x.Id).ToList();
            if (fluids.Count == 0)
            {
                throw new InvalidOperationException("Zbiornik z gry nie ma gniazd rur");
            }
            bool relayed = fluids.Any(f => f.RelayedSockets != null && f.RelayedSockets.Any());

            // ścianka gniazda: oś, na której leży najbliżej obrysu
            (int axis, float sign) FaceOf(Vector3 p)
            {
                Vector3 d = p - c;
                int best = 0;
                float bestGap = float.MaxValue;
                for (int k = 0; k < 3; k++)
                {
                    float gap = half[k] - Mathf.Abs(d[k]);
                    if (gap < bestGap) { bestGap = gap; best = k; }
                }
                return (best, Mathf.Sign(d[best]));
            }

            SocketObject source = fluids[0];
            Vector3 srcPos = root.transform.InverseTransformPoint(source.transform.position);
            var srcFace = FaceOf(srcPos);
            float inset = half[srcFace.axis] - Mathf.Abs(srcPos[srcFace.axis] - c[srcFace.axis]);
            float y = c.y - half.y + 0.5f;  // najniższy rząd pól (blisko podłogi)

            var used = new HashSet<(int, float)>(fluids.Select(f => FaceOf(root.transform.InverseTransformPoint(f.transform.position))));
            var faces = new[] { (0, -1f), (0, 1f), (2, -1f), (2, 1f), (1, 1f), (1, -1f) };
            var ids = new HashSet<byte>(all.Select(x => x.Id));
            byte nextId = 0;
            foreach (var face in faces)
            {
                if (fluids.Count >= FluidSockets) break;
                if (used.Contains(face)) continue;
                while (ids.Contains(nextId)) nextId++;
                Vector3 pos = c;
                if (face.Item1 != 1) pos.y = y;
                pos[face.Item1] = c[face.Item1] + face.Item2 * (half[face.Item1] - inset);
                var normal = new Vector3();
                normal[face.Item1] = face.Item2;
                SocketObject copy = PartFactory.CloneSocket(source, root.transform, nextId, pos, 0f);
                copy.transform.localRotation = Quaternion.LookRotation(-normal, face.Item1 == 1 ? Vector3.forward : Vector3.up); // +Z gniazda do wnętrza
                ids.Add(nextId);
                fluids.Add(copy);
                used.Add(face);
            }
            if (relayed)
            {
                foreach (SocketObject f in fluids)
                {
                    Traverse.Create(f).Field("relayedSockets").SetValue(fluids.Where(o => o != f).ToArray());
                }
            }
            Melon<MiningMod>.Logger.Msg($"Duży zbiornik: gniazda rur {string.Join(", ", fluids.Select(f => $"{f.Id}@{root.transform.InverseTransformPoint(f.transform.position)}"))}, połączone={relayed}");
        }
    }

    // Znacznik na obiekcie wyglądu dużego zbiornika (FluidTankVisuals liczy wskaźnik i poziom płynu do 100).
    internal sealed class BigFluidTankMarker : MonoBehaviour
    {
    }
}
