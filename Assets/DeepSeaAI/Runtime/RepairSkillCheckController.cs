using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using DeepSeaDemo;

namespace DeepSeaAI
{
    /// <summary>
    /// Dead-by-Daylight-style timed repair check. It is driven by RepairTool,
    /// so it cannot run while the player is not actively repairing a target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairSkillCheckController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float firstCheckDelay = 1.25f;
        [SerializeField, Min(0.1f)] private float minimumInterval = 6f;
        [SerializeField, Min(0.1f)] private float maximumInterval = 8f;
        [SerializeField, Min(0.1f)] private float checkDuration = 2.2f;
        [SerializeField, Range(30f, 720f)] private float needleDegreesPerSecond = 150f;

        [Header("Windows")]
        [SerializeField, Range(5f, 160f)] private float successArcDegrees = 110f;
        [SerializeField, Range(2f, 80f)] private float perfectArcDegrees = 28f;
        [SerializeField, Range(0f, 0.75f)] private float failureProgressRegression = 0.03f;
        [SerializeField, Range(1f, 3f)] private float perfectSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float perfectBoostSeconds = 2f;

        [Header("Player View Visual")]
        [SerializeField, Min(0.35f)] private float visualDistance = 1.05f;
        [SerializeField] private float visualVerticalOffset = 0f;
        [SerializeField, Min(0.05f)] private float visualRadius = 0.3f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new();
        [SerializeField] private UnityEvent onPerfect = new();
        [SerializeField] private UnityEvent onFailure = new();
        [SerializeField] private UnityEvent onTimedOut = new();

        private RepairableFacility target;
        private float nextCheckAt = float.PositiveInfinity;
        private float checkStartedAt;
        private float successCenterDegrees;
        private float perfectCenterDegrees;
        private float boostEndsAt;
        private bool checkActive;
        private bool previousVrGripPressed;
        private SkillCheckView view;

        public bool IsCheckActive => checkActive;
        public float RepairSpeedMultiplier => Time.time < boostEndsAt ? perfectSpeedMultiplier : 1f;
        public UnityEvent OnSuccess => onSuccess;
        public UnityEvent OnPerfect => onPerfect;
        public UnityEvent OnFailure => onFailure;
        public UnityEvent OnTimedOut => onTimedOut;

        public void ApplyAccessibleDefaults()
        {
            firstCheckDelay = 1.25f;
            minimumInterval = 6f;
            maximumInterval = 8f;
            checkDuration = 2.2f;
            needleDegreesPerSecond = 150f;
            successArcDegrees = 110f;
            perfectArcDegrees = 28f;
            failureProgressRegression = .03f;
        }

        /// <summary>Called every frame by the held repair tool.</summary>
        public float Tick(RepairableFacility activeTarget, bool isRepairing, bool toolHeldByRightHand)
        {
            if (!isRepairing || activeTarget == null || activeTarget.IsRepaired)
            {
                CancelCheck();
                target = null;
                return 1f;
            }

            if (target != activeTarget)
            {
                CancelCheck();
                target = activeTarget;
                nextCheckAt = Time.time + firstCheckDelay;
            }

            if (!checkActive && Time.time >= nextCheckAt)
                BeginCheck(toolHeldByRightHand);

            if (!checkActive)
                return RepairSpeedMultiplier;

            UpdateView();
            if (WasQtePressed(toolHeldByRightHand))
                ResolveCheck();
            else if (Time.time - checkStartedAt >= checkDuration)
                Fail(true);

            // The player is judging the ring while it is visible; repair is
            // paused until the result so an ignored QTE cannot be bypassed by
            // simply holding the repair Trigger.
            return checkActive ? 0f : RepairSpeedMultiplier;
        }

        private void BeginCheck(bool toolHeldByRightHand)
        {
            DemoAudioEmitter.Play(this, DemoSound.QteStart);
            checkActive = true;
            checkStartedAt = Time.time;
            successCenterDegrees = Random.Range(75f, 320f);
            perfectCenterDegrees = successCenterDegrees;
            previousVrGripPressed = ReadVrGrip(toolHeldByRightHand);
            EnsureView();
            UpdateView();
            view.gameObject.SetActive(true);
        }

        private void ResolveCheck()
        {
            float needle = NeedleDegrees();
            if (AngleDelta(needle, perfectCenterDegrees) <= perfectArcDegrees * 0.5f)
            {
                boostEndsAt = Time.time + perfectBoostSeconds;
                onPerfect?.Invoke();
                DemoAudioEmitter.Play(this, DemoSound.RepairSuccess);
                FinishCheck();
                return;
            }

            if (AngleDelta(needle, successCenterDegrees) <= successArcDegrees * 0.5f)
            {
                onSuccess?.Invoke();
                DemoAudioEmitter.Play(this, DemoSound.RepairSuccess);
                FinishCheck();
                return;
            }

            Fail(false);
        }

