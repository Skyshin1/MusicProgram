using UnityEngine;

namespace MusicProgram.AbyssalWater
{
    /// <summary>Small runtime controller used only by the isolated showcase scene.</summary>
    public sealed class AbyssalWaterShowcaseDriver : MonoBehaviour
    {
        public Camera showcaseCamera;
        public Transform lookTarget;
        public Transform movingInteractor;
        public AbyssalWaterSystem water;
        public bool animateInteractor = true;
        [Range(0.5f, 8f)] public float cameraBlendSpeed = 3f;

        Vector3 _desiredPosition;
        Vector3 _desiredLookAt;
        int _viewMode;

        void Start()
        {
            if (showcaseCamera == null) showcaseCamera = Camera.main;
            SetView(0, true);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetView(0, false);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetView(1, false);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetView(2, false);
            if (Input.GetKeyDown(KeyCode.Space) && water != null)
                water.EnqueueImpulse(new Vector3(0f, water.waterLevel, 3f), 2.4f, 1.8f);

            if (animateInteractor && movingInteractor != null && water != null)
            {
                var t = Time.time * 0.42f;
                var position = new Vector3(Mathf.Sin(t) * 9f, water.waterLevel + Mathf.Sin(t * 2.3f) * 0.32f,
                    4f + Mathf.Cos(t * 0.78f) * 5f);
                var previous = movingInteractor.position;
                movingInteractor.position = position;
                var direction = position - previous;
                if (direction.sqrMagnitude > 0.0001f)
                    movingInteractor.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (showcaseCamera == null) return;
            var blend = 1f - Mathf.Exp(-cameraBlendSpeed * Time.deltaTime);
            showcaseCamera.transform.position = Vector3.Lerp(showcaseCamera.transform.position, _desiredPosition, blend);
            var forward = (_desiredLookAt - showcaseCamera.transform.position).normalized;
            if (forward.sqrMagnitude > 0.001f)
                showcaseCamera.transform.rotation = Quaternion.Slerp(showcaseCamera.transform.rotation,
                    Quaternion.LookRotation(forward, Vector3.up), blend);
        }

        void SetView(int mode, bool immediate)
        {
            _viewMode = mode;
            switch (mode)
            {
                case 1:
                    _desiredPosition = new Vector3(0f, 0.12f, -12f);
                    _desiredLookAt = new Vector3(0f, -0.1f, 5f);
                    break;
                case 2:
                    _desiredPosition = new Vector3(0f, -5.5f, -11f);
                    _desiredLookAt = new Vector3(0f, -4f, 7f);
                    break;
                default:
                    _desiredPosition = new Vector3(0f, 7.5f, -17f);
                    _desiredLookAt = new Vector3(0f, 0f, 7f);
                    break;
            }

            if (immediate && showcaseCamera != null)
            {
                showcaseCamera.transform.position = _desiredPosition;
                showcaseCamera.transform.LookAt(_desiredLookAt);
            }
        }

        void OnGUI()
        {
            const int width = 420;
            GUI.Box(new Rect(18, 18, width, 116), "Abyssal Water — Complete URP Showcase");
            GUI.Label(new Rect(32, 48, width - 24, 22), "1 水上视角   2 动态吃水线   3 水下视角");
            GUI.Label(new Rect(32, 72, width - 24, 22), "Space：注入大型动态波纹");
            GUI.Label(new Rect(32, 96, width - 24, 22),
                _viewMode == 0 ? "当前：水上 / 平面反射与浪尖透光" :
                _viewMode == 1 ? "当前：跨水面 / 双向吃水线" :
                "当前：Beer–Lambert 吸收 / 波面曲率焦散");
        }
    }
}
