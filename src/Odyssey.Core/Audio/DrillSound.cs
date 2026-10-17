using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Odyssey.Audio
{
    internal static class DrillSound
    {
        private static FMOD.Sound _sound;
        private static bool _created;
        private static bool _failed;

        internal static bool TryGet(out FMOD.Sound sound)
        {
            sound = _sound;
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
                FMOD.System core = FMODUnity.RuntimeManager.CoreSystem;
                FMOD.MODE mode = FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE;
                string source;
                byte[] data = LoadOverride(out source) ?? LoadEmbedded(out source);
                if (data == null)
                {
                    data = Synthesize();
                    source = "generated";
                }
                var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
                FMOD.RESULT result = core.createSound(data, mode | FMOD.MODE.OPENMEMORY, ref info, out _sound);
                if (result != FMOD.RESULT.OK)
                {
                    throw new Exception($"createSound: {result}");
                }
                _created = true;
                sound = _sound;
                Melon<CoreMod>.Logger.Msg($"Drill sound: {source}");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<CoreMod>.Logger.Warning($"Could not prepare the drill sound: {e.Message}");
            }
            return _created;
        }

        internal static FMOD.Channel Play(FMOD.Sound sound)
        {
            FMOD.System core = FMODUnity.RuntimeManager.CoreSystem;
            core.getMasterChannelGroup(out FMOD.ChannelGroup master);
            core.playSound(sound, master, true, out FMOD.Channel channel);
            channel.setPriority(0);
            channel.setVolume(0f);
            channel.setPaused(false);
            return channel;
        }

        internal static bool IsAlive(FMOD.Channel channel) => channel.isPlaying(out bool playing) == FMOD.RESULT.OK && playing;

        private static byte[] LoadOverride(out string source)
        {
            source = null;
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining");
            foreach (string ext in new[] { "wav", "ogg", "mp3" })
            {
                string path = Path.Combine(dir, "drill." + ext);
                if (File.Exists(path))
                {
                    source = path;
                    return File.ReadAllBytes(path);
                }
            }
            return null;
        }

        private static byte[] LoadEmbedded(out string source)
        {
            source = null;
            foreach (string ext in new[] { "wav", "ogg", "mp3" })
            {
                byte[] data = Content.ModAssets.Read("drill_loop." + ext);
                if (data != null)
                {
                    source = "drill_loop." + ext;
                    return data;
                }
            }
            return null;
        }

        private static byte[] Synthesize()
        {
            const int rate = 44100;
            const float seconds = 2f;
            int n = (int)(rate * seconds);
            var samples = new float[n];
            var rng = new System.Random(1234);

            var noise = new float[n];
            float lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.25f * (white - lp);
                bp = 0.85f * bp + (white - lp);
                noise[i] = bp * 0.5f + lp * 0.8f;
            }
            int fade = rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                noise[i] = noise[i] * t + noise[n - fade + i] * (1 - t);
            }

            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double motor = 0;
                double baseHz = 92.5;
                double wobble = 0.004 * Math.Sin(2 * Math.PI * 1.5 * t);
                for (int h = 1; h <= 8; h++)
                {
                    motor += Math.Sin(2 * Math.PI * baseHz * h * t * (1 + wobble)) / (h * 1.2);
                }
                double whine = 0.18 * Math.Sin(2 * Math.PI * 740 * t) + 0.08 * Math.Sin(2 * Math.PI * 1480.5 * t);
                double spin = 0.55 + 0.45 * Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * 14 * t), 3);
                double grind = noise[i] * spin * 0.9;
                samples[i] = (float)(motor * 0.35 + whine * 0.25 + grind * 0.55);
            }

            float peak = 0.001f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + n * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(n * 2);
                foreach (float s in samples)
                {
                    w.Write((short)(s / peak * 0.8f * short.MaxValue));
                }
                return ms.ToArray();
            }
        }
    }
}
