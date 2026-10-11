using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using HarmonyLib;
using Planets;
using Planets.Network;
using UnityEngine;
using UnityEngine.Rendering;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Mining.Sites
{
    // Pokazuje złoża w pobliżu kamery. Każde złoże to jedna siatka złożona z kilkudziesięciu pomniejszonych,
    // spłaszczonych kopii kamienia rudy z gry (ten sam materiał i kontury co prawdziwe skały), wtopionych w teren.
    internal static class MiningSiteVisuals
    {
        private const float ShowDistance = 450f;
        private const float HideDistance = 550f;

        private sealed class Spawned
        {
            internal MiningSite Site;
            internal GameObject Object;
        }

        private static readonly Dictionary<long, Spawned> Active = new Dictionary<long, Spawned>();
        private sealed class Rock
        {
            internal Mesh Mesh;
            internal Material[] Materials;
            internal int Layer;
            internal Matrix4x4 Base; // położenie siatki względem korzenia prefabu (modele z FBX bywają obrócone/przeskalowane)
            internal Bounds Bounds;  // granice siatki w układzie korzenia prefabu
        }

        private static readonly Dictionary<GameObject, Rock> RockCache = new Dictionary<GameObject, Rock>();
        private static float _nextScan;
        private static ulong _planetId;

        internal static void Clear()
        {
            foreach (Spawned s in Active.Values)
            {
                if (s.Object != null) Object.Destroy(s.Object);
            }
            Active.Clear();
        }

        internal static void Update()
        {
            if (!MiningMod.ShowSites.Value)
            {
                return;
            }
            if (!(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || Camera.main == null)
            {
                if (Active.Count > 0) Clear();
                return;
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            if (transpose == null)
            {
                return;
            }
            if (space.PlanetId != _planetId)
            {
                Clear();
                _planetId = space.PlanetId;
            }

            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                Vector3 camPlanet = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)Camera.main.transform.position);
                Scan(space.PlanetId, camPlanet);
            }

            // Pozycje co klatkę (pływające origin gry przesuwa świat wokół gracza).
            foreach (Spawned s in Active.Values)
            {
                if (s.Object == null) continue;
                s.Object.transform.position = (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, (Vector3Double)s.Site.Center);
                s.Object.transform.rotation = transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, s.Site.Rotation);
            }
        }

        private static void Scan(ulong planetId, Vector3 camPlanet)
        {
            float cell = MiningSites.CellSize(planetId);
            if (cell <= 0f)
            {
                return;
            }
            int range = Mathf.CeilToInt(ShowDistance / cell) + 1;
            foreach (MiningSite site in MiningSites.Near(planetId, camPlanet, range))
            {
                if (!Active.ContainsKey(site.Key) && (site.Center - camPlanet).magnitude < ShowDistance)
                {
                    Active[site.Key] = new Spawned { Site = site, Object = Build(site) };
                }
            }
            foreach (long key in Active.Keys.ToList())
            {
                Spawned s = Active[key];
                if ((s.Site.Center - camPlanet).magnitude > HideDistance)
                {
                    if (s.Object != null) Object.Destroy(s.Object);
                    Active.Remove(key);
                }
            }
        }

        internal static GameObject Build(MiningSite site)
        {
            Rock rockInfo = GetRock(site.Kind.RockPrefab);
            var go = new GameObject($"MiningSite_{site.Kind.Key}");
            Object.DontDestroyOnLoad(go);
            if (rockInfo == null)
            {
                return go;
            }

            var tracked = TrackedPlanetFor(site.PlanetId);
            var rng = new System.Random(site.Seed);
            Vector3 rockSize = rockInfo.Bounds.size;
            float rockDiameter = Mathf.Max(0.05f, Mathf.Max(rockSize.x, rockSize.z));
            Quaternion inv = Quaternion.Inverse(site.Rotation);

            BuildGround(site, go, rockInfo, tracked, inv);

            int count = Mathf.RoundToInt(site.Radius * site.Radius * 5.5f);
            for (int n = 0; n < count; n++)
            {
                // Gęściej w środku, rzadziej na brzegu (jak plamy rudy w Factorio).
                float r = site.Radius * Mathf.Pow((float)rng.NextDouble(), 0.65f);
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float edge = 1f - r / site.Radius;
                float size = Mathf.Lerp(0.25f, 0.85f, (float)rng.NextDouble() * (0.3f + 0.7f * edge)) / rockDiameter;

                Vector3 local = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 planetPoint = site.Center + site.Rotation * local;
                if (tracked != null)
                {
                    Vector3 dir = planetPoint.normalized;
                    planetPoint = dir * tracked.PointsProvider.GetElevationAtPoint(dir);
                }
                Vector3 p = inv * (planetPoint - site.Center);
                var scale = new Vector3(size, size * Mathf.Lerp(0.45f, 0.8f, (float)rng.NextDouble()), size);
                p.y -= (rockInfo.Bounds.min.y + rockSize.y * 0.2f) * scale.y; // częściowo w ziemi

                var rock = new GameObject("Rock") { layer = rockInfo.Layer };
                rock.transform.SetParent(go.transform, false);
                rock.transform.localPosition = p;
                rock.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 16f - 8f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 16f - 8f);
                rock.transform.localScale = scale;
                var model = new GameObject("Model") { layer = rockInfo.Layer };
                model.transform.SetParent(rock.transform, false);
                model.transform.localPosition = rockInfo.Base.GetColumn(3);
                model.transform.localRotation = rockInfo.Base.rotation;
                model.transform.localScale = rockInfo.Base.lossyScale;
                model.AddComponent<MeshFilter>().sharedMesh = rockInfo.Mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = rockInfo.Materials;
                renderer.shadowCastingMode = size * rockDiameter > 0.35f ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return go;
        }

        // Kolor żwiru pod kamieniami: ciemna ziemia z drobinkami w kolorze rudy.
        private static readonly Dictionary<string, (Color dirt, Color ore)> GroundColors = new Dictionary<string, (Color, Color)>
        {
            { "Iron", (new Color(0.30f, 0.24f, 0.21f), new Color(0.62f, 0.36f, 0.26f)) },
            { "Copper", (new Color(0.32f, 0.25f, 0.18f), new Color(0.85f, 0.50f, 0.25f)) },
            { "Aluminum", (new Color(0.36f, 0.35f, 0.34f), new Color(0.82f, 0.84f, 0.86f)) },
            { "Glass", (new Color(0.30f, 0.32f, 0.34f), new Color(0.45f, 0.70f, 0.95f)) },
            { "Ice", (new Color(0.40f, 0.44f, 0.48f), new Color(0.90f, 0.96f, 1.00f)) },
        };

        private static readonly Dictionary<string, Material> GroundMaterials = new Dictionary<string, Material>();

        // Płaska, nieregularna plama żwiru dopasowana do terenu (kilka cm nad ziemią), żeby złoże było widać z daleka.
        private static void BuildGround(MiningSite site, GameObject parent, Rock rock, TrackedPlanet tracked, Quaternion inv)
        {
            const int rings = 7, segments = 28;
            var rng = new System.Random(site.Seed ^ 0x5A5A);
            float[] edge = new float[segments];
            for (int k = 0; k < segments; k++) edge[k] = 1.05f + 0.25f * (float)rng.NextDouble();
            for (int pass = 0; pass < 2; pass++) // wygładzenie brzegu
            {
                float[] e = (float[])edge.Clone();
                for (int k = 0; k < segments; k++) edge[k] = (e[(k + segments - 1) % segments] + e[k] * 2f + e[(k + 1) % segments]) / 4f;
            }

            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { Vector2.zero };
            var tris = new List<int>();
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int k = 0; k < segments; k++)
                {
                    float a = k * Mathf.PI * 2f / segments;
                    float r = site.Radius * edge[k] * ring / rings;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r) * 0.35f);
                }
            }
            for (int k = 0; k < segments; k++)
            {
                tris.Add(0); tris.Add(1 + (k + 1) % segments); tris.Add(1 + k);
            }
            for (int ring = 1; ring < rings; ring++)
            {
                int a0 = 1 + (ring - 1) * segments, b0 = 1 + ring * segments;
                for (int k = 0; k < segments; k++)
                {
                    int k1 = (k + 1) % segments;
                    tris.Add(a0 + k); tris.Add(a0 + k1); tris.Add(b0 + k);
                    tris.Add(a0 + k1); tris.Add(b0 + k1); tris.Add(b0 + k);
                }
            }
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 planetPoint = site.Center + site.Rotation * verts[i];
                if (tracked != null)
                {
                    Vector3 dir = planetPoint.normalized;
                    planetPoint = dir * (tracked.PointsProvider.GetElevationAtPoint(dir) + 0.1f);
                }
                verts[i] = inv * (planetPoint - site.Center);
            }
            var mesh = new Mesh { name = "MiningSiteGround" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, Enumerable.Repeat(Content.ModAssets.PaletteUv, verts.Count).ToList());
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.Lerp(normals[i], Vector3.up, 0.6f).normalized;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground") { layer = rock.Layer };
            ground.transform.SetParent(parent.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetGroundMaterial(site.Kind.Key, rock.Materials.FirstOrDefault());
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material GetGroundMaterial(string key, Material template)
        {
            if (GroundMaterials.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }
            (Color dirt, Color ore) colors = GroundColors.TryGetValue(key, out var c) ? c : (new Color(0.3f, 0.27f, 0.24f), new Color(0.6f, 0.6f, 0.6f));
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "MiningGravel_" + key, wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(key.Length * 7919);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Color.Lerp(colors.dirt * 0.85f, colors.dirt * 1.15f, (float)rng.NextDouble());
            }
            // Drobinki rudy i kamyczki (kilkupikselowe plamki, zawijane na brzegach tekstury).
            for (int s = 0; s < 260; s++)
            {
                int cx = rng.Next(size), cy = rng.Next(size), r = 1 + rng.Next(3);
                Color col = rng.NextDouble() < 0.55 ? colors.ore * (0.8f + 0.4f * (float)rng.NextDouble()) : colors.dirt * 0.6f;
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy <= r * r)
                        {
                            px[((cy + dy + size) % size) * size + (cx + dx + size) % size] = col;
                        }
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);

            Material mat = template != null ? new Material(template) : new Material(Shader.Find("Custom/PlanetObject"));
            mat.name = tex.name;
            if (mat.HasProperty("_Albedo")) mat.SetTexture("_Albedo", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Normal")) mat.SetTexture("_Normal", null);
            if (mat.HasProperty("_RoughnessMap")) mat.SetTexture("_RoughnessMap", null);
            if (mat.HasProperty("_Metalness")) mat.SetFloat("_Metalness", 0f);
            GroundMaterials[key] = mat;
            return mat;
        }

        private static Rock GetRock(GameObject prefab)
        {
            if (RockCache.TryGetValue(prefab, out Rock cached))
            {
                return cached;
            }
            MeshFilter filter = prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null)
                .OrderByDescending(f => f.sharedMesh.vertexCount).FirstOrDefault();
            Rock rock = null;
            if (filter != null)
            {
                Matrix4x4 baseMatrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = filter.sharedMesh.bounds;
                var bounds = new Bounds(baseMatrix.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(baseMatrix.MultiplyPoint3x4(corner));
                }
                rock = new Rock
                {
                    Mesh = filter.sharedMesh,
                    Materials = filter.GetComponent<MeshRenderer>().sharedMaterials,
                    Layer = filter.gameObject.layer,
                    Base = baseMatrix,
                    Bounds = bounds,
                };
                MelonLoader.Melon<MiningMod>.Logger.Msg($"Kamień złoża {prefab.name}: siatka {filter.sharedMesh.name}, rozmiar {bounds.size}");
            }
            else
            {
                MelonLoader.Melon<MiningMod>.Logger.Warning($"Kamień {prefab.name} nie ma siatki - złoża tego typu będą niewidoczne");
            }
            RockCache[prefab] = rock;
            return rock;
        }

        private static TrackedPlanet TrackedPlanetFor(ulong planetId)
        {
            foreach (MonoBehaviour tracker in new MonoBehaviour[] { Object.FindObjectOfType<Planets.Services.PlanetsClientTracker>(), Object.FindObjectOfType<Planets.Services.PlanetsServerTracker>() })
            {
                if (tracker != null && Traverse.Create(tracker).Field("_planets").GetValue() is System.Collections.IDictionary planets)
                {
                    foreach (System.Collections.DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet p && p.Id == planetId) return p;
                    }
                }
            }
            return null;
        }

        internal static MiningSite FindNearest(float maxDistance, out float distance)
        {
            distance = 0f;
            if (!(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || Camera.main == null)
            {
                return null;
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Vector3 cam = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)Camera.main.transform.position);
            float cell = MiningSites.CellSize(space.PlanetId);
            if (cell <= 0f)
            {
                return null;
            }
            MiningSite nearest = MiningSites.Near(space.PlanetId, cam, Mathf.CeilToInt(maxDistance / cell)).OrderBy(s => (s.Center - cam).sqrMagnitude).FirstOrDefault();
            if (nearest != null) distance = (nearest.Center - cam).magnitude;
            return nearest;
        }

        // Najbliższe złoże do kamery - do komunikatu na ekranie.
        internal static string DescribeNearest()
        {
            if (!(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || Camera.main == null)
            {
                return "Nie jesteś na planecie.";
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Vector3 cam = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)Camera.main.transform.position);
            float cell = MiningSites.CellSize(space.PlanetId);
            if (cell <= 0f)
            {
                return "Brak danych o planecie.";
            }
            MiningSite nearest = MiningSites.Near(space.PlanetId, cam, Mathf.CeilToInt(2000f / cell)).OrderBy(s => (s.Center - cam).sqrMagnitude).FirstOrDefault();
            if (nearest == null)
            {
                return "W promieniu 2 km nie ma złóż.";
            }
            Vector3 visual = (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, (Vector3Double)nearest.Center);
            Vector3 toSite = visual - Camera.main.transform.position;
            Vector3 up = cam.normalized;
            up = transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * up;
            Vector3 flatTo = Vector3.ProjectOnPlane(toSite, up);
            Vector3 flatFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, up);
            float angle = Vector3.SignedAngle(flatFwd, flatTo, up);
            string side = Mathf.Abs(angle) < 25f ? "przed tobą" : Mathf.Abs(angle) > 155f ? "za tobą" : angle > 0 ? "w prawo" : "w lewo";
            return $"Najbliższe złoże: {nearest.Kind.Name}, {flatTo.magnitude:0} m {side}";
        }
    }
}
