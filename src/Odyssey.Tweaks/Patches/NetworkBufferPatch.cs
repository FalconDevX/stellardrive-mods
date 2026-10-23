using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using FishNet.Serializing;
using HarmonyLib;
using Managers.Server.Network;
using MelonLoader;
using Steamworks;

namespace Odyssey.Patches
{
    [HarmonyPatch]
    internal static class SteamSendBufferPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(FishySteamworks.Server.ServerSocket), "StartConnection");
            yield return AccessTools.Method(typeof(FishySteamworks.Client.ClientSocket), "StartConnection");
        }

        private static bool SetGlobal(ESteamNetworkingConfigValue key, int value)
        {
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                return SteamNetworkingUtils.SetConfigValue(key, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global, IntPtr.Zero, ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        private static void Prefix()
        {
            try
            {
                int buffer = Math.Max(1, TweaksMod.SteamSendBufferMB.Value) * 1024 * 1024;
                int rate = Math.Max(256, TweaksMod.SteamSendRateKB.Value) * 1024;
                bool ok = SetGlobal(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, buffer);
                ok &= SetGlobal(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, rate);
                ok &= SetGlobal(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, Math.Min(rate, 512 * 1024));
                Melon<TweaksMod>.Logger.Msg($"[network] Steam send buffer {buffer / 1024 / 1024} MB, send rate up to {rate / 1024} KB/s{(ok ? "" : " (Steam refused some values)")}");
            }
            catch (Exception e)
            {
                Melon<TweaksMod>.Logger.Warning($"[network] Could not set the Steam send buffer: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(MasterSync), "WriteDelta")]
    internal static class SyncTrafficPatch
    {
        private const float Window = 5f;
        private const int ReportBytes = 256 * 1024;
        private static readonly AccessTools.FieldRef<MasterSync, List<IShipChange>> Changed = AccessTools.FieldRefAccess<MasterSync, List<IShipChange>>("_changed");
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static long _bytes;
        private static float _windowStart = -1f;

        private static void Prefix(MasterSync __instance, PooledWriter writer, out int __state)
        {
            __state = writer.Length;
            try
            {
                foreach (IShipChange change in Changed(__instance))
                {
                    string key = change is UpdateStatefulPart part && part.State != null ? "part state " + part.State.GetType().Name : change.GetType().Name;
                    Counts[key] = Counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void Postfix(PooledWriter writer, int __state)
        {
            _bytes += Math.Max(0, writer.Length - __state);
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_windowStart < 0f)
            {
                _windowStart = now;
            }
            if (now - _windowStart < Window)
            {
                return;
            }
            if (_bytes >= ReportBytes)
            {
                string top = string.Join(", ", Counts.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key} x{p.Value}"));
                Melon<TweaksMod>.Logger.Msg($"[network] world sync sent {_bytes / 1024f / (now - _windowStart):0} KB/s in the last {now - _windowStart:0} s: {top}");
            }
            _bytes = 0;
            Counts.Clear();
            _windowStart = now;
        }
    }
}
