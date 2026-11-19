using System;
using UnityEngine;

namespace Roglike.Core
{
    public sealed class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }
        public event Action<string> SoundRequested;
        [SerializeField] private SfxLibrary _library;
        [SerializeField, Min(1)] private int _maxVoices = 12;
        [SerializeField, Range(0f, 0.5f)] private float _pitchVariation = 0.06f;
        [SerializeField] private float _overdrivePitch = 0.85f;
        private AudioSource[] _voices;
        private float[] _pitches;
        private int _nextVoice;
        private bool _overdrive;

        private void Awake()
        {
            Instance = this;
            _voices = new AudioSource[Mathf.Max(1, _maxVoices)];
            _pitches = new float[_voices.Length];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                _voices[i].spatialBlend = 0f;
            }
        }

        public void SetOverdrive(bool active)
        {
            _overdrive = active;
            for (int i = 0; i < _voices.Length; i++)
                _voices[i].pitch = _pitches[i] * (active ? _overdrivePitch : 1f);
        }

        public bool Play(string id)
        {
            if (_library == null || !_library.TryGet(id, out var entry) || entry.Clip == null) return false;
            int index = _nextVoice;
            for (int i = 0; i < _voices.Length; i++)
                if (!_voices[i].isPlaying) { index = i; break; }
            AudioSource voice = _voices[index];
            voice.Stop();
            voice.clip = entry.Clip;
            voice.volume = entry.Volume;
            _pitches[index] = UnityEngine.Random.Range(1f - _pitchVariation, 1f + _pitchVariation);
            // AudioSource playback uses the audio clock, independent of Time.timeScale.
            voice.pitch = _pitches[index] * (_overdrive ? _overdrivePitch : 1f);
            voice.Play();
            _nextVoice = (index + 1) % _voices.Length;
            SoundRequested?.Invoke(id);
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
