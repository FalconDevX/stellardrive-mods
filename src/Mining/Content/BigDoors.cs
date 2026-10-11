using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Duże drzwi (3x3, 4x4, 6x3, 10x5) - kopie drzwi z gry (2x2) z większym rozmiarem w siatce i przeskalowanym wyglądem.
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
            var door = PartFactory.Find(gameParts, DoorPartId) as HullPartSettings;
            if (door?.part == null)
            {
                return null;
            }
            var variant = Variants.First(v => v.id == id);
            try
            {
                PartSettings part = Build(door, variant);
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

        private static PartSettings Build(HullPartSettings door, (ushort id, string name, int width, int height, int iron) v)
        {
            GameObject root = Object.Instantiate(door.part, PartFactory.Holder);
            root.name = "MiningDoor" + v.width + "x" + v.height;
            Scale(root, v.width / door.size.x, v.height / door.size.y);

            HullPartSettings settings = Object.Instantiate(door);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.id;
            settings.fullLabel = v.name;
            settings.description = $"A {v.width} x {v.height} door. Opens and closes like the normal door and seals the room when closed.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = new Vector3(v.width, v.height, door.size.z);
            settings.part = root;
            if (settings.pivotedSettings == door)
            {
                settings.pivotedSettings = null; // obrócona wersja drzwi z gry miałaby 2x2
            }
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.iron), (112, Mathf.Max(2, v.iron / 8)));
            return settings;
        }

        // Wygląd, kolizje i pole interakcji rosną w osiach X (szerokość) i Y (wysokość); lewy dolny róg zostaje
        // na miejscu (część zajmuje pola od pola położenia w prawo i w górę). Wszystko wkładamy do obiektu "Scaled",
        // a animator drzwi (jeśli był na korzeniu) przenosimy na niego, żeby ścieżki animacji dalej pasowały.
        private static void Scale(GameObject root, float sx, float sy)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c0 = bounds.center, b0 = bounds.bounds;
            var s = new Vector3(sx, sy, 1f);
            Vector3 min0 = c0 - b0 * 0.5f;
            Vector3 b1 = Vector3.Scale(b0, s);
            Vector3 c1 = min0 + b1 * 0.5f;

            Transform scaled = new GameObject("Scaled").transform;
            scaled.SetParent(root.transform, false);
            foreach (Transform child in root.transform.Cast<Transform>().Where(t => t != scaled).ToArray())
            {
                child.SetParent(scaled, false);
            }
            scaled.localPosition = c1 - Vector3.Scale(s, c0);
            scaled.localScale = s;
            bounds.center = c1;
            bounds.bounds = b1;

            Animator rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null)
            {
                Animator moved = scaled.gameObject.AddComponent<Animator>();
                moved.runtimeAnimatorController = rootAnimator.runtimeAnimatorController;
                moved.avatar = rootAnimator.avatar;
                moved.cullingMode = rootAnimator.cullingMode;
                moved.updateMode = rootAnimator.updateMode;
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    foreach (var field in AccessTools.GetDeclaredFields(c.GetType()).Where(f => f.FieldType == typeof(Animator)))
                    {
                        if ((Animator)field.GetValue(c) == rootAnimator)
                        {
                            field.SetValue(c, moved);
                        }
                    }
                }
                Object.DestroyImmediate(rootAnimator);
            }
            Melon<MiningMod>.Logger.Msg($"Drzwi {root.name}: obrys {c0}/{b0} -> {c1}/{b1}, animator na korzeniu: {rootAnimator != null}");
        }
    }
}
