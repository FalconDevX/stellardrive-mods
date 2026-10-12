using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Duże drzwi (3x2, 3x3, 4x4, 6x3, 10x5, 16x8) - kopie drzwi z gry (2x2) z większym rozmiarem w siatce i przeskalowanym wyglądem.
    // Gra liczy zajęte pola, kolizje i pomieszczenia z rozmiaru części, więc otwieranie, zamykanie i szczelność działają
    // jak w zwykłych drzwiach. Odblokowuje je osobne badanie "Large Doors" (za Doors).
    internal static class BigDoors
    {
        internal const ushort ResearchId = 206;
        internal const ushort DoorPartId = 14;

        internal static readonly (ushort id, string name, int width, int height, int iron)[] Variants =
        {
            (210, "Door 3x3", 3, 3, 18),
            (211, "Door 4x4", 4, 4, 32),
            (212, "Wide Door 6x3", 6, 3, 36),
            (213, "Hangar Door 10x5", 10, 5, 100),
            (216, "Hangar Door 16x8", 16, 8, 220),
            (217, "Wide Door 3x2", 3, 2, 12),
        };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static PartSettings[] All => Variants.Select(v => Get(v.id)).Where(p => p != null).ToArray();

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            List<PartSettings> list = gameParts.ToList();
            var door = PartFactory.Find(list, DoorPartId) as HullPartSettings;
            PartSettings chest = PartFactory.Find(list, 30);
            if (door?.part == null || chest?.part == null)
            {
                return null;
            }
            var variant = Variants.First(v => v.id == id);
            try
            {
                PartSettings part = Build(door, chest, variant);
                Parts[id] = part;
                Melon<MiningMod>.Logger.Msg($"Dodano część '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować drzwi {variant.name}: {e}");
                return null;
            }
        }

        private static PartSettings Build(HullPartSettings door, PartSettings chest, (ushort id, string name, int width, int height, int iron) v)
        {
            GameObject root = Object.Instantiate(door.part, PartFactory.Holder);
            root.name = "MiningDoor" + v.width + "x" + v.height;
            Scale(root, v.width / door.size.x, v.height / door.size.y);
            CoverInteraction(root);
            ReplaceLook(root, chest);

            HullPartSettings settings = Object.Instantiate(door);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.id;
            settings.fullLabel = v.name;
            settings.description = $"A {v.width} x {v.height} door. Opens and closes like the normal door and seals the room when closed.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = new Vector3(v.width, v.height, door.size.z);
            settings.part = root;
            // Bez wersji "na skos" z drzwi z gry (byłaby 2x2). Z nią podgląd budowania przy każdym przesunięciu
            // celownika o pole niszczył i tworzył od nowa cały model z obrysem i hologramem (przycięcia).
            settings.pivotedSettings = null;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.iron), (112, Mathf.Max(2, v.iron / 8)));
            return settings;
        }

        // Wygląd z gry zastępujemy własnym modelem (Content/BigDoorModel) z animacją skrzydeł; kolizje i pole interakcji
        // drzwi z gry zostają (powiększone) - dzięki nim działa blokowanie przejścia, E i szczelność.
        private static void ReplaceLook(GameObject root, PartSettings chest)
        {
            MeshRenderer look = chest.part.transform.Find("Visual")?.GetComponentInChildren<MeshRenderer>(true) ?? chest.part.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "BigDoor");

            var visuals = root.GetComponentInChildren<DefaultShipPartVisuals>(true);
            Transform host = visuals != null ? visuals.transform : root.transform;
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            if (visuals != null) Object.DestroyImmediate(visuals);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(r);
            host.gameObject.AddComponent<BigDoorVisuals>();

            // Gra tworzy w statku kopię samego obiektu wyglądu (bez rodzica), więc model jest jego dzieckiem,
            // ustawionym tak, żeby jego układ był układem całej części.
            var bounds = root.GetComponent<ShipPartBounds>();
            Matrix4x4 hostToRoot = root.transform.worldToLocalMatrix * host.localToWorldMatrix;
            Transform model = BigDoorModel.Build(host, bounds.GetBounds(), material, look);
            Matrix4x4 inv = hostToRoot.inverse;
            model.localPosition = inv.GetColumn(3);
            model.localRotation = inv.rotation;
            model.localScale = inv.lossyScale;
        }

        // Strefa F (E) na całą powierzchnię drzwi i grubsza od ich kolizji (0.2 m) - promień z każdej strony i z każdego
        // miejsca trafia najpierw w nią, a nie w drzwi (po samym przeskalowaniu była cienka i w proporcjach drzwi 2x2).
        private static void CoverInteraction(GameObject root)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Transform interactions = root.transform.Find("Interactions");
            if (interactions == null)
            {
                Melon<MiningMod>.Logger.Warning($"Drzwi {root.name}: brak obiektu Interactions - strefa F bez zmian");
                return;
            }
            Vector3 size = new Vector3(bounds.bounds.x, bounds.bounds.y, Mathf.Max(bounds.bounds.z, 0.2f) + 0.5f);
            foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
            {
                Transform t = box.transform;
                Vector3 lossy = t.lossyScale, rootScale = root.transform.lossyScale;
                box.center = t.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
                box.size = new Vector3(size.x * rootScale.x / lossy.x, size.y * rootScale.y / lossy.y, size.z * rootScale.z / lossy.z);
            }
        }

        // Kolizje i pole interakcji rosną w osiach X (szerokość) i Y (wysokość); lewy dolny róg zostaje na miejscu
        // (część zajmuje pola od pola położenia w prawo i w górę). Gra kopiuje osobno każdy obiekt z korzenia części
        // (wygląd, kolizje drzwi, interakcje) - bez rodzica - więc skalujemy wewnątrz każdego z nich.
        private static void Scale(GameObject root, float sx, float sy)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c0 = bounds.center, b0 = bounds.bounds;
            var s = new Vector3(sx, sy, 1f);
            Vector3 min0 = c0 - b0 * 0.5f;
            Vector3 b1 = Vector3.Scale(b0, s);
            Vector3 c1 = min0 + b1 * 0.5f;
            bounds.center = c1;
            bounds.bounds = b1;

            foreach (Transform unit in root.transform.Cast<Transform>().ToArray())
            {
                // punkt p w układzie obiektu: w układzie części to unit.pos + p; ma trafić w c1 + S(unit.pos + p - c0)
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
                Melon<MiningMod>.Logger.Msg($"Drzwi {root.name}: {unit.name} pos={unit.localPosition} rot={unit.localEulerAngles} scale={unit.localScale}");
            }
            Melon<MiningMod>.Logger.Msg($"Drzwi {root.name}: obrys {c0}/{b0} -> {c1}/{b1}");
        }
    }
}
