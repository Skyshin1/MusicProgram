using UnityEngine;

namespace DeepSeaAI
{
    /// <summary>Readable prototype alert language: yellow/orange while checking a sound, red while chasing.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DeepSeaStalkerController))]
    public sealed class DeepSeaStalkerAlertIndicator : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField, Min(0.1f)] private float height = 2.25f;
        [SerializeField] private bool showIndicator = true;

        [Header("Colours and Pulse")]
        [SerializeField] private Color investigateColor = new(1f, 0.65f, 0.05f, 1f);
        [SerializeField] private Color chaseColor = new(1f, 0.07f, 0.03f, 1f);
        [SerializeField, Min(0.1f)] private float investigatePulseRate = 2f;
        [SerializeField, Min(0.1f)] private float chasePulseRate = 6f;

        private DeepSeaStalkerController stalker;
        private GameObject visual;
        private TextMesh mark;
        private Light glow;

        private void Awake()
        {
            stalker = GetComponent<DeepSeaStalkerController>();
            CreateVisual();
        }

        private void LateUpdate()
        {
            if (stalker == null)
                return;

            bool chase = stalker.State == DeepSeaStalkerController.StalkerState.Chase;
            bool investigate = stalker.State == DeepSeaStalkerController.StalkerState.Investigate ||
                stalker.State == DeepSeaStalkerController.StalkerState.Search ||
                stalker.State == DeepSeaStalkerController.StalkerState.ReturnToPatrol;
            bool active = showIndicator && (chase || investigate);
            if (visual == null)
                return;
            if (visual.activeSelf != active)
                visual.SetActive(active);
            if (!active)
                return;

            Color color = chase ? chaseColor : investigateColor;
            float rate = chase ? chasePulseRate : investigatePulseRate;
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * rate * Mathf.PI * 2f);
            visual.transform.position = transform.position + Vector3.up * height;
            Camera view = Camera.main;
            if (view != null)
                visual.transform.rotation = Quaternion.LookRotation(visual.transform.position - view.transform.position);
            mark.color = new Color(color.r, color.g, color.b, pulse);
            mark.transform.localScale = Vector3.one * (0.85f + pulse * 0.25f);
            glow.color = color;
            glow.intensity = pulse * (chase ? 2.2f : 1.2f);
        }

        private void CreateVisual()
        {
            visual = new GameObject("AI Alert Indicator");
            visual.transform.SetParent(transform, false);
            mark = visual.AddComponent<TextMesh>();
            mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mark.text = "!";
            mark.anchor = TextAnchor.MiddleCenter;
            mark.alignment = TextAlignment.Center;
            mark.characterSize = 0.45f;
            mark.fontSize = 96;
            mark.color = investigateColor;
            glow = visual.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 2.25f;
            glow.shadows = LightShadows.None;
            visual.SetActive(false);
        }
    }
}
