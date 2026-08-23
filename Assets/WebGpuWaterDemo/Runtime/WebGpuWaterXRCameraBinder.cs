using AbstractOcclusion.WebGpuWater;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MusicProgram.WebGpuWaterDemo
{
    /// <summary>
    /// Makes an embedded WebGpuWater body follow the actual XR display camera.
    /// This prevents the common "two Main Cameras" failure mode where the old desktop
    /// camera drives underwater fog over the Quest camera's entire view.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class WebGpuWaterXRCameraBinder : MonoBehaviour
    {
        [SerializeField] WaterVolume water;
        [SerializeField] XROrigin xrOrigin;
        [Tooltip("Only during Play Mode, disable other cameras tagged MainCamera in this scene. " +
                 "The XR Origin camera remains enabled.")]
        [SerializeField] bool disableLegacyMainCameras = true;

        void Reset() => water = GetComponent<WaterVolume>();

        void Awake() => Bind();
        void OnEnable() => Bind();

        public void Bind()
        {
            if (water == null) water = GetComponent<WaterVolume>();
            if (xrOrigin == null) xrOrigin = FindFirstObjectByType<XROrigin>();
            Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : null;
            if (water == null || xrCamera == null) return;

            water.TargetCamera = xrCamera;

            if (!Application.isPlaying || !disableLegacyMainCameras) return;
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                                                                FindObjectsSortMode.None))
            {
                if (camera == xrCamera || camera.gameObject.scene != gameObject.scene) continue;
                if (camera.CompareTag("MainCamera")) camera.enabled = false;
            }
        }
    }
}
