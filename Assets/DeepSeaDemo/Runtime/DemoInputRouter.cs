using DeepSeaAI;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.EventSystems;

namespace DeepSeaDemo
{
    [DefaultExecutionOrder(-90)]
    public sealed class DemoInputRouter : MonoBehaviour
    {
        public Transform leftHand, rightHand;
        public DemoConfig config;
        public DemoXRInput Actions { get; private set; }
        public string HoverTarget { get; private set; } = "None";
        public string LastBlockedReason { get; private set; } = "";
        public static DemoInputRouter Instance { get; private set; }
        public bool LeftConsumed { get; private set; }
        public bool RightConsumed { get; private set; }
        public bool QteRight { get; private set; }
        public bool QteActive { get; private set; }
        bool pausedGrabLock;
        bool wasLeft, wasRight, wasMenu;
        float nextPulse;
        XRBaseInteractor[] leftInteractors, rightInteractors;
        LineRenderer leftLine, rightLine;
        Material lineMaterial;
        XRSelectFilterDelegate leftFilter, rightFilter;
        void Awake()
        {
            Instance = this;
            Actions = GetComponent<DemoXRInput>();
            leftInteractors = leftHand.GetComponentsInChildren<XRBaseInteractor>(true);
            rightInteractors = rightHand.GetComponentsInChildren<XRBaseInteractor>(true);
            leftFilter = new XRSelectFilterDelegate((i, target) => i.interactablesSelected.Contains(target) || CanGrab(false));
            rightFilter = new XRSelectFilterDelegate((i, target) => i.interactablesSelected.Contains(target) || CanGrab(true));
            foreach (var i in leftInteractors) i.selectFilters.Add(leftFilter);
            foreach (var i in rightInteractors) i.selectFilters.Add(rightFilter);
            lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); lineMaterial.color = Color.cyan;
            leftLine = Line(leftHand); rightLine = Line(rightHand);
        }
        void Start()
        {
            // XRI passes the caster mask to TrackedDeviceGraphicRaycaster as well
            // as physics. Excluding UI here prevents even button hover/clicks.
            // Start runs after NearFarInteractor has created its default casters.
            foreach (var caster in GetComponentsInChildren<CurveInteractionCaster>(true))
                caster.raycastMask |= config.uiMask;
            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
                ray.raycastMask |= config.uiMask;
        }
        LineRenderer Line(Transform hand)
        {
            var go = new GameObject("Interaction Ray"); go.transform.SetParent(hand, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial;
            line.positionCount = 2; line.startWidth = .002f; line.endWidth = .001f; line.enabled = false;
            return line;
        }
        void OnDestroy()
        {
            if (leftInteractors != null) foreach (var i in leftInteractors) if (i != null) i.selectFilters.Remove(leftFilter);
            if (rightInteractors != null) foreach (var i in rightInteractors) if (i != null) i.selectFilters.Remove(rightFilter);
            if (Instance == this) Instance = null; if (lineMaterial != null) Destroy(lineMaterial);
        }
        bool CanGrab(bool right) => !pausedGrabLock && !(QteActive && QteRight == right) && !OverUI(right);
        public bool OverUI(bool right)
        {
            var list = right ? rightInteractors : leftInteractors;
            if (list == null) return false;
            foreach (var i in list)
            {
                if (i is NearFarInteractor nf && nf.TryGetUIModel(out var model) && model.currentRaycast.isValid) return true;
                if (i is XRRayInteractor ray && ray.TryGetUIModel(out var rayModel) && rayModel.currentRaycast.isValid) return true;
            }
            return false;
        }
        public bool IsRight(Transform t) => t == rightHand || t.IsChildOf(rightHand);
        public DemoProp HeldProp(bool right)
        {
            var list = right ? rightInteractors : leftInteractors;
            if (list == null) return null;
            foreach (var i in list) if (i != null && i.interactablesSelected.Count > 0)
                return i.interactablesSelected[0].transform.GetComponentInParent<DemoProp>();
            return null;
        }
        public bool Holding(bool right)
        {
            foreach (var i in right ? rightInteractors : leftInteractors)
                if (i != null && i.interactablesSelected.Count > 0) return true;
            return false;
        }
        public static bool BlockItemTrigger(Transform item)
        {
            var flow = DemoFlow.Instance;
            if (Instance == null || flow == null) return false;
            if (!flow.Running || flow.Paused || flow.Busy || flow.ui.Modal) return true;
            for (int side = 0; side < 2; side++)
            {
                var prop = Instance.HeldProp(side == 1);
                if (prop != null && (item == prop.transform || item.IsChildOf(prop.transform)) && Instance.OverUI(side == 1)) return true;
            }
            return false;
        }
        void Update()
        {
            var flow = DemoFlow.Instance; if (flow == null || flow.ui == null) return;
            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            bool left = Pressed(leftDevice), right = Pressed(rightDevice);
            bool menu = leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool m) && m;
            if (Actions != null)
            {
                left = Actions.left.Trigger >= .75f; right = Actions.right.Trigger >= .75f;
                menu = Actions.left.menu != null && Actions.left.menu.action.IsPressed();
            }
            if ((menu && !wasMenu) || (Application.isEditor && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            { if (flow.Paused && !flow.ui.Modal) flow.Resume(); else if (flow.Running && !flow.Busy) { if (flow.Paused) flow.Resume(); else flow.Pause(); } }
            wasMenu = menu;
            UpdateQteLock();
            LeftConsumed = Route(false, left && !wasLeft, leftDevice, leftLine);
            RightConsumed = Route(true, right && !wasRight, rightDevice, rightLine);
            wasLeft = left; wasRight = right;
        }
        void UpdateQteLock()
        {
            QteActive = false;
            for (int side = 0; side < 2; side++)
            {
                var prop = HeldProp(side == 1);
                var check = prop != null ? prop.GetComponent<RepairSkillCheckController>() : null;
                if (check != null && check.IsCheckActive) { QteActive = true; QteRight = side == 0; break; }
            }
            var flow = DemoFlow.Instance;
            pausedGrabLock = flow != null && (!flow.Running || flow.Paused || flow.Busy || flow.ui.Modal);
            // Select filters gate world grabbing without disabling UI registration or tracking.
        }
        bool Route(bool right, bool pressed, UnityEngine.XR.InputDevice device, LineRenderer line)
        {
            var flow = DemoFlow.Instance; Transform hand = right ? rightHand : leftHand;
            Vector3 origin = hand.position, forward = hand.forward;
            bool tracked = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool t) && t;
            if (Actions != null) tracked = Actions.Hand(right).Tracked;
            if (!tracked && flow.player.Camera != null) { origin = flow.player.Camera.transform.position; forward = flow.player.Camera.transform.forward; }
            int mask = config.worldMask | config.interactMask | config.uiMask;
            if (OverUI(right) || flow.ui.Modal)
            { line.enabled = false; if (pressed) ReportSonar("sonar.ui", right, origin, false); return true; }
            DemoWorldAction action = null; Vector3 end = origin + forward * config.grabRange;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, config.grabRange, mask, QueryTriggerInteraction.Collide))
            { action = hit.collider.GetComponentInParent<DemoWorldAction>(); end = hit.point; }
            line.enabled = action != null || flow.ui.Modal;
            line.SetPosition(0, origin); line.SetPosition(1, end);
            if (action != null)
            {
                HoverTarget = action.title; flow.ui.SetHover(action.Prompt);
                if (pressed && !Holding(right)) { ReportSonar("sonar.interaction", right, origin, false); action.Interact(right); }
                return true;
            }
            if (!pressed) return false;
            var water = GetComponent<WaterSurfaceStateTracker>();
            // Sonar is a permanent hand ability. The suit remains a mission/oxygen
            // prerequisite, not a hidden lock on an otherwise valid underwater press.
            string blocked = SonarBlockReason(flow.Running, flow.Paused, flow.Busy,
                water != null && water.IsUnderwater, Holding(right), QteActive, Time.time < nextPulse);
            if (blocked != null)
            {
                ReportSonar(blocked, right, origin, blocked == "sonar.surface" || blocked == "sonar.cooldown");
                return false;
            }
            VolumetricFogPulseEmitter.EmitPlayerAt(origin, 1f, tracked ? hand : flow.player.Camera.transform);
            nextPulse = Time.time + config.sonarCooldown;
            ReportSonar("sonar.sent", right, origin, true); return true;
        }
        internal static string SonarBlockReason(bool running, bool paused, bool busy, bool underwater, bool holding, bool qte, bool coolingDown)
        {
            if (!running || paused || busy) return "sonar.paused";
            if (holding) return "sonar.holding";
            if (qte) return "sonar.qte";
            if (!underwater) return "sonar.surface";
            if (coolingDown) return "sonar.cooldown";
            return null;
        }
        void ReportSonar(string status, bool right, Vector3 origin, bool showToast)
        {
            LastBlockedReason = status == "sonar.sent" ? "" : DemoTextCatalog.Get(status);
            var flow = DemoFlow.Instance;
            if (showToast) flow.ui.Toast(DemoTextCatalog.Get(status));
#if UNITY_EDITOR
            // Event-based: records actual presses even after the startup diagnostic expires.
            var water = GetComponent<WaterSurfaceStateTracker>();
            System.IO.Directory.CreateDirectory("Logs/DeepSeaDemo");
            System.IO.File.AppendAllText("Logs/DeepSeaDemo/Sonar-Input.txt", System.DateTime.Now.ToString("s") +
                " " + (right ? "Right" : "Left") + " " + status + " Origin=" + origin +
                " Underwater=" + (water != null && water.IsUnderwater) + " Depth=" + water?.SignedDepth +
                " Suit=" + flow.Equipped + " Held=" + Holding(right) + "\n");
#endif
        }
        static bool Pressed(UnityEngine.XR.InputDevice d) => d.isValid &&
            ((d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool p) && p) ||
             (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float v) && v > .75f));
    }
}
