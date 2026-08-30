using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR;

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
        [SerializeField, Min(0.1f)] private float firstCheckDelay = 2.5f;
        [SerializeField, Min(0.1f)] private float minimumInterval = 3.5f;
        [SerializeField, Min(0.1f)] private float maximumInterval = 6f;
        [SerializeField, Min(0.1f)] private float checkDuration = 1.25f;
        [SerializeField, Range(30f, 720f)] private float needleDegreesPerSecond = 270f;

        [Header("Windows")]
        [SerializeField, Range(5f, 160f)] private float successArcDegrees = 56f;
        [SerializeField, Range(2f, 80f)] private float perfectArcDegrees = 16f;
        [SerializeField, Range(0f, 0.75f)] private float failureProgressRegression = 0.1f;
        [SerializeField, Range(1f, 3f)] private float perfectSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float perfectBoostSeconds = 2f;

        [Header("World Visual")]
        [SerializeField, Min(0.1f)] private float visualHeight = 1.15f;
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
                FinishCheck();
                return;
            }

            if (AngleDelta(needle, successCenterDegrees) <= successArcDegrees * 0.5f)
            {
                onSuccess?.Invoke();
                FinishCheck();
                return;
            }

            Fail(false);
        }

        private void Fail(bool timedOut)
        {
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

        private void CancelCheck()
        {
            checkActive = false;
            nextCheckAt = float.PositiveInfinity;
            if (view != null)
                view.gameObject.SetActive(false);
        }

        private bool WasQtePressed(bool toolHeldByRightHand)
        {
            if (!Application.isMobilePlatform && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;

            bool pressed = ReadVrGrip(toolHeldByRightHand);
            bool downThisFrame = pressed && !previousVrGripPressed;
            previousVrGripPressed = pressed;
            return downThisFrame;
        }

        private static bool ReadVrGrip(bool toolHeldByRightHand)
        {
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
            view = root.AddComponent<SkillCheckView>();
            view.Initialize(visualRadius);
        }

        private void UpdateView()
        {
            if (view == null || target == null)
                return;

            Transform playerView = VolumetricFogPulseEmitter.FindPlayerViewTransform();
            Camera camera = playerView != null ? playerView.GetComponent<Camera>() : Camera.main;
            view.transform.position = target.transform.position + Vector3.up * visualHeight;
            if (camera != null)
                view.transform.rotation = Quaternion.LookRotation(view.transform.position - camera.transform.position);
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
            private LineRenderer circle;
            private LineRenderer success;
            private LineRenderer perfect;
            private LineRenderer needle;
            private float radius;

            public void Initialize(float viewRadius)
            {
                radius = viewRadius;
                circle = CreateLine("Ring", Color.white, 0.012f);
                success = CreateLine("Success", new Color(0.2f, 1f, 0.38f), 0.026f);
                perfect = CreateLine("Perfect", Color.white, 0.038f);
                needle = CreateLine("Needle", new Color(1f, 0.78f, 0.15f), 0.018f);
                DrawArc(circle, 0f, 360f, 72);
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
                    line.material = new Material(shader) { color = color };
                return line;
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
