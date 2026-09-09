#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace DeepSeaDemo.Editor
{
    public sealed class DemoInputDiagnostics : EditorWindow
    {
        [MenuItem("Tools/Deep Sea Demo/13 Input Diagnostics (read-only)")]
        public static void Open() => GetWindow<DemoInputDiagnostics>("Demo Input Diagnostics");
        void OnInspectorUpdate() => Repaint();
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Read-only diagnostics. This window does not advance objectives or simulate a completed playthrough.", MessageType.Info);
            var input = Object.FindFirstObjectByType<DemoXRInput>();
            if (input == null) { EditorGUILayout.HelpBox("No DemoXRInput in the loaded scene. Configure the saved 1-VR copy first.", MessageType.Warning); return; }
            EditorGUILayout.ObjectField("Input adapter", input, typeof(DemoXRInput), true);
            EditorGUILayout.LabelField("Test mode", input.editorMode.ToString());
            EditorGUILayout.LabelField("Devices", string.Join(", ", InputSystem.devices.Select(d => d.displayName)));
            foreach (bool right in new[] { false, true })
            {
                var h = input.Hand(right);
                EditorGUILayout.LabelField(right ? "Right Hand" : "Left Hand", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Tracked / Trigger / Grip", $"{h.Tracked} / {h.Trigger:F2} / {h.Grip:F2}");
                EditorGUILayout.LabelField("Stick", h.Stick.ToString("F2"));
                var router = input.GetComponent<DemoInputRouter>();
                if (router != null) EditorGUILayout.LabelField("Held / UI hover", $"{router.Holding(right)} / {router.OverUI(right)}");
            }
            var route = input.GetComponent<DemoInputRouter>();
            if (route != null) { EditorGUILayout.LabelField("Target", route.HoverTarget); EditorGUILayout.LabelField("Input block", route.LastBlockedReason); }
            var water = input.GetComponent<WaterSurfaceStateTracker>();
            if (water != null) EditorGUILayout.LabelField("Underwater / depth", $"{water.IsUnderwater} / {water.SignedDepth:F2}m");
            var flow = DemoFlow.Instance;
            EditorGUILayout.LabelField("Mission", flow != null ? flow.State.stage.ToString() : "Full mission not running / not bound");
        }
    }
}
#endif
