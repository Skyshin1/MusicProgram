using UnityEngine;

namespace DeepSeaAI
{
    /// <summary>
    /// Simple 3D fish behaviour: wander inside a spherical water volume, then
    /// flee only when an actual Water Sonar shell reaches the fish.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeepSeaFishAI : MonoBehaviour
    {
        private enum FishState { Swim, Flee }

        [Header("Swim Volume")]
        [SerializeField] private Transform swimVolumeCenter;
        [SerializeField, Min(0.1f)] private float roamRadius = 6f;
        [SerializeField, Min(0f)] private float verticalRange = 2f;
        [SerializeField, Min(0f)] private float swimSpeed = 1.1f;
        [SerializeField, Min(0.1f)] private float waypointTolerance = 0.22f;

        [Header("Sonar Escape")]
        [SerializeField, Min(0.1f)] private float sonarReactionRange = 12f;
        [SerializeField, Min(0f)] private float shellPadding = 0.45f;
        [SerializeField, Min(0.1f)] private float fleeSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float fleeDistance = 8f;
        [SerializeField, Min(0.1f)] private float fleeDuration = 3f;
        [SerializeField, Min(0f)] private float sonarReactionCooldown = 0.5f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string fleeingParameter = "Flee";
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform tail;
        [SerializeField, Range(0f, 70f)] private float swimTailAngle = 18f;
        [SerializeField, Range(0f, 70f)] private float fleeTailAngle = 42f;
        [SerializeField, Min(0f)] private float swimTailFrequency = 2.5f;
        [SerializeField, Min(0f)] private float fleeTailFrequency = 7f;
        [SerializeField, Min(0f)] private float idleBobAmplitude = 0.05f;

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip swimLoopClip;
        [SerializeField] private AudioClip fleeClip;
        [SerializeField] private bool playSwimLoopSound = true;
        [SerializeField] private bool generateFallbackSounds = true;
        [SerializeField, Range(0f, 1f)] private float swimVolume = 0.09f;
        [SerializeField, Range(0f, 1f)] private float fleeVolume = 0.45f;

        private FishState state;
        private Vector3 home;
        private Vector3 target;
        private float fleeUntil;
        private float nextSonarReaction;
        private Quaternion tailBaseRotation;
        private Vector3 visualBasePosition;
        private bool animatorHasSpeed;
        private bool animatorHasFlee;
        private AudioClip generatedSwimLoop;
        private AudioClip generatedFlee;

        public bool IsFleeing => state == FishState.Flee;

        public void ConfigureDemo(
            Transform center,
            Transform tailTransform,
            Transform visualTransform,
            AudioSource source,
            float radius)
        {
            swimVolumeCenter = center;
            tail = tailTransform;
            visualRoot = visualTransform;
            audioSource = source;
            roamRadius = Mathf.Max(0.1f, radius);
        }

        private void Awake()
        {
            home = swimVolumeCenter != null ? swimVolumeCenter.position : transform.position;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            ConfigureAudioSource();

            if (tail != null)
                tailBaseRotation = tail.localRotation;
            if (visualRoot != null)
                visualBasePosition = visualRoot.localPosition;
            CacheAnimatorParameters();
            ChooseSwimTarget();
        }

        private void OnEnable()
        {
            VolumetricFogPulseEmitter.PulseUpdated += OnPulseUpdated;
        }

        private void OnDisable()
        {
            VolumetricFogPulseEmitter.PulseUpdated -= OnPulseUpdated;
        }

        private void Start()
        {
            StartSwimAudio();
        }

        private void OnDestroy()
        {
            if (generatedSwimLoop != null)
                Destroy(generatedSwimLoop);
            if (generatedFlee != null)
                Destroy(generatedFlee);
        }

        private void Update()
        {
            if (state == FishState.Flee && Time.time >= fleeUntil)
            {
                state = FishState.Swim;
                ChooseSwimTarget();
                StartSwimAudio();
            }

            float speed = state == FishState.Flee ? fleeSpeed : swimSpeed;
            MoveTowardsTarget(speed);
            UpdateVisualAnimation(speed);
            UpdateAnimator(speed);
        }

        private void OnPulseUpdated(VolumetricFogPulseEmitter.PulseState pulse)
        {
            if (pulse.Strength <= 0.001f || Time.time < nextSonarReaction)
                return;

            float distance = Vector3.Distance(transform.position, pulse.Origin);
            if (distance > sonarReactionRange)
                return;

            float shellHalfWidth = pulse.Width * 0.5f + shellPadding;
            if (Mathf.Abs(distance - pulse.Radius) > shellHalfWidth)
                return;

            BeginFlee(pulse.Origin);
        }

        private void BeginFlee(Vector3 sonarOrigin)
        {
            Vector3 away = transform.position - sonarOrigin;
            if (away.sqrMagnitude < 0.001f)
                away = Random.onUnitSphere;
            away.Normalize();

            // Keep the escape feeling directional while avoiding perfectly uniform fish movement.
            away = (away + Random.insideUnitSphere * 0.25f).normalized;
            target = ClampToSwimVolume(transform.position + away * fleeDistance);
            state = FishState.Flee;
            fleeUntil = Time.time + fleeDuration;
            nextSonarReaction = Time.time + sonarReactionCooldown;
            PlayFleeAudio();
        }

        private void MoveTowardsTarget(float speed)
        {
            Vector3 direction = target - transform.position;
            float distance = direction.magnitude;
            if (distance <= waypointTolerance)
            {
                if (state == FishState.Swim)
                    ChooseSwimTarget();
                return;
            }

            Vector3 movement = direction / distance;
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                speed * Time.deltaTime);
            Quaternion look = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 5f * Time.deltaTime);
        }

        private void ChooseSwimTarget()
        {
            Vector3 random = Random.insideUnitSphere * roamRadius;
            random.y = Mathf.Clamp(random.y, -verticalRange, verticalRange);
            target = ClampToSwimVolume(home + random);
        }

        private Vector3 ClampToSwimVolume(Vector3 point)
        {
            Vector3 center = swimVolumeCenter != null ? swimVolumeCenter.position : home;
            Vector3 offset = point - center;
            Vector2 planar = new Vector2(offset.x, offset.z);
            if (planar.magnitude > roamRadius)
            {
                planar = planar.normalized * roamRadius;
                offset.x = planar.x;
                offset.z = planar.y;
            }
            offset.y = Mathf.Clamp(offset.y, -verticalRange, verticalRange);
            return center + offset;
        }

        private void UpdateVisualAnimation(float speed)
        {
            bool fleeing = state == FishState.Flee;
            float frequency = fleeing ? fleeTailFrequency : swimTailFrequency;
            float angle = fleeing ? fleeTailAngle : swimTailAngle;
            if (tail != null)
            {
                float wave = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f);
                tail.localRotation = tailBaseRotation * Quaternion.Euler(0f, wave * angle, 0f);
            }
            if (visualRoot != null)
            {
                float bob = Mathf.Sin(Time.time * (fleeing ? 6f : 2f)) * idleBobAmplitude;
                visualRoot.localPosition = visualBasePosition + Vector3.up * bob;
            }
        }

        private void UpdateAnimator(float speed)
        {
            if (animator == null)
                return;
            if (animatorHasSpeed)
                animator.SetFloat(speedParameter, speed);
            if (animatorHasFlee)
                animator.SetBool(fleeingParameter, state == FishState.Flee);
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                animatorHasSpeed |= parameter.name == speedParameter &&
                                    parameter.type == AnimatorControllerParameterType.Float;
                animatorHasFlee |= parameter.name == fleeingParameter &&
                                   parameter.type == AnimatorControllerParameterType.Bool;
            }
        }

        private void ConfigureAudioSource()
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 0.5f;
            audioSource.maxDistance = 12f;
            audioSource.dopplerLevel = 0.15f;
        }

        private void StartSwimAudio()
        {
            if (!playSwimLoopSound || audioSource == null || audioSource.isPlaying)
                return;
            AudioClip clip = swimLoopClip != null ? swimLoopClip : GetGeneratedSwimLoop();
            if (clip == null)
                return;
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.volume = swimVolume;
            audioSource.Play();
        }

        private void PlayFleeAudio()
        {
            if (audioSource == null)
                return;
            AudioClip clip = fleeClip != null ? fleeClip : GetGeneratedFleeClip();
            if (clip == null)
                return;
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.PlayOneShot(clip, fleeVolume);
        }

        private AudioClip GetGeneratedSwimLoop()
        {
            if (!generateFallbackSounds)
                return null;
            if (generatedSwimLoop == null)
                generatedSwimLoop = CreateTone("Fish Swim Loop", 0.75f, 180f, 235f, 0.08f);
            return generatedSwimLoop;
        }

        private AudioClip GetGeneratedFleeClip()
        {
            if (!generateFallbackSounds)
                return null;
            if (generatedFlee == null)
                generatedFlee = CreateTone("Fish Flee Chirp", 0.28f, 760f, 1220f, 0.24f);
            return generatedFlee;
        }

        private static AudioClip CreateTone(string name, float seconds, float startHz, float endHz, float volume)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(seconds * sampleRate);
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = Mathf.Lerp(startHz, endHz, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Sin(phase) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = swimVolumeCenter != null ? swimVolumeCenter.position : transform.position;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, roamRadius);
            Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, sonarReactionRange);
        }

        private void OnValidate()
        {
            roamRadius = Mathf.Max(0.1f, roamRadius);
            verticalRange = Mathf.Max(0f, verticalRange);
            swimSpeed = Mathf.Max(0f, swimSpeed);
            fleeSpeed = Mathf.Max(0.1f, fleeSpeed);
            fleeDistance = Mathf.Max(0.1f, fleeDistance);
            fleeDuration = Mathf.Max(0.1f, fleeDuration);
        }
    }
}
