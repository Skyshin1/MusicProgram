using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MusicProgram.CrestURP
{
    /// <summary>
    /// Non-XR free camera for inspecting the waterline. It automatically stays
    /// idle while an XR headset is driving the camera transform.
    /// </summary>
    public sealed class CrestURPDemoCamera : MonoBehaviour
    {
        [Range(0.5f, 30f)] public float moveSpeed = 6f;
        [Range(0.01f, 1f)] public float lookSpeed = 0.12f;
        [Range(1f, 5f)] public float sprintMultiplier = 2.5f;

        float _pitch;
        float _yaw;

        void OnEnable()
        {
            var euler = transform.eulerAngles;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            _yaw = euler.y;
        }

        void Update()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null)
            {
                return;
            }

            var localMove = Vector3.zero;
            if (keyboard.wKey.isPressed) localMove.z += 1f;
            if (keyboard.sKey.isPressed) localMove.z -= 1f;
            if (keyboard.dKey.isPressed) localMove.x += 1f;
            if (keyboard.aKey.isPressed) localMove.x -= 1f;
            if (keyboard.eKey.isPressed) localMove.y += 1f;
            if (keyboard.qKey.isPressed) localMove.y -= 1f;

            var speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1f);
            transform.position += transform.TransformDirection(localMove.normalized) * speed * Time.unscaledDeltaTime;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * lookSpeed;
                _pitch = Mathf.Clamp(_pitch - delta.y * lookSpeed, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
#endif
        }
    }
}
