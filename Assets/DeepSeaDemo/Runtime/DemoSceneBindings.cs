using DeepSeaAI;
using UnityEngine;

namespace DeepSeaDemo
{
    [DefaultExecutionOrder(-40)]
    public sealed class DemoSceneBindings : MonoBehaviour
    {
        public DemoFlow flow;
        public DemoAcoustics acoustics;
        public Transform[] route;
        public Animator enemyAnimator;
        public BlackBoxPlaybackDock dock;
        AudioClip generatedEngine;
        void Start()
        {
            if (flow.engineAudio != null && flow.engineAudio.clip == null)
            {
                const int rate = 16000; var data = new float[rate * 4];
                for (int i = 0; i < data.Length; i++)
                {
                    float t = (float)i / rate;
                    data[i] = (.16f * Mathf.Sin(t * 2 * Mathf.PI * 53) + .07f * Mathf.Sin(t * 2 * Mathf.PI * 107)) * (.7f + .3f * Mathf.Sin(t * 2 * Mathf.PI * 7));
                }
                generatedEngine = AudioClip.Create("Temporary engine loop", data.Length, 1, rate, false); generatedEngine.SetData(data, 0);
                flow.engineAudio.clip = generatedEngine; flow.engineAudio.loop = true;
            }
            flow.enemy.Configure(acoustics.RuntimeEnemyConfig, route, flow.player.transform,
                flow.player.GetComponent<PlayerRespawnController>(), enemyAnimator);
            if (dock != null) dock.OnPlaybackFinished.AddListener(flow.ParsedBlackBox);
            // Held props never push the tracked player's collision capsule.
            var playerColliders = flow.player.GetComponentsInChildren<Collider>(true);
            foreach (var prop in flow.props)
            {
                foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
                    foreach (var playerCollider in playerColliders)
                        if (collider != null && playerCollider != null) Physics.IgnoreCollision(collider, playerCollider, true);
            }
        }
        void OnDestroy() { if (dock != null && flow != null) dock.OnPlaybackFinished.RemoveListener(flow.ParsedBlackBox); if (generatedEngine != null) Destroy(generatedEngine); }
    }
}
