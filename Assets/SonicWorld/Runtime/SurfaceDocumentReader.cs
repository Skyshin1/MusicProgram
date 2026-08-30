using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Surface-only document pickup and reading. Empty-hand Trigger opens a nearby
/// item; desktop L is the matching test input. Sonar deliberately stands down
/// at the water surface so both systems can share Trigger safely.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XROrigin))]
public sealed class SurfaceDocumentReader : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.05f)] private float handRange = 0.42f;
    [SerializeField, Min(0.1f)] private float gazeRange = 2f;
    [SerializeField] private LayerMask documentLayers = ~0;
    [SerializeField] private bool allowDesktopKeyboardTest = true;
    [SerializeField] private Key desktopReadKey = Key.L;

    [Header("Reading Card")]
    [SerializeField, Min(0.1f)] private float cardDistance = 0.75f;
    [SerializeField] private Vector3 cardLocalOffset = new(0f, -0.05f, 0f);

    private XROrigin origin;
    private QuestLeftStickLocomotion locomotion;
    private UnityEngine.XR.InputDevice leftDevice;
    private UnityEngine.XR.InputDevice rightDevice;
    private bool leftWasPressed;
    private bool rightWasPressed;
    private SurfaceDocument openDocument;
    private GameObject cardRoot;
    private Text titleText;
    private Text bodyText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureReaders()
    {
        XROrigin[] origins = FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (XROrigin xrOrigin in origins)
        {
            if (xrOrigin != null && xrOrigin.GetComponent<SurfaceDocumentReader>() == null)
                xrOrigin.gameObject.AddComponent<SurfaceDocumentReader>();
        }
    }

    private void Awake()
    {
        origin = GetComponent<XROrigin>();
        locomotion = GetComponent<QuestLeftStickLocomotion>();
        CreateCard();
    }

    private void Update()
    {
        if (locomotion == null)
            locomotion = GetComponent<QuestLeftStickLocomotion>();
        if (locomotion == null || !locomotion.IsSurfaceFloating)
        {
            if (openDocument != null)
                CloseDocument();
            return;
        }

        if (!leftDevice.isValid)
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightDevice.isValid)
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool leftPressed = IsTriggerPressed(leftDevice);
        bool rightPressed = IsTriggerPressed(rightDevice);
        bool keyboardPressed = allowDesktopKeyboardTest && !Application.isMobilePlatform &&
            Keyboard.current != null && Keyboard.current[desktopReadKey].wasPressedThisFrame;

        if (keyboardPressed)
            HandlePress(null);
        if (leftPressed && !leftWasPressed)
            HandlePress(VolumetricFogPulseEmitter.FindPlayerHandTransform(false));
        if (rightPressed && !rightWasPressed)
            HandlePress(VolumetricFogPulseEmitter.FindPlayerHandTransform(true));

        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;
    }

    private void HandlePress(Transform hand)
    {
        if (openDocument != null)
        {
            CloseDocument();
            return;
        }

        if (hand != null && HandIsHoldingSomething(hand))
            return;

        SurfaceDocument document = FindNearbyDocument(hand);
        if (document != null)
            OpenDocument(document);
    }

    private SurfaceDocument FindNearbyDocument(Transform hand)
    {
        Vector3 position = hand != null ? hand.position : ViewTransform().position;
        Collider[] hits = Physics.OverlapSphere(position, handRange, documentLayers,
            QueryTriggerInteraction.Collide);
        float bestDistance = float.PositiveInfinity;
        SurfaceDocument result = null;
        foreach (Collider hit in hits)
        {
            SurfaceDocument candidate = hit.GetComponentInParent<SurfaceDocument>();
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;
            float distance = (hit.ClosestPoint(position) - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = candidate;
            }
        }

        if (result != null)
            return result;

        Transform view = ViewTransform();
        if (Physics.Raycast(view.position, view.forward, out RaycastHit rayHit, gazeRange,
                documentLayers, QueryTriggerInteraction.Collide))
        {
            return rayHit.collider.GetComponentInParent<SurfaceDocument>();
        }
        return null;
    }

    private void OpenDocument(SurfaceDocument document)
    {
        openDocument = document;
        titleText.text = document.DocumentTitle;
        bodyText.text = document.DocumentBody;
        cardRoot.SetActive(true);
        document.Open();
    }

    private void CloseDocument()
    {
        SurfaceDocument document = openDocument;
        openDocument = null;
        if (cardRoot != null)
            cardRoot.SetActive(false);
        if (document != null)
            document.Close();
    }

    private Transform ViewTransform()
    {
        return origin != null && origin.Camera != null ? origin.Camera.transform : transform;
    }

    private static bool IsTriggerPressed(UnityEngine.XR.InputDevice device)
    {
        return device.isValid &&
            ((device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool button) && button) ||
             (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float value) && value >= 0.75f));
    }

    private static bool HandIsHoldingSomething(Transform hand)
    {
        foreach (XRBaseInteractor interactor in hand.GetComponentsInChildren<XRBaseInteractor>(true))
        {
            if (interactor != null && interactor.interactablesSelected.Count > 0)
                return true;
        }
        return false;
    }

    private void CreateCard()
    {
        Transform parent = ViewTransform();
        cardRoot = new GameObject("Surface Document Reading Card", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cardRoot.transform.SetParent(parent, false);
        cardRoot.transform.localPosition = cardLocalOffset + Vector3.forward * cardDistance;
        cardRoot.transform.localRotation = Quaternion.identity;
        cardRoot.transform.localScale = Vector3.one * 0.0012f;

        Canvas canvas = cardRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;
        RectTransform rootRect = cardRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(760f, 480f);

        Image background = cardRoot.AddComponent<Image>();
        background.color = new Color(0.01f, 0.06f, 0.1f, 0.94f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText = CreateText("Title", cardRoot.transform, font, 42, TextAnchor.UpperLeft);
        bodyText = CreateText("Body", cardRoot.transform, font, 27, TextAnchor.UpperLeft);
        titleText.rectTransform.anchorMin = new Vector2(0.06f, 0.79f);
        titleText.rectTransform.anchorMax = new Vector2(0.94f, 0.94f);
        titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
        bodyText.rectTransform.anchorMin = new Vector2(0.06f, 0.08f);
        bodyText.rectTransform.anchorMax = new Vector2(0.94f, 0.75f);
        bodyText.rectTransform.offsetMin = bodyText.rectTransform.offsetMax = Vector2.zero;
        cardRoot.SetActive(false);
    }

    private static Text CreateText(string objectName, Transform parent, Font font, int size, TextAnchor alignment)
    {
        GameObject child = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        child.transform.SetParent(parent, false);
        Text text = child.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(0.84f, 0.96f, 1f, 1f);
        return text;
    }
}
