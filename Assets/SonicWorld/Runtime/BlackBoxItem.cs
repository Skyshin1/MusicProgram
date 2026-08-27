using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Identity and recording payload for a buoyant XR black box.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class BlackBoxItem : MonoBehaviour
{
    [SerializeField] private string blackBoxId = "BlackBox01";
    [SerializeField] private AudioClip recording;
    [SerializeField, Range(0f, 1f)] private float playbackVolume = 0.9f;

    public string BlackBoxId => string.IsNullOrWhiteSpace(blackBoxId) ? name : blackBoxId;
    public AudioClip Recording => recording;
    public float PlaybackVolume => playbackVolume;
}