        private void Fail(bool timedOut)
        {
            DemoAudioEmitter.Play(this, DemoSound.RepairFailure);
            if (target != null)
                target.AdjustRepairProgress(-failureProgressRegression);
            if (timedOut)
                onTimedOut?.Invoke();
            else
                onFailure?.Invoke();
            FinishCheck();
        }

        private void FinishCheck()
        {
            checkActive = false;
            nextCheckAt = Time.time + Random.Range(minimumInterval, Mathf.Max(minimumInterval, maximumInterval));
            if (view != null)
                view.gameObject.SetActive(false);
        }

        public void CancelActiveCheck()
        {
            CancelCheck(); target = null; boostEndsAt = 0;
        }

        private void CancelCheck()
        {
            checkActive = false;
            nextCheckAt = float.PositiveInfinity;
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private bool WasQtePressed(bool toolHeldByRightHand)
        {
            var adapter = FindFirstObjectByType<DeepSeaDemo.DemoXRInput>();
            // R resets the XRI simulator: only allow it in the separate desktop test mode.
            if ((adapter == null || adapter.DesktopUI) && !Application.isMobilePlatform && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;

            bool pressed = ReadVrGrip(toolHeldByRightHand);
            bool downThisFrame = pressed && !previousVrGripPressed;
            previousVrGripPressed = pressed;
            return downThisFrame;
        }

        private static bool ReadVrGrip(bool toolHeldByRightHand)
        {
            var demoInput = DeepSeaDemo.DemoInputRouter.Instance;
            if (demoInput != null && demoInput.Holding(!toolHeldByRightHand)) return false;
            if (demoInput != null && demoInput.Actions != null)
                return demoInput.Actions.Hand(!toolHeldByRightHand).Grip >= .75f;
            var adapter = FindFirstObjectByType<DeepSeaDemo.DemoXRInput>();
            if (adapter != null) return adapter.Hand(!toolHeldByRightHand).Grip >= .75f;
            XRNode freeHand = toolHeldByRightHand ? XRNode.LeftHand : XRNode.RightHand;
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(freeHand);
            return device.isValid &&
                ((device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool pressed) && pressed) ||
                 (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float value) && value >= 0.75f));
        }

        private float NeedleDegrees()
        {
            return Mathf.Repeat((Time.time - checkStartedAt) * needleDegreesPerSecond, 360f);
        }

        private void EnsureView()
        {
            if (view != null)
                return;
            GameObject root = new GameObject("Repair Skill Check Ring");
            // This is interaction UI. Use the same after-water layer as the
            // existing HUD so depth absorption cannot make the QTE invisible.
            var flow = DeepSeaDemo.DemoFlow.Instance;
            if (flow != null && flow.config != null)
            {
                int mask = flow.config.uiMask.value;
                for (int layer = 0; layer < 32; layer++)
                    if ((mask & (1 << layer)) != 0) { root.layer = layer; break; }
            }
            view = root.AddComponent<SkillCheckView>();
            view.Initialize(visualRadius);
        }

        private void OnDestroy()
        {
            if (view != null) Destroy(view.gameObject);
        }

        private void UpdateView()
        {
            if (view == null || target == null)
                return;

            Camera camera = DemoFlow.Instance != null && DemoFlow.Instance.player != null
                ? DemoFlow.Instance.player.Camera : Camera.main;
            if (camera == null) return;

            // Keep the skill check locked to the headset so the door, water fog,
            // repair tool and nearby geometry can never hide it.
            if (view.transform.parent != camera.transform)
                view.transform.SetParent(camera.transform, false);
            view.transform.localPosition = new Vector3(0f, visualVerticalOffset, visualDistance);
            view.transform.localRotation = Quaternion.identity;
            view.SetGeometry(successCenterDegrees, successArcDegrees, perfectCenterDegrees,
                perfectArcDegrees, NeedleDegrees());
        }

        private static float AngleDelta(float first, float second)
        {
            return Mathf.Abs(Mathf.DeltaAngle(first, second));
        }

        private void OnValidate()
        {
            firstCheckDelay = Mathf.Max(0.1f, firstCheckDelay);
            minimumInterval = Mathf.Max(0.1f, minimumInterval);
            maximumInterval = Mathf.Max(minimumInterval, maximumInterval);
            checkDuration = Mathf.Max(0.1f, checkDuration);
            perfectArcDegrees = Mathf.Min(perfectArcDegrees, successArcDegrees);
        }

