using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Surface-platform socket that automatically plays a docked BlackBoxItem.
/// Playback may repeat, while the task-complete event fires only once.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRSocketInteractor))]
[RequireComponent(typeof(AudioSource))]
public sealed class BlackBoxPlaybackDock : MonoBehaviour
{
    [Header("Fallback")]
    [SerializeField]
    [Tooltip("Used when the black box has no recording. If also empty, a temporary confirmation tone is generated.")]
    private AudioClip fallbackRecording;
    [SerializeField] private bool generateTemporaryTone = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onPlaybackStarted = new();
    [SerializeField] private UnityEvent onPlaybackFinished = new();
    [SerializeField] private UnityEvent onFirstTaskCompleted = new();

    private XRSocketInteractor socket;
    private AudioSource source;
    private BlackBoxItem currentItem;
    private Coroutine playbackRoutine;
    private AudioClip generatedTone;
    private bool taskCompleted;

    public bool IsPlaying => source != null && source.isPlaying;
    public bool TaskCompleted => taskCompleted;
    public BlackBoxItem CurrentItem => currentItem;
    public UnityEvent OnPlaybackStarted => onPlaybackStarted;
    public UnityEvent OnPlaybackFinished => onPlaybackFinished;
    public UnityEvent OnFirstTaskCompleted => onFirstTaskCompleted;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
    }

    private void OnEnable()
    {
        socket.selectEntered.AddListener(OnSocketed);
        socket.selectExited.AddListener(OnRemoved);
    }

    private void OnDisable()
    {
        if (socket != null)
        {
            socket.selectEntered.RemoveListener(OnSocketed);
            socket.selectExited.RemoveListener(OnRemoved);
        }
        StopPlayback();
    }

    private void OnDestroy()
    {
        if (generatedTone != null)
            Destroy(generatedTone);
    }

    public void Replay()
    {
        if (currentItem != null)
            BeginPlayback(currentItem);
    }

    private void OnSocketed(SelectEnterEventArgs args)
    {
        Transform selected = args.interactableObject?.transform;
        BlackBoxItem item = selected != null
            ? selected.GetComponentInParent<BlackBoxItem>()
            : null;
        if (item == null)
            return;

        currentItem = item;
        BeginPlayback(item);
    }

    private void OnRemoved(SelectExitEventArgs args)
    {
        Transform selected = args.interactableObject?.transform;
        BlackBoxItem item = selected != null
            ? selected.GetComponentInParent<BlackBoxItem>()
            : null;
        if (item == null || item != currentItem)
            return;

        currentItem = null;
        StopPlayback();
    }

    private void BeginPlayback(BlackBoxItem item)
    {
        StopPlayback();
        AudioClip clip = item.Recording != null ? item.Recording : fallbackRecording;
        if (clip == null && generateTemporaryTone)
        {
            if (generatedTone == null)
                generatedTone = CreateTemporaryTone();
            clip = generatedTone;
        }
        if (clip == null)
            return;

        source.clip = clip;
        source.volume = item.PlaybackVolume;
        source.Play();
        onPlaybackStarted?.Invoke();
        playbackRoutine = StartCoroutine(WaitForPlayback(item));
    }

    private IEnumerator WaitForPlayback(BlackBoxItem item)
    {
        while (source != null && source.isPlaying && currentItem == item)
            yield return null;

        playbackRoutine = null;
        if (currentItem != item)
            yield break;

        onPlaybackFinished?.Invoke();
        if (!taskCompleted)
        {
            taskCompleted = true;
            onFirstTaskCompleted?.Invoke();
        }
    }

    private void StopPlayback()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
        if (source != null)
            source.Stop();
    }

    private static AudioClip CreateTemporaryTone()
    {
        const int rate = 22050;
        const float duration = 1.2f;
        int count = Mathf.RoundToInt(rate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)rate;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(time / duration));
            float carrier = Mathf.Sin(time * Mathf.PI * 2f * 430f);
            float pulse = 0.45f + 0.55f * Mathf.Sin(time * Mathf.PI * 2f * 3f);
            samples[i] = carrier * envelope * pulse * 0.18f;
        }
        AudioClip clip = AudioClip.Create("Temporary Black Box Recording", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
