using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Mining.Audio
{
    // Dźwięk w pętli przez rdzeń FMOD-a (dźwięk Unity jest w grze wyłączony), jak DrillSound.
    // Źródło: plik UserData/Mining/<nazwa>.(wav|ogg|mp3) albo osadzony Assets/<nazwa>_loop.wav.
    internal static class LoopSound
    {
        private static readonly Dictionary<string, FMOD.Sound> Sounds = new Dictionary<string, FMOD.Sound>();
        private static readonly HashSet<string> Failed = new HashSet<string>();

        // loop = false: dźwięk jednorazowy, osadzony jako Assets/<nazwa>.wav (OneShot).
        internal static bool TryGet(string name, out FMOD.Sound sound, bool loop = true)
        {
            if (Sounds.TryGetValue(name, out sound))
            {
                return true;
            }
            if (Failed.Contains(name) || !FMODUnity.RuntimeManager.IsInitialized)
            {
                return false;
            }
            try
            {
                string source = null;
                byte[] data = null;
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining");
                foreach (string ext in new[] { "wav", "ogg", "mp3" })
                {
                    string path = Path.Combine(dir, name + "." + ext);
                    if (File.Exists(path))
                    {
                        data = File.ReadAllBytes(path);
                        source = path;
                        break;
                    }
                }
                if (data == null)
                {
                    source = loop ? name + "_loop.wav" : name + ".wav";
                    data = Content.ModAssets.Read(source);
                }
                if (data == null)
                {
                    throw new FileNotFoundException(name);
                }
                var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
                FMOD.MODE mode = (loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF) | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.OPENMEMORY;
                FMOD.RESULT result = FMODUnity.RuntimeManager.CoreSystem.createSound(data, mode, ref info, out sound);
                if (result != FMOD.RESULT.OK)
                {
                    throw new Exception($"createSound: {result}");
                }
                Sounds[name] = sound;
                Melon<MiningMod>.Logger.Msg($"Dźwięk {name}: {source}");
                return true;
            }
            catch (Exception e)
            {
                Failed.Add(name);
                Melon<MiningMod>.Logger.Warning($"Nie udało się przygotować dźwięku {name}: {e.Message}");
                return false;
            }
        }
    }

    // Jedna pętla przypięta do obiektu: głośność zależna od odległości od kamery, panorama lewo/prawo.
    internal sealed class LoopEmitter
    {
        private readonly string _name;
        private readonly float _hearDistance;
        private readonly MelonPreferences_Entry<float> _volume;
        private FMOD.Channel _channel;
        private bool _playing;

        internal static Camera ListenerOverride; // tylko testy twórcy moda (kamera testowa zamiast głównej)

        internal LoopEmitter(string name, float hearDistance, MelonPreferences_Entry<float> volume = null)
        {
            _name = name;
            _hearDistance = hearDistance;
            _volume = volume;
        }

        internal void Update(Vector3 position, float level, float pitch)
        {
            Camera cam = ListenerOverride != null ? ListenerOverride : Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / _hearDistance);
            float volume = level * attenuation * attenuation * (_volume ?? MiningMod.PumpVolume).Value;
            if (volume <= 0.001f)
            {
                Stop();
                return;
            }
            if (!_playing)
            {
                if (!LoopSound.TryGet(_name, out FMOD.Sound sound))
                {
                    return;
                }
                FMOD.System core = FMODUnity.RuntimeManager.CoreSystem;
                core.getMasterChannelGroup(out FMOD.ChannelGroup master);
                core.playSound(sound, master, true, out _channel);
                _channel.setPosition((uint)UnityEngine.Random.Range(0, 20000), FMOD.TIMEUNIT.PCM); // kilka pomp nie gra w fazie
                _channel.setPaused(false);
                _playing = true;
            }
            _channel.setVolume(volume);
            _channel.setPitch(pitch);
            if (cam != null)
            {
                Vector3 local = cam.transform.InverseTransformPoint(position);
                _channel.setPan(Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f);
            }
        }

        internal void Stop()
        {
            if (_playing)
            {
                _channel.stop();
                _playing = false;
            }
        }
    }

    // Dźwięk jednorazowy w miejscu obiektu (głośność od odległości od kamery, panorama) - np. uderzenie dużych drzwi.
    internal static class OneShot
    {
        internal static void Play(string name, Vector3 position, float hearDistance, float volume, float pitch)
        {
            Camera cam = LoopEmitter.ListenerOverride != null ? LoopEmitter.ListenerOverride : Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / hearDistance);
            float v = volume * attenuation * attenuation;
            if (v <= 0.001f || !LoopSound.TryGet(name, out FMOD.Sound sound, false))
            {
                return;
            }
            FMOD.System core = FMODUnity.RuntimeManager.CoreSystem;
            core.getMasterChannelGroup(out FMOD.ChannelGroup master);
            core.playSound(sound, master, true, out FMOD.Channel channel);
            channel.setVolume(v);
            channel.setPitch(pitch);
            if (cam != null)
            {
                Vector3 local = cam.transform.InverseTransformPoint(position);
                channel.setPan(Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f);
            }
            channel.setPaused(false);
        }
    }
}
