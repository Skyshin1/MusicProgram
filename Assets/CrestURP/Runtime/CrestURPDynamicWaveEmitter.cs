using Crest;
using UnityEngine;

namespace MusicProgram.CrestURP
{
    /// <summary>
    /// Project-facing wrapper for Crest's sphere interaction input. Add one or
    /// more emitters along a player, fish, vehicle or floating body to push
    /// velocity and displacement into Crest's Dynamic Waves simulation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereWaterInteraction))]
    public sealed class CrestURPDynamicWaveEmitter : MonoBehaviour
    {
        [UnityEngine.Range(0.02f, 12f)] public float radius = 0.65f;
        [UnityEngine.Range(-20f, 20f)] public float strength = 1f;
        [UnityEngine.Range(0f, 2f)] public float verticalMotionStrength = 0.55f;
        [UnityEngine.Range(0f, 1f)] public float compensateForWaveMotion = 0.42f;
        [UnityEngine.Range(0f, 2f)] public float velocityLead = 0.06f;
        [Tooltip("Controls the annular pressure shape around the interaction sphere.")]
        [UnityEngine.Range(0f, 10f)] public float innerSphereMultiplier = 1.55f;
        [UnityEngine.Range(0f, 1f)] public float innerSphereOffset = 0.109f;
        public bool boostLargeWaves;

        SphereWaterInteraction _interaction;

        public SphereWaterInteraction Interaction
        {
            get
            {
                if (_interaction == null) _interaction = GetComponent<SphereWaterInteraction>();
                return _interaction;
            }
        }

        void Reset() => Apply();
        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var interaction = Interaction;
            if (interaction == null) return;
            interaction._radius = Mathf.Max(0.02f, radius);
            interaction._weight = strength;
            interaction._weightUpDownMul = verticalMotionStrength;
            interaction._compensateForWaveMotion = compensateForWaveMotion;
            interaction._velocityOffset = velocityLead;
            interaction._innerSphereMultiplier = innerSphereMultiplier;
            interaction._innerSphereOffset = innerSphereOffset;
            interaction._boostLargeWaves = boostLargeWaves;
        }
    }
}
