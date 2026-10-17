using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace Odyssey.Audio
{
    internal static class LaserSound
    {
        private const int Rate = 44100;
        private const float MaxHearDistance = 250f;

        private static FMOD.Sound _shot, _hit, _overheat;
        private static bool _created, _failed;

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
                _shot = Create(Shot());
                _hit = Create(Hit());
                _overheat = Create(Overheat());
                _created = true;
                Melon<GearMod>.Logger.Msg("Laser sounds ready");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<GearMod>.Logger.Warning($"Could not prepare laser sounds: {e.Message}");
            }
            return _created;
        }

        internal static void PlayShot(Vector3 position) => Play(_shot, position, 0.55f, UnityEngine.Random.Range(0.95f, 1.06f));

        internal static void PlayHit(Vector3 position) => Play(_hit, position, 0.35f, UnityEngine.Random.Range(0.85f, 1.15f));

        internal static void PlayOverheat(Vector3 position) => Play(_overheat, position, 0.5f, 1f);

        private static void Play(FMOD.Sound sound, Vector3 position, float loudness, float pitch)
        {
            Camera cam = Camera.main;
            float volumeSetting = Mathf.Clamp01(GearMod.LaserVolume.Value);
            float distance = cam != null ? Vector3.Distance(cam.transform.position, position) : 0f;
            if (volumeSetting <= 0f || distance > MaxHearDistance || !Ensure())
            {
                return;
            }
            float volume = volumeSetting * loudness / (1f + distance / 12f);
            FMOD.Channel channel = DrillSound.Play(sound);
            channel.setVolume(volume);
            channel.setPitch(pitch);
            if (cam != null)
            {
                Vector3 local = cam.transform.InverseTransformPoint(position);
                channel.setPan(Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f);
            }
        }

        private static FMOD.Sound Create(byte[] data)
        {
            var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
            FMOD.MODE mode = FMOD.MODE.LOOP_OFF | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.OPENMEMORY;
            FMOD.RESULT result = FMODUnity.RuntimeManager.CoreSystem.createSound(data, mode, ref info, out FMOD.Sound sound);
            if (result != FMOD.RESULT.OK)
            {
                throw new Exception($"createSound: {result}");
            }
            return sound;
        }

        private static byte[] Shot()
        {
            int n = (int)(Rate * 0.55f);
            var s = new float[n];
            var rng = new System.Random(3);
            double phase = 0, phase2 = 0, mod = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float sweep = Mathf.Exp(-t / 0.05f);
                double freq = 260 + 2400 * sweep;
                mod += 2 * Math.PI * (freq * 0.5) / Rate;
                phase += 2 * Math.PI * (freq + 180 * Math.Sin(mod)) / Rate;
                phase2 += 2 * Math.PI * (freq * 1.505) / Rate;
                float tone = (float)(Math.Sin(phase) + 0.35 * Math.Sin(phase2) + 0.2 * Math.Sign(Math.Sin(phase)) * sweep);
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.3f * (white - lp);
                float click = (white - lp) * Mathf.Exp(-t / 0.004f);
                float env = Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t / 0.09f);
                float tail = lp * 0.25f * Mathf.Exp(-t / 0.16f);
                s[i] = tone * env + click * 0.6f + tail;
            }
            Echo(s, 0.07f, 0.25f);
            Echo(s, 0.13f, 0.12f);
            return Wav(s);
        }

        private static byte[] Hit()
        {
            int n = (int)(Rate * 0.35f);
            var s = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.15f * (white - lp);
                float sizzle = (white - lp) * Mathf.Exp(-t / 0.08f) * (0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * 90 * t));
                float thud = (float)Math.Sin(2 * Math.PI * (120 + 200 * Math.Exp(-t / 0.02)) * t) * Mathf.Exp(-t / 0.05f);
                s[i] = (sizzle * 0.7f + thud * 0.6f + lp * Mathf.Exp(-t / 0.12f)) * Mathf.Clamp01(t / 0.001f);
            }
            return Wav(s);
        }

        private static byte[] Overheat()
        {
            int n = (int)(Rate * 2.2f);
            var s = new float[n];
            var rng = new System.Random(7);
            float lp = 0f, lp2 = 0f;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float beepT = t < 0.16f ? t : t - 0.22f;
                bool inBeep = t < 0.16f || (t >= 0.22f && t < 0.38f);
                phase += 2 * Math.PI * (t < 0.2f ? 620 : 520) / Rate;
                float beep = inBeep ? (float)Math.Sin(phase) * Mathf.Clamp01(beepT / 0.01f) * Mathf.Clamp01((0.16f - beepT) / 0.03f) * 0.35f : 0f;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.5f * (white - lp);
                lp2 += 0.08f * (lp - lp2);
                float hissT = t - 0.3f;
                float hissEnv = hissT < 0f ? 0f : Mathf.Clamp01(hissT / 0.08f) * Mathf.Exp(-hissT / 0.7f);
                float hiss = (lp - lp2) * 0.8f + lp2 * 0.6f;
                s[i] = beep + hiss * hissEnv * 0.55f;
            }
            return Wav(s);
        }

        private static void Echo(float[] s, float delay, float gain)
        {
            int d = (int)(delay * Rate);
            for (int i = s.Length - 1; i >= d; i--)
            {
                s[i] += s[i - d] * gain;
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
            int fade = Rate / 50;
            for (int i = 0; i < fade; i++)
            {
                samples[samples.Length - 1 - i] *= (float)i / fade;
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
