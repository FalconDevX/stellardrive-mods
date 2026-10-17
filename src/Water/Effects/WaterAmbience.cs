using System;
using System.IO;
using System.Reflection;
using Core.Values;
using MelonLoader;
using UnityEngine;
using Water.Buoyancy;
using Water.Swimming;
using Water.Terrain;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Random = UnityEngine.Random;

namespace Water.Effects
{
    internal static class WaterAmbience
    {
        private const float ShoreRange = 40f;
        private const float MaxShoreAltitude = 30f;
        private static readonly float[] Radii = { 3f, 7f, 13f, 21f, 30f, 40f };
        private const int Directions = 12;

        private static FMOD.Sound[] _strokes, _laps;
        private static bool _created, _failed;
        private static float _nextStroke, _nextLap, _nextScan;
        private static float _shoreLevel;
        private static Vector3 _shoreVisual;

        internal static void Clear()
        {
            _shoreLevel = 0f;
            _nextScan = 0f;
        }

        internal static void Update()
        {
            float volume = Mathf.Clamp01(WaterMod.SplashVolume.Value);
            if (volume <= 0f || !Ensure())
            {
                return;
            }
            Strokes(volume);
            Shore(volume);
        }

        private static void Strokes(float volume)
        {
            if (!SwimmingPatch.LocalSwimming || SwimmingPatch.LocalSpeed < 0.8f)
            {
                _nextStroke = Mathf.Max(_nextStroke, Time.time + 0.15f);
                return;
            }
            if (Time.time < _nextStroke)
            {
                return;
            }
            float speed = SwimmingPatch.LocalSpeed;
            bool under = SwimmingPatch.LocalDepth > 1.7f;
            _nextStroke = Time.time + Mathf.Lerp(1.25f, 0.7f, Mathf.InverseLerp(2f, 5.5f, speed)) * Random.Range(0.9f, 1.1f);
            float v = volume * Mathf.Lerp(0.3f, 0.55f, Mathf.InverseLerp(1f, 5.5f, speed)) * (under ? 0.55f : 1f);
            float pitch = Random.Range(0.92f, 1.08f) * (under ? 0.7f : 1f);
            SplashSound.Play(_strokes[Random.Range(0, _strokes.Length)], v, pitch, Random.Range(-0.15f, 0.15f));
        }

        private static void Shore(float volume)
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.5f;
                _shoreLevel = Scan(out _shoreVisual);
            }
            if (_shoreLevel <= 0.02f || Time.time < _nextLap)
            {
                return;
            }
            _nextLap = Time.time + Random.Range(1.1f, 3.2f);
            float v = volume * 0.3f * _shoreLevel * Random.Range(0.7f, 1f);
            SplashSound.Play(_laps[Random.Range(0, _laps.Length)], v, Random.Range(0.85f, 1.1f), SplashSound.Pan(_shoreVisual));
        }

        private static float Scan(out Vector3 shoreVisual)
        {
            shoreVisual = Vector3.zero;
            Camera cam = Camera.main;
            if (!WaterMod.Enabled.Value || cam == null || !(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || space.PlanetId != WaterLevel.PlanetId || !WaterLevel.Ready())
            {
                return 0f;
            }
            var transpose = ServiceCache.Get<IWorldTransposeClient>();
            if (transpose == null)
            {
                return 0f;
            }
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Vector3 camPlanet = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)cam.transform.position);
            float altitude = camPlanet.magnitude - WaterLevel.SeaRadius;
            if (altitude > MaxShoreAltitude || altitude < -2f)
            {
                return 0f;
            }
            Vector3 up = camPlanet.normalized;
            bool wet = WaterLevel.Ground(up) < LakeTerrainPatch.SeaLevel;
            Vector3 t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(up, t1);
            float sea = WaterLevel.SeaRadius;
            foreach (float r in Radii)
            {
                for (int i = 0; i < Directions; i++)
                {
                    float a = (i + (r * 0.37f % 1f)) * Mathf.PI * 2f / Directions;
                    Vector3 dir = (up * sea + (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * r).normalized;
                    if (WaterLevel.Ground(dir) < LakeTerrainPatch.SeaLevel == wet)
                    {
                        continue;
                    }
                    Vector3 shore = wet ? up * sea + (dir * sea - up * sea) * 0.5f : dir * sea;
                    shoreVisual = (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, (Vector3Double)shore);
                    float distance = Mathf.Sqrt(r * r + altitude * altitude);
                    return Mathf.Clamp01(1f - distance / ShoreRange) * Mathf.Clamp01(1f - altitude / MaxShoreAltitude);
                }
            }
            return 0f;
        }

        private static bool Ensure()
        {
            if (_created || _failed)
            {
                return _created;
            }
            try
            {
                if (!FMODUnity.RuntimeManager.IsInitialized)
                {
                    return false;
                }
                _strokes = new[] { Load("swim1"), Load("swim2"), Load("swim3") };
                _laps = new[] { Load("shore1"), Load("shore2") };
                _created = true;
                Melon<WaterMod>.Logger.Msg("Water: swimming and shore sounds ready");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Warning($"Could not prepare swimming and shore sounds: {e.Message}");
            }
            return _created;
        }

        private static FMOD.Sound Load(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Water.Sounds.{name}.mp3"))
            {
                if (s == null)
                {
                    throw new Exception($"missing sound {name}");
                }
                var data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                return SplashSound.Create(data, FMOD.MODE.LOOP_OFF);
            }
        }
    }
}
