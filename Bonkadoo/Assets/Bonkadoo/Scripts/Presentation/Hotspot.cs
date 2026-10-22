using Bonkadoo.Application;
using UnityEngine;

namespace Bonkadoo.Presentation
{
    /// <summary>Maps a scene collider to a stable content ID. Holds no story logic.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hotspot : MonoBehaviour
    {
        [SerializeField] private string hotspotId;
        [SerializeField] private HotspotKind kind = HotspotKind.Prop;
        [Tooltip("Optional transient key, must start with input.")]
        [SerializeField] private string inputKey;
        [SerializeField] private string inputValue;

        private HotspotInfo _info;

        public string Id => hotspotId;

        public HotspotInfo Info => _info ?? (_info = new HotspotInfo(hotspotId, kind,
            string.IsNullOrEmpty(inputKey) ? null : inputKey, string.IsNullOrEmpty(inputKey) ? null : inputValue));

        /// <summary>Runtime/test configuration; scenes normally set fields in the inspector.</summary>
        public void Configure(string id, HotspotKind hotspotKind, string transientKey = null, string transientValue = null)
        {
            hotspotId = id;
            kind = hotspotKind;
            inputKey = transientKey;
            inputValue = transientValue;
            _info = null;
        }
    }
}
