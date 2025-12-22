using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectSulamith.Systems
{
    [CreateAssetMenu(menuName = "ProjectSulamith/Systems/Building Yield Config")]
    public class BuildingYieldConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string prototypeId;   // "Warehouse" / "Battery" / "Canteen" ...
            public float foodPerMin;
            public float matPerMin;
            public float energyPerMin;
        }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _map;

        public bool TryGet(string proto, out Entry e)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (var it in entries)
                    if (!string.IsNullOrEmpty(it.prototypeId))
                        _map[it.prototypeId] = it;
            }
            return _map.TryGetValue(proto, out e);
        }
    }
}
