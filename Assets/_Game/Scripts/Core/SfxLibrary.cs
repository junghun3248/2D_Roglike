using System;
using UnityEngine;

namespace Roglike.Core
{
    [CreateAssetMenu(menuName = "Roglike/Sfx Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private AudioClip _clip;
            [SerializeField, Range(0f, 1f)] private float _volume;
            public string Id => _id;
            public AudioClip Clip => _clip;
            public float Volume => _volume;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public bool TryGet(string id, out Entry entry)
        {
            foreach (Entry candidate in _entries)
                if (candidate.Id == id) { entry = candidate; return true; }
            entry = default;
            return false;
        }
    }
}
