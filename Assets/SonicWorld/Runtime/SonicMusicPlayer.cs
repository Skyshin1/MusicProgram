using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SonicMusicPlayer : MonoBehaviour
    {
        public static SonicMusicPlayer Instance { get; private set; }

        [SerializeField] private AudioClip[] playlist;
        [SerializeField] private int startIndex;
        [SerializeField, Range(0f, 1f)] private float volume = 0.32f;
        [SerializeField] private bool playOnStart = true;

        private AudioSource source;
        private int currentIndex;
        private const int SpectrumSize = 256;
        private readonly float[] spectrum = new float[SpectrumSize];
        private Vector3 smoothedBands;
        private float smoothedEnergy;

        public AudioClip CurrentClip => source != null ? source.clip : null;
        public bool IsPlaying => source != null && source.isPlaying;
        public Vector3 CurrentBands => smoothedBands;
        public float CurrentEnergy => smoothedEnergy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;
            currentIndex = playlist != null && playlist.Length > 0
                ? Mathf.Clamp(startIndex, 0, playlist.Length - 1)
                : 0;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (source == null || !source.isPlaying)
            {
                smoothedBands = Vector3.Lerp(
                    smoothedBands,
                    Vector3.zero,
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
                smoothedEnergy = Mathf.Lerp(
                    smoothedEnergy,
                    0f,
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
                return;
            }

            source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            float low = 0f;
            float mid = 0f;
            float high = 0f;
            for (int i = 1; i < spectrum.Length; i++)
            {
                float perceptual = Mathf.Sqrt(Mathf.Max(0f, spectrum[i]));
                float normalized = i / (float)(spectrum.Length - 1);
                if (normalized < 0.08f)
                    low += perceptual;
                else if (normalized < 0.36f)
                    mid += perceptual;
                else
                    high += perceptual;
            }

            Vector3 raw = new Vector3(low * 0.22f, mid * 0.07f, high * 0.045f);
            raw.x = Mathf.Clamp01(raw.x);
            raw.y = Mathf.Clamp01(raw.y);
            raw.z = Mathf.Clamp01(raw.z);
            float rawEnergy = Mathf.Clamp01((raw.x + raw.y + raw.z) * 0.6f);
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 9f);
            smoothedBands = Vector3.Lerp(smoothedBands, raw, blend);
            smoothedEnergy = Mathf.Lerp(smoothedEnergy, rawEnergy, blend);
        }

        private void Start()
        {
            if (playlist != null && playlist.Length > 0)
            {
                source.clip = playlist[currentIndex];
                if (playOnStart)
                    source.Play();
            }
        }

        public void TogglePlayback()
        {
            if (source.clip == null)
                return;

            if (source.isPlaying)
                source.Pause();
            else
                source.UnPause();
        }

        public void Next()
        {
            Select(currentIndex + 1);
        }

        public void Previous()
        {
            Select(currentIndex - 1);
        }

        private void Select(int index)
        {
            if (playlist == null || playlist.Length == 0)
                return;

            bool wasPlaying = source.isPlaying;
            currentIndex = (index % playlist.Length + playlist.Length) % playlist.Length;
            source.clip = playlist[currentIndex];
            if (wasPlaying || playOnStart)
                source.Play();
        }
    }
}
