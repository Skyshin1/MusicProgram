using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DeepSeaDemo
{
    public enum DemoSound { RepairLoop, QteStart, RepairSuccess, RepairFailure, RepairComplete, HatchOpen, FishSwim, FishFlee, EnemyChase, EnemyBite }

    [CreateAssetMenu(menuName = "Deep Sea Demo/Audio Bank")]
    public sealed class DemoAudioBank : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            public DemoSound sound;
            public AudioClip clip;
            [Range(0, 1)] public float volume = .5f;
            [Range(0, 1)] public float spatialBlend = 1f;
            [Min(.1f)] public float minDistance = 1f;
            [Min(1f)] public float maxDistance = 20f;
        }
        [Range(0, 1)] public float masterVolume = .8f;
        public AudioMixerGroup output;
        public Cue[] cues = Array.Empty<Cue>();
        static DemoAudioBank defaults;
        public static DemoAudioBank Default => defaults != null ? defaults : defaults = Resources.Load<DemoAudioBank>("DeepSeaAudio");
        public Cue Get(DemoSound sound)
        {
            foreach (var cue in cues) if (cue != null && cue.sound == sound) return cue;
            return null;
        }
    }
}
