using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Small, removable runtime readout for diagnosing an XR grab target.
/// It reports hover, selection, and the Select action state of the configured
/// Near/Far interactors. Intended for scene setup, not final gameplay UI.
/// </summary>
[DisallowMultipleComponent]
public sealed class XRGrabRuntimeDiagnostics : MonoBehaviour
{
    [SerializeField] private bool showOnScreen = true;
    [SerializeField] private float refreshInterval = 0.2f;

    private XRGrabInteractable grabInteractable;
    private XRBaseInputInteractor[] interactors;
    private string status = "Waiting for XR interaction system...";
    private float nextRefresh;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (grabInteractable == null)
        {
            enabled = false;
            return;
        }

        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void Start()
    {
        interactors = FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        RefreshStatus();
        Debug.Log($"[XR Grab Debug] {name} ready. Found {interactors.Length} input interactor(s).", this);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + refreshInterval;
        RefreshStatus();
    }

    private void OnDestroy()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        Debug.Log($"[XR Grab Debug] Hover entered by {args.interactorObject.transform.name}.", this);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Debug.Log($"[XR Grab Debug] GRABBED by {args.interactorObject.transform.name}.", this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        Debug.Log($"[XR Grab Debug] Released by {args.interactorObject.transform.name}.", this);
    }

    private void RefreshStatus()
    {
        var text = new StringBuilder();
        text.AppendLine("XR Grab Debug — " + name);
        text.AppendLine($"Hover: {grabInteractable.isHovered}   Selected: {grabInteractable.isSelected}");

        if (interactors == null || interactors.Length == 0)
        {
            text.Append("No active XR input interactor found.");
            status = text.ToString();
            return;
        }

        foreach (XRBaseInputInteractor interactor in interactors)
        {
            if (interactor == null)
                continue;

            InputAction action = interactor.selectInput.inputActionReferencePerformed != null
                ? interactor.selectInput.inputActionReferencePerformed.action
                : interactor.selectInput.inputActionPerformed;
            string actionState = action == null ? "missing" : action.enabled ? "enabled" : "DISABLED";
            text.AppendLine($"{interactor.transform.name}: Select={interactor.selectInput.ReadIsPerformed()} ({actionState})");
        }

        status = text.ToString();
    }

    private void OnGUI()
    {
        if (!showOnScreen || !Application.isPlaying)
            return;

        GUI.Box(new Rect(16f, 16f, 430f, 110f), status);
    }
}
