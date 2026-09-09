using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepSeaDemo
{
    /// <summary>Opt-in adapter: simulated and physical XR devices use the same actions.</summary>
    // Must run before XRI's SimulatedDeviceLifecycleManager (-29995), which can
    // otherwise remove the real headset from Input System during OnEnable.
    [DefaultExecutionOrder(-31000)]
    [DisallowMultipleComponent]
    public sealed class DemoXRInput : MonoBehaviour
    {
        public enum TestMode { XRSimulator = 0, DesktopUI = 1, RealVR = 2 }
        [Serializable] public sealed class HandActions
        {
            public InputActionReference trigger, grip, stick, tracked, menu;
            public float Trigger => Read(trigger);
            public float Grip => Read(grip);
            public bool Tracked => Read(tracked) > .5f;
            public Vector2 Stick => stick != null && stick.action.enabled ? stick.action.ReadValue<Vector2>() : Vector2.zero;
            static float Read(InputActionReference reference) => reference != null && reference.action.enabled ? reference.action.ReadValue<float>() : 0;
        }
        public HandActions left = new(), right = new();
        [Tooltip("RealVR: physical Quest/OpenXR. XRSimulator: explicit keyboard simulation. DesktopUI: mouse menus only. Select before Play.")]
        public TestMode editorMode = TestMode.RealVR;
        public GameObject[] simulatorRoots;
        readonly HashSet<InputAction> owned = new();
        public bool DesktopUI => Application.isEditor && editorMode == TestMode.DesktopUI;
        public bool UseSimulator => Application.isEditor && editorMode == TestMode.XRSimulator;
        public HandActions Hand(bool isRight) => isRight ? right : left;
        void OnEnable()
        {
            ApplySimulationMode();
#if UNITY_EDITOR
            if (GetComponent<DemoXRRuntimeDiagnostics>() == null) gameObject.AddComponent<DemoXRRuntimeDiagnostics>();
#endif
            if ((!Application.isEditor || editorMode == TestMode.RealVR) && GetComponent<DemoXRSessionBootstrap>() == null)
                gameObject.AddComponent<DemoXRSessionBootstrap>();
            foreach (var h in new[] { left, right })
                foreach (var r in new[] { h.trigger, h.grip, h.stick, h.tracked, h.menu })
                    if (r != null && !r.action.enabled) { r.action.Enable(); owned.Add(r.action); }
        }
        public void ApplySimulationMode()
        {
            foreach (var root in simulatorRoots ?? Array.Empty<GameObject>())
                if (root != null && root != gameObject && !transform.IsChildOf(root.transform))
                    root.SetActive(UseSimulator);
        }
        void OnDisable() { foreach (var a in owned) a.Disable(); owned.Clear(); }
    }
}
