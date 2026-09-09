using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace DeepSeaDemo
{
    /// <summary>Scene-scoped XR startup when the project's automatic startup is off.</summary>
    [DisallowMultipleComponent]
    public sealed class DemoXRSessionBootstrap : MonoBehaviour
    {
        [SerializeField] string startupStatus = "Not started";
        public string StartupStatus => startupStatus;
        XRManagerSettings manager;
        bool ownsInitialization;
        bool runtimeRequestedQuit;
        bool subscribed;
        IEnumerator Start()
        {
            var input = GetComponent<DemoXRInput>();
            if (input == null || (Application.isEditor && input.editorMode != DemoXRInput.TestMode.RealVR))
            { Status("Skipped: editor simulation/mouse mode"); yield break; }
            OpenXRRuntime.wantsToQuit += RuntimeWantsToQuit;
            subscribed = true;
            // Allow the Demo render pipeline and graphics device to become ready first.
            yield return null;
            manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null) { Status("FAILED: XR Plug-in Management settings are missing", true); yield break; }
            if (manager.activeLoader == null)
            {
                if (manager.activeLoaders.Count == 0) { Status("FAILED: no XR loader configured for this platform", true); yield break; }
                Status("Initializing configured XR loader (project automatic XR startup is not running)");
                ownsInitialization = true;
                yield return manager.InitializeLoader();
            }
            if (manager.activeLoader == null)
            { Status("FAILED: XR loader initialization failed. Check OpenXR runtime / Quest Link connection and Unity Console", true); yield break; }
            var display = manager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
            if (display == null || !display.running)
            {
                try { manager.StartSubsystems(); }
                catch (System.Exception ex) { Status("FAILED: starting XR subsystems: " + ex.Message, true); yield break; }
            }
            float deadline = Time.realtimeSinceStartup + 15f;
            float stableSince = -1f;
            bool confirmed = false;
            do
            {
                if (runtimeRequestedQuit) yield break;
                display = manager.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
                if (display != null && display.running)
                {
                    if (stableSince < 0) stableSince = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - stableSince >= 3f)
                    {
                        Status("XR display stable for 3 seconds: " + manager.activeLoader.name + ". Headset image and tracking still require visual confirmation.");
                        confirmed = true;
                        break;
                    }
                }
                else stableSince = -1f;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            if (!confirmed)
            { Status("FAILED: XR display did not remain running. Audio alone does not mean VR started", true); yield break; }
            // A transient 'running' flag is not proof of a healthy OpenXR session.
            while (!runtimeRequestedQuit)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (runtimeRequestedQuit) yield break;
                display = manager.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
                if (display == null || !display.running)
                { Status("XR display stopped after startup; check the OpenXR native diagnostic report / Link session", true); yield break; }
            }
        }
        bool RuntimeWantsToQuit()
        {
            runtimeRequestedQuit = true;
            Status("FAILED: OpenXR runtime requested exit. Inspect the native OpenXR report in Editor.log; the session did not stay active", true);
            return true; // Observe only: do not suppress the runtime's normal shutdown.
        }
        void Status(string value, bool error = false)
        {
            startupStatus = value;
            if (error) Debug.LogError("[DeepSeaDemo XR] " + value, this);
            else Debug.Log("[DeepSeaDemo XR] " + value, this);
#if UNITY_EDITOR
            System.IO.Directory.CreateDirectory("Logs/DeepSeaDemo");
            System.IO.File.AppendAllText("Logs/DeepSeaDemo/XR-Startup.txt", System.DateTime.Now.ToString("s") + " " + value + "\n");
#endif
        }
        void OnDestroy()
        {
            StopAllCoroutines();
            if (subscribed) OpenXRRuntime.wantsToQuit -= RuntimeWantsToQuit;
            // Never shut down an XR session started by project settings or another owner.
            if (ownsInitialization && manager != null && manager.activeLoader != null)
            { manager.StopSubsystems(); manager.DeinitializeLoader(); }
        }
    }
}
