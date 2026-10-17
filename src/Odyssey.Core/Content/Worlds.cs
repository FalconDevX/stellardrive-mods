using System.Collections;
using HarmonyLib;
using Planets.Network;
using Planets.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class Worlds
    {
        internal const float SeaLevel = 64f;
        internal const ulong LakePlanetId = 4;

        private static PlanetsServerTracker _serverTracker;
        private static PlanetsClientTracker _clientTracker;
        private static float _nextTrackerSearch;

        internal static TrackedPlanet FindTrackedPlanet(ulong planetId)
        {
            if ((_serverTracker == null || _clientTracker == null) && Time.unscaledTime >= _nextTrackerSearch)
            {
                _nextTrackerSearch = Time.unscaledTime + 2f;
                if (_serverTracker == null) _serverTracker = Object.FindAnyObjectByType<PlanetsServerTracker>();
                if (_clientTracker == null) _clientTracker = Object.FindAnyObjectByType<PlanetsClientTracker>();
            }
            foreach (MonoBehaviour tracker in new MonoBehaviour[] { _serverTracker, _clientTracker })
            {
                if (tracker != null && Traverse.Create(tracker).Field("_planets").GetValue() is IDictionary planets)
                {
                    foreach (DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet planet && planet.Id == planetId)
                        {
                            return planet;
                        }
                    }
                }
            }
            return null;
        }
    }

    internal static class ModItems
    {
        internal const uint Uranium = 120;
    }
}
