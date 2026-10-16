using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mining.Content
{
    internal static class ParticleTemplates
    {
        private static readonly Dictionary<string, ParticleSystem> Found = new Dictionary<string, ParticleSystem>();

        internal static ParticleSystem Get(string name = "DustPoof")
        {
            if (Found.TryGetValue(name, out ParticleSystem template) && template != null)
            {
                return template;
            }
            template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == name);
            if (template != null)
            {
                Found[name] = template;
            }
            return template;
        }
    }
}
