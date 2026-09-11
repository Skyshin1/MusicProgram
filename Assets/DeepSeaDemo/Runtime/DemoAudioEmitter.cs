using UnityEngine;

namespace DeepSeaDemo
{
    [DisallowMultipleComponent]
    public sealed class DemoAudioEmitter : MonoBehaviour
    {
        [Tooltip("Optional local override. Otherwise uses Resources/DeepSeaAudio.")]
        public DemoAudioBank bank;
        readonly AudioSource[] voices = new AudioSource[4];
        AudioSource loop;
        int cursor;
        bool paused;
        public int PlayedCount { get; private set; }
        public DemoSound LastPlayed { get; private set; }
        bool Blocked => DemoFlow.Instance != null && (!DemoFlow.Instance.Running || DemoFlow.Instance.Paused);
        static DemoAudioEmitter For(Component owner) => owner.GetComponent<DemoAudioEmitter>() ?? owner.gameObject.AddComponent<DemoAudioEmitter>();
        public static void Play(Component owner, DemoSound sound) => For(owner).Play(sound);
        public static void SetLoop(Component owner, DemoSound sound, bool playing)
        {
            var emitter = owner.GetComponent<DemoAudioEmitter>();
            if (emitter == null && !playing) return;
            (emitter != null ? emitter : For(owner)).SetLoop(sound, playing);
        }
        AudioSource MakeSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }
        void Configure(AudioSource source, DemoAudioBank.Cue cue, DemoAudioBank selected)
        {
            source.clip = cue.clip; source.volume = cue.volume * selected.masterVolume;
            source.spatialBlend = cue.spatialBlend;
            source.minDistance = Mathf.Max(.1f, cue.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
            source.outputAudioMixerGroup = selected.output;
        }
        public void Play(DemoSound sound)
        {
            var selected = bank != null ? bank : DemoAudioBank.Default;
            var cue = selected != null ? selected.Get(sound) : null;
            if (Blocked || cue?.clip == null || cue.volume <= 0) return;
            int slot = cursor++ % voices.Length;
            var source = voices[slot] != null ? voices[slot] : voices[slot] = MakeSource();
            source.Stop(); Configure(source, cue, selected); source.loop = false; source.Play();
            LastPlayed = sound; PlayedCount++;
        }
        public void SetLoop(DemoSound sound, bool playing)
        {
            if (!playing) { if (loop != null) loop.Stop(); return; }
            var selected = bank != null ? bank : DemoAudioBank.Default;
            var cue = selected != null ? selected.Get(sound) : null;
            if (Blocked || cue?.clip == null) return;
            if (loop == null) loop = MakeSource();
            if (loop.clip == cue.clip && loop.isPlaying) return;
            loop.Stop(); Configure(loop,cue,selected); loop.loop = true; loop.Play();
        }
        void Update()
        {
            if (Blocked == paused) return;
            paused = Blocked;
            foreach (var source in voices) if (source != null) { if (paused) source.Pause(); else source.UnPause(); }
            if (loop != null) { if (paused) loop.Pause(); else loop.UnPause(); }
        }
        void OnDisable()
        {
            foreach (var source in voices) if (source != null) source.Stop();
            if (loop != null) loop.Stop(); paused = false;
        }
    }
}
