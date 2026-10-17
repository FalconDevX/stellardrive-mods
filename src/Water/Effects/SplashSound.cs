using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace Water.Effects
{
    internal static class SplashSound
    {
        private const int Rate = 44100;
        private const float MaxHearDistance = 450f;

        private static FMOD.Sound _impact, _wake;
        private static bool _created, _failed;
        private static FMOD.Channel _wakeChannel;
        private static bool _hasWake;

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
                _impact = Create(Impact(), FMOD.MODE.LOOP_OFF);
                _wake = Create(WakeLoop(), FMOD.MODE.LOOP_NORMAL);
                _created = true;
                Melon<WaterMod>.Logger.Msg("Water: splash sounds ready");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Warning($"Could not prepare splash sounds: {e.Message}");
            }
            return _created;
        }

        internal static FMOD.Sound Create(byte[] data, FMOD.MODE loop)
        {
            var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
            FMOD.MODE mode = loop | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.OPENMEMORY;
            FMOD.RESULT result = FMODUnity.RuntimeManager.CoreSystem.createSound(data, mode, ref info, out FMOD.Sound sound);
            if (result != FMOD.RESULT.OK)
            {
                throw new Exception($"createSound: {result}");
            }
            return sound;
        }

        internal static FMOD.Channel Play(FMOD.Sound sound, float volume, float pitch, float pan)
        {
            FMOD.System core = FMODUnity.RuntimeManager.CoreSystem;
            core.getMasterChannelGroup(out FMOD.ChannelGroup master);
            core.playSound(sound, master, true, out FMOD.Channel channel);
            channel.setPriority(0);
            channel.setVolume(volume);
            channel.setPitch(pitch);
            channel.setPan(pan);
            channel.setPaused(false);
            return channel;
        }

        internal static void PlayImpact(Vector3 position, float power, Vector3 listener)
        {
            float volumeSetting = Mathf.Clamp01(WaterMod.SplashVolume.Value);
            float distance = (position - listener).magnitude;
            if (volumeSetting <= 0f || distance > MaxHearDistance || !Ensure())
            {
                return;
            }
            float volume = volumeSetting * Mathf.Clamp01(0.3f + 0.45f * power) / (1f + distance / 25f);
            float pan = Pan(position);
            float pitch = Mathf.Lerp(1.2f, 0.72f, Mathf.Clamp01((power - 0.2f) / 2f)) * UnityEngine.Random.Range(0.93f, 1.07f);
            Play(_impact, volume, pitch, pan);
            if (power > 0.9f)
            {
                Play(_impact, volume * Mathf.Clamp01(power - 0.6f) * 0.6f, pitch * 0.55f, pan);
            }
        }

        internal static void SetWake(float level, float pan)
        {
            float volume = level * Mathf.Clamp01(WaterMod.SplashVolume.Value) * 0.8f;
            if (volume <= 0.003f)
            {
                StopWake();
                return;
            }
            if (!Ensure())
            {
                return;
            }
            if (_hasWake && !(_wakeChannel.isPlaying(out bool playing) == FMOD.RESULT.OK && playing))
            {
                _hasWake = false;
            }
            if (!_hasWake)
            {
                _wakeChannel = Play(_wake, 0f, 1f, 0f);
                _hasWake = true;
            }
            _wakeChannel.setVolume(volume);
            _wakeChannel.setPitch(0.85f + 0.3f * Mathf.Clamp01(level));
            _wakeChannel.setPan(pan);
        }

        internal static void StopWake()
        {
            if (_hasWake)
            {
                _wakeChannel.stop();
                _hasWake = false;
            }
        }

        internal static float Pan(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return 0f;
            }
            Vector3 local = cam.transform.InverseTransformPoint(position);
            return Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f;
        }

        private static byte[] Impact()
        {
            const float seconds = 2.8f;
            int n = (int)(Rate * seconds);
            var s = new float[n];
            var rng = new System.Random(7);
            float lpBody = 0f, lpSlap = 0f, lpRoarA = 0f, lpRoarB = 0f, grain = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lpSlap += 0.6f * (white - lpSlap);
                lpBody += 0.08f * (white - lpBody);
                lpRoarA += 0.3f * (white - lpRoarA);
                lpRoarB += 0.02f * (white - lpRoarB);
                if (rng.NextDouble() < 0.004) grain = (float)rng.NextDouble();
                grain *= 0.9995f;

                float slap = (white - lpSlap * 0.5f) * Attack(t, 0.003f) * Mathf.Exp(-t / 0.07f);
                float body = lpBody * 3.2f * Attack(t, 0.02f) * Mathf.Exp(-t / 0.35f);
                float thumpHz = 46f + 34f * Mathf.Exp(-t / 0.05f);
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t) * Mathf.Exp(-t / 0.16f) * Attack(t, 0.006f);
                float roarEnv = t < 0.45f ? Mathf.SmoothStep(0f, 1f, (t - 0.08f) / 0.37f) : Mathf.Exp(-(t - 0.45f) / 0.6f);
                float roar = (lpRoarA - lpRoarB) * roarEnv * (0.55f + 0.9f * grain);
                s[i] = slap * 0.8f + body * 1.1f + thump * 0.85f + roar * 1.3f;
            }
            AddBubbles(s, rng, 110, 0.04f, 0.55f, 2.4f, 0.2f);
            Fade(s, (int)(Rate * 0.12f));
            return Wav(s);
        }

        private static byte[] WakeLoop()
        {
            const float seconds = 3f;
            int n = (int)(Rate * seconds);
            int fade = (int)(Rate * 0.4f);
            var s = new float[n + fade];
            var rng = new System.Random(11);
            float lpA = 0f, lpB = 0f, lpC = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lpA += 0.35f * (white - lpA);
                lpB += 0.05f * (white - lpB);
                lpC += 0.012f * (white - lpC);
                float swell = 0.65f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.7f * t) + 0.1f * Mathf.Sin(2f * Mathf.PI * 1.9f * t + 1f) + 0.05f * Mathf.Sin(2f * Mathf.PI * 4.3f * t + 2f);
                s[i] = ((lpA - lpB) * 0.9f + lpB * 1.6f + lpC * 2.5f) * swell;
            }
            AddBubbles(s, rng, 45, 0f, 1.6f, s.Length / (float)Rate - 0.1f, 0.12f);
            var loop = new float[n];
            for (int i = 0; i < n; i++)
            {
                loop[i] = s[i];
            }
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                loop[i] = s[i] * k + s[n + i] * (1f - k);
            }
            return Wav(loop);
        }

        private static void AddBubbles(float[] s, System.Random rng, int count, float start, float mean, float end, float loudness)
        {
            for (int b = 0; b < count; b++)
            {
                float at = start + Mathf.Min(end - start, (float)(-Math.Log(1 - rng.NextDouble() * 0.999) * mean));
                if (mean > 1f) at = start + (float)rng.NextDouble() * (end - start);
                float f0 = 350f * Mathf.Pow(2f, (float)rng.NextDouble() * 2.6f);
                float tau = Mathf.Lerp(0.012f, 0.045f, (float)rng.NextDouble()) * Mathf.Sqrt(800f / f0);
                float amp = loudness * Mathf.Lerp(0.3f, 1f, (float)rng.NextDouble()) * (1f - 0.6f * (at - start) / Mathf.Max(0.1f, end - start));
                int first = (int)(at * Rate);
                int length = (int)(tau * 6f * Rate);
                double phase = 0;
                for (int j = 0; j < length && first + j < s.Length; j++)
                {
                    float t = (float)j / Rate;
                    phase += 2 * Math.PI * f0 * (1f + 0.3f * t / tau) / Rate;
                    s[first + j] += amp * (float)Math.Sin(phase) * Mathf.Exp(-t / tau) * Attack(t, 0.001f);
                }
            }
        }

        private static float Attack(float t, float time) => Mathf.Clamp01(t / time);

        private static void Fade(float[] s, int samples)
        {
            for (int i = 0; i < samples; i++)
            {
                s[s.Length - 1 - i] *= (float)i / samples;
            }
        }

        private static byte[] Wav(float[] samples)
        {
            float peak = 0.001f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)Math.Tanh(samples[i]);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            int n = samples.Length;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + n * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(n * 2);
                foreach (float x in samples)
                {
                    w.Write((short)(x / peak * 0.85f * short.MaxValue));
                }
                return ms.ToArray();
            }
        }
    }
}
