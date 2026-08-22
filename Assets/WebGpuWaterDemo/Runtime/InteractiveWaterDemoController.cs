using AbstractOcclusion.WebGpuWater;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MusicProgram.WebGpuWaterDemo
{
    /// <summary>
    /// Small, project-owned driver for the WebGPU Water showcase. The water package itself
    /// remains reusable; this component only supplies automatic ripples, reset controls and
    /// an in-game control card for the demo scene.
    /// </summary>
    public sealed class InteractiveWaterDemoController : MonoBehaviour
    {
        [SerializeField] WaterVolume water;
        [SerializeField] Rigidbody[] resetBodies = System.Array.Empty<Rigidbody>();
        [SerializeField, Min(0.25f)] float autoRippleInterval = 1.35f;
        [SerializeField] bool automaticRipples = false;

        Vector3[] _initialPositions;
        Quaternion[] _initialRotations;
        float _nextRippleTime;
        int _rippleIndex;

        static readonly Vector2[] RipplePattern =
        {
            new Vector2(-3.8f, -2.2f),
            new Vector2( 3.2f,  1.7f),
            new Vector2(-1.2f,  2.8f),
            new Vector2( 2.0f, -2.6f),
            Vector2.zero,
        };

        void Awake()
        {
            _initialPositions = new Vector3[resetBodies.Length];
            _initialRotations = new Quaternion[resetBodies.Length];
            for (int i = 0; i < resetBodies.Length; i++)
            {
                if (resetBodies[i] == null) continue;
                _initialPositions[i] = resetBodies[i].position;
                _initialRotations[i] = resetBodies[i].rotation;
            }
        }

        void Start() => _nextRippleTime = Time.time + 0.8f;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ResetFloaters();
                if (keyboard.aKey.wasPressedThisFrame) automaticRipples = !automaticRipples;
            }

            if (!automaticRipples || water == null || Time.time < _nextRippleTime) return;

            Vector2 offset = RipplePattern[_rippleIndex++ % RipplePattern.Length];
            Vector3 center = water.transform.position;
            water.AddRipple(center.x + offset.x, center.z + offset.y, 0.22f, 0.035f);
            _nextRippleTime = Time.time + autoRippleInterval;
        }

        void ResetFloaters()
        {
            for (int i = 0; i < resetBodies.Length; i++)
            {
                Rigidbody body = resetBodies[i];
                if (body == null) continue;
                body.position = _initialPositions[i];
                body.rotation = _initialRotations[i];
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        void OnGUI()
        {
            const float width = 360f;
            var area = new Rect(18f, 18f, width, 154f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 10f, width - 28f, area.height - 20f));
            GUILayout.Label("WEBGPU WATER - INTERACTIVE POOL");
            GUILayout.Label("Drag on water: ripples + spray");
            GUILayout.Label("Drag outside pool: orbit camera | Wheel: zoom");
            GUILayout.Label("Space: pause simulation | Hold L: align sunlight");
            GUILayout.Label("R: drop the floaters again | A: automatic ripples " +
                            (automaticRipples ? "ON" : "OFF"));
            GUILayout.EndArea();
        }
    }
}