        private sealed class SkillCheckView : MonoBehaviour
        {
            private Canvas terminalCanvas;
            private LineRenderer circle;
            private LineRenderer success;
            private LineRenderer perfect;
            private LineRenderer needle;
            private float radius;

            public void Initialize(float viewRadius)
            {
                radius = viewRadius;
                CreateTerminalPanel();
                circle = CreateLine("Ring", new Color(.35f, .78f, .82f, 1f), 0.012f);
                success = CreateLine("Success", new Color(0.18f, 1f, 0.55f), 0.034f);
                perfect = CreateLine("Perfect", new Color(.92f, 1f, 1f), 0.05f);
                needle = CreateLine("Needle", new Color(1f, 0.68f, 0.16f), 0.022f);
                DrawArc(circle, 0f, 360f, 72);
            }

            private void CreateTerminalPanel()
            {
                var panel = new GameObject("Repair Terminal", typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(Outline));
                panel.transform.SetParent(transform, false);
                panel.layer = gameObject.layer;
                var rect = (RectTransform)panel.transform;
                rect.sizeDelta = new Vector2(760, 620);
                rect.localScale = Vector3.one * .001f;
                rect.localPosition = Vector3.forward * .025f;
                terminalCanvas = panel.GetComponent<Canvas>();
                terminalCanvas.renderMode = RenderMode.WorldSpace;
                terminalCanvas.sortingOrder = 1200;
                var image = panel.GetComponent<Image>();
                image.color = new Color(.015f, .055f, .075f, .92f);
                image.raycastTarget = false;
                var outline = panel.GetComponent<Outline>();
                outline.effectColor = new Color(.18f, .86f, .9f, .95f);
                outline.effectDistance = new Vector2(3, -3);
                AddText(rect, "REPAIR SYNCHRONIZATION", new Vector2(0, 254), new Vector2(700, 52), 28,
                    new Color(.35f, .95f, .95f), TextAnchor.MiddleCenter);
                AddText(rect, "OTHER HAND  /  PRESS GRIP IN THE GREEN SECTOR", new Vector2(0, -258),
                    new Vector2(700, 52), 20, new Color(.78f, .9f, .92f), TextAnchor.MiddleCenter);
                var line = new GameObject("Header Line", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(rect, false); line.layer = gameObject.layer;
                var lineRect = (RectTransform)line.transform; lineRect.sizeDelta = new Vector2(660, 3); lineRect.anchoredPosition = new Vector2(0, 220);
                line.GetComponent<Image>().color = new Color(.18f, .86f, .9f, .75f);
            }

            private static void AddText(RectTransform parent, string value, Vector2 position, Vector2 size,
                int fontSize, Color color, TextAnchor alignment)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
                var rect = (RectTransform)go.transform; rect.anchoredPosition = position; rect.sizeDelta = size;
                var text = go.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.text = value; text.fontSize = fontSize; text.color = color; text.alignment = alignment;
                text.raycastTarget = false;
            }

            public void SetGeometry(float successCenter, float successWidth, float perfectCenter,
                float perfectWidth, float needleDegrees)
            {
                DrawArc(success, successCenter - successWidth * 0.5f, successWidth, 18);
                DrawArc(perfect, perfectCenter - perfectWidth * 0.5f, perfectWidth, 10);
                float radians = needleDegrees * Mathf.Deg2Rad;
                needle.positionCount = 2;
                needle.SetPosition(0, Vector3.zero);
                needle.SetPosition(1, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius * 0.92f);
            }

            private LineRenderer CreateLine(string name, Color color, float width)
            {
                GameObject child = new(name);
                child.transform.SetParent(transform, false);
                child.layer = gameObject.layer;
                LineRenderer line = child.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = false;
                line.startWidth = width;
                line.endWidth = width;
                line.startColor = color;
                line.endColor = color;
                line.numCapVertices = 4;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    var material = new Material(shader) { color = color };
                    // Demo UI After Water draws the transparent UI queue.
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    line.sharedMaterial = material;
                }
                return line;
            }

            private void OnDestroy()
            {
                foreach (var line in GetComponentsInChildren<LineRenderer>())
                    if (line.sharedMaterial != null) Destroy(line.sharedMaterial);
            }

            private void DrawArc(LineRenderer line, float startDegrees, float widthDegrees, int segments)
            {
                line.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float degrees = startDegrees + widthDegrees * i / segments;
                    float radians = degrees * Mathf.Deg2Rad;
                    line.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius);
                }
            }
        }
    }
}
