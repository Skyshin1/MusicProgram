#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace DeepSeaDemo
{
    // Read values from the PLAYER update. EditorApplication.update observes the
    // editor input state, which can report zero even while player actions work.
    [DefaultExecutionOrder(10000)]
    public sealed class DemoXRRuntimeDiagnostics : MonoBehaviour
    {
        DemoXRInput input;
        float next;
        int samples;
        int renderErrors;
        float maxFrameMs;
        bool measuringPulse;
        void Awake()
        {
            input = GetComponent<DemoXRInput>();
            Application.logMessageReceived += CaptureError;
            VolumetricFogPulseEmitter.PulseStarted += PulseStarted;
            VolumetricFogPulseEmitter.AllPulsesEnded += PulsesEnded;
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= CaptureError;
            VolumetricFogPulseEmitter.PulseStarted -= PulseStarted;
            VolumetricFogPulseEmitter.AllPulsesEnded -= PulsesEnded;
        }
        void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (renderErrors++ >= 10) return;
            WriteRenderEvidence(type + ": " + message + "\n" + stack);
        }
        void PulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
        {
            if (!measuringPulse) maxFrameMs = 0;
            measuringPulse = true;
            WriteRenderEvidence("Pulse started id=" + pulse.Id + " origin=" + pulse.Origin);
        }
        void PulsesEnded()
        {
            WriteRenderEvidence("Pulses ended. Outlined renderers=" + SonarRevealManager.ActiveRenderers.Count +
                " max observed CPU frame interval=" + maxFrameMs.ToString("F1") + " ms (not a GPU measurement)");
            measuringPulse = false;
        }
        static void WriteRenderEvidence(string text)
        {
            Directory.CreateDirectory("Logs/DeepSeaDemo");
            File.AppendAllText("Logs/DeepSeaDemo/Sonar-Render.txt", DateTime.Now.ToString("s") + " " + text + "\n");
        }
        void Update()
        {
            if (measuringPulse) maxFrameMs = Mathf.Max(maxFrameMs, Time.unscaledDeltaTime * 1000);
            if (input == null || samples >= 60 || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1; samples++;
            var sb = new StringBuilder();
            var flow = DemoFlow.Instance;
            var movement = GetComponent<QuestLeftStickLocomotion>();
            sb.AppendLine(DateTime.Now.ToString("s") + " PlayerUpdate Mode=" + input.editorMode + " Focus=" + Application.isFocused +
                " TimeScale=" + Time.timeScale + " UpdateMode=" + InputSystem.settings.updateMode + " Running=" + flow?.Running +
                " Paused=" + flow?.Paused + " Movement=" + movement?.MovementEnabled + " Position=" + transform.position);
            for (int i = 0; i < 2; i++)
            {
                var h = input.Hand(i == 1);
                var d = InputDevices.GetDeviceAtXRNode(i == 1 ? XRNode.RightHand : XRNode.LeftHand);
                d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float nativeTrigger);
                d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 nativeStick);
                sb.AppendLine((i == 0 ? "Left" : "Right") + " Tracked=" + h.Tracked + " Trigger=" + h.Trigger + " Grip=" + h.Grip +
                    " Stick=" + h.Stick + " NativeValid=" + d.isValid + " NativeTrigger=" + nativeTrigger + " NativeStick=" + nativeStick);
            }
            foreach (var nf in GetComponentsInChildren<NearFarInteractor>(true))
            {
                bool modelExists = nf.TryGetUIModel(out var model);
                var caster = nf.GetComponent<CurveInteractionCaster>();
                var press = nf.uiPressInput.inputActionReferencePerformed;
                sb.AppendLine("UI=" + nf.name + " Active=" + nf.isActiveAndEnabled + " UIEnabled=" + nf.enableUIInteraction +
                    " Mask=" + (caster != null ? caster.raycastMask.value : 0) + " Registered=" + modelExists +
                    " Hit=" + (modelExists && model.currentRaycast.gameObject != null ? model.currentRaycast.gameObject.name : "None") +
                    " PressEnabled=" + (press != null && press.action.enabled) + " Press=" + nf.uiPressInput.ReadIsPerformed());
            }
            Directory.CreateDirectory("Logs/DeepSeaDemo");
            File.AppendAllText("Logs/DeepSeaDemo/XR-PlayerInput.txt", sb.ToString());
        }
    }
}
#endif
