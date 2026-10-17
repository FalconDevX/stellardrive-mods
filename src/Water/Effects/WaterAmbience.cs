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

        private static FMOD.Sound[] _strokes;
        private static FMOD.Sound _shoreLoop;
        private static FMOD.Channel _shoreChannel;
        private static bool _hasShoreChannel;
        private static bool _created, _failed;
        private static float _nextStroke, _nextScan;
        private static float _shoreLevel, _shoreVolume, _shorePan;
        private static Vector3 _shoreVisual;

        internal static void Clear()
        {
            _shoreLevel = 0f;
            _shoreVolume = 0f;
            _nextScan = 0f;
            StopShore();
        }

        internal static void Update()
        {
            float volume = Mathf.Clamp01(WaterMod.SplashVolume.Value);
            if (volume <= 0f || !Ensure())
            {
                StopShore();
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
                _nextScan = Time.time + 0.4f;
                _shoreLevel = Scan(out _shoreVisual);
            }
            float target = volume * 0.55f * _shoreLevel * (SwimmingPatch.LocalSwimming && SwimmingPatch.LocalDepth > 1.7f ? 0.35f : 1f);
            _shoreVolume = Mathf.MoveTowards(_shoreVolume, target, Time.deltaTime * 0.35f);
            if (_shoreVolume <= 0.002f)
            {
                StopShore();
                return;
            }
            if (!_hasShoreChannel || !(_shoreChannel.isPlaying(out bool playing) == FMOD.RESULT.OK && playing))
            {
                _shoreChannel = SplashSound.Play(_shoreLoop, 0f, 1f, 0f);
                _shoreLoop.getLength(out uint length, FMOD.TIMEUNIT.MS);
                _shoreChannel.setPosition((uint)Random.Range(0, (int)length), FMOD.TIMEUNIT.MS);
                _hasShoreChannel = true;
            }
            if (_shoreLevel > 0.02f)
            {
                _shorePan = Mathf.MoveTowards(_shorePan, SplashSound.Pan(_shoreVisual) * 0.7f, Time.deltaTime * 0.6f);
            }
            _shoreChannel.setVolume(_shoreVolume);
            _shoreChannel.setPan(_shorePan);
        }

        private static void StopShore()
        {
            if (_hasShoreChannel)
            {
                _shoreChannel.stop();
                _hasShoreChannel = false;
            }
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
                _strokes = new[] { Load("swim1", "mp3", FMOD.MODE.LOOP_OFF), Load("swim2", "mp3", FMOD.MODE.LOOP_OFF), Load("swim3", "mp3", FMOD.MODE.LOOP_OFF) };
                _shoreLoop = Load("shore_loop", "ogg", FMOD.MODE.LOOP_NORMAL);
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

        private static FMOD.Sound Load(string name, string extension, FMOD.MODE loop)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Water.Sounds.{name}.{extension}"))
            {
                if (s == null)
                {
                    throw new Exception($"missing sound {name}");
                }
                var data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                return SplashSound.Create(data, loop);
            }
        }
    }
}
