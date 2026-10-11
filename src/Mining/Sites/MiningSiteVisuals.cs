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
        private const float ShowDistance = 300f;
        private const float HideDistance = 380f;

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
        // Złoża do zbudowania - najwyżej jedno na klatkę, żeby w locie nie było przycięć.
        private static readonly Queue<MiningSite> Pending = new Queue<MiningSite>();

        internal static void Clear()
        {
            foreach (Spawned s in Active.Values)
            {
                DestroySite(s.Object);
            }
            Active.Clear();
            Pending.Clear();
        }

        // Złączona siatka kamieni należy tylko do tego złoża - usuwamy ją razem z obiektem.
        private static void DestroySite(GameObject site)
        {
            if (site == null)
            {
                return;
            }
            var filter = site.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("MiningSite_"))
            {
                Object.Destroy(filter.sharedMesh);
            }
            Object.Destroy(site);
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
            if (Pending.Count > 0)
            {
                MiningSite next = Pending.Dequeue();
                if (Active.TryGetValue(next.Key, out Spawned slot) && slot.Object == null)
                {
                    slot.Object = Build(next);
                }
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
                    Active[site.Key] = new Spawned { Site = site };
                    Pending.Enqueue(site);
                }
            }
            foreach (long key in Active.Keys.ToList())
            {
                Spawned s = Active[key];
                if ((s.Site.Center - camPlanet).magnitude > HideDistance)
                {
                    DestroySite(s.Object);
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

            int count = Mathf.RoundToInt(site.Radius * site.Radius * 5.5f);
            var matrices = new List<Matrix4x4>(count);
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
                // Kamienie nie mają kolizji, więc chodzi się po złożu jak po płaskim terenie.
                var scale = new Vector3(size, size * Mathf.Lerp(0.45f, 0.8f, (float)rng.NextDouble()), size);
                p.y -= (rockInfo.Bounds.min.y + rockSize.y * 0.2f) * scale.y; // częściowo w ziemi
                var rot = Quaternion.Euler((float)rng.NextDouble() * 16f - 8f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 16f - 8f);
                matrices.Add(Matrix4x4.TRS(p, rot, scale) * rockInfo.Base);
            }

            // Wszystkie kamienie złoża w jednej siatce = jedno rysowanie zamiast setek osobnych obiektów.
            if (rockInfo.Mesh.isReadable)
            {
                var model = new GameObject("Rocks") { layer = rockInfo.Layer };
                model.transform.SetParent(go.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = CombineRocks(rockInfo.Mesh, matrices, go.name);
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = MaterialsFor(site, rockInfo);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                return go;
            }
            foreach (Matrix4x4 m in matrices)
            {
                var rock = new GameObject("Rock") { layer = rockInfo.Layer };
                rock.transform.SetParent(go.transform, false);
                rock.transform.localPosition = m.GetColumn(3);
                rock.transform.localRotation = m.rotation;
                rock.transform.localScale = m.lossyScale;
                rock.AddComponent<MeshFilter>().sharedMesh = rockInfo.Mesh;
                rock.AddComponent<MeshRenderer>().sharedMaterials = MaterialsFor(site, rockInfo);
            }
            return go;
        }

        private static Material[] MaterialsFor(MiningSite site, Rock rock) =>
            site.Kind.Glowing ? rock.Materials.Select(Content.Uranium.Glow).ToArray() : rock.Materials;

        private static Mesh CombineRocks(Mesh rock, List<Matrix4x4> matrices, string name)
        {
            var perSubmesh = new CombineInstance[rock.subMeshCount];
            for (int sub = 0; sub < rock.subMeshCount; sub++)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(matrices.Select(m => new CombineInstance { mesh = rock, subMeshIndex = sub, transform = m }).ToArray(), true, true);
                perSubmesh[sub] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }
            var combined = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(perSubmesh, false, false);
            foreach (CombineInstance c in perSubmesh) Object.Destroy(c.mesh);
            combined.RecalculateBounds();
            combined.UploadMeshData(true);
            return combined;
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
                MelonLoader.Melon<MiningMod>.Logger.Msg($"Kamień złoża {prefab.name}: siatka {filter.sharedMesh.name} ({filter.sharedMesh.vertexCount} wierzch., odczyt: {filter.sharedMesh.isReadable}), rozmiar {bounds.size}");
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
                return "You are not on a planet.";
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Vector3 cam = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)Camera.main.transform.position);
            float cell = MiningSites.CellSize(space.PlanetId);
            if (cell <= 0f)
            {
                return "No planet data.";
            }
            MiningSite nearest = MiningSites.Near(space.PlanetId, cam, Mathf.CeilToInt(2000f / cell)).OrderBy(s => (s.Center - cam).sqrMagnitude).FirstOrDefault();
            if (nearest == null)
            {
                return "No ore deposits within 2 km.";
            }
            Vector3 visual = (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, (Vector3Double)nearest.Center);
            Vector3 toSite = visual - Camera.main.transform.position;
            Vector3 up = cam.normalized;
            up = transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * up;
            Vector3 flatTo = Vector3.ProjectOnPlane(toSite, up);
            Vector3 flatFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, up);
            float angle = Vector3.SignedAngle(flatFwd, flatTo, up);
            string side = Mathf.Abs(angle) < 25f ? "ahead" : Mathf.Abs(angle) > 155f ? "behind you" : angle > 0 ? "to the right" : "to the left";
            return $"Nearest ore deposit: {nearest.Kind.Name}, {flatTo.magnitude:0} m {side}";
        }
    }
}
