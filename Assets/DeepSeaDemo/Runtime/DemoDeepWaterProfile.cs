using UnityEngine;
namespace DeepSeaDemo
{
    [CreateAssetMenu(menuName = "Deep Sea Demo/Deep Water Presentation")]
    public sealed class DemoDeepWaterProfile : ScriptableObject
    {
        [Header("Water-only absorption; surface lighting and exposure are untouched")]
        [ColorUsage(false, true)] public Color fogColor = new(.008f, .035f, .065f);
        [ColorUsage(false, true)] public Color fogExtinction = new(.48f, .34f, .26f);
        [Range(0, 8)] public float fogDensity = .9f;
        public Color depthExtinction = new(.16f, .11f, .08f);
        [Range(0, 8)] public float depthStrength = .9f;
        [Range(0, .25f)] public float minimumDepthLight = .075f;
        [Range(0, 4)] public float scatterAmbient = .6f;
        [Range(0, 4)] public float scatterSun = .16f;
        [Range(0, 8)] public float scatterIntensity = .7f;
        [Range(0, 2)] public float causticIntensity = .12f;
        [Min(0)] public float causticDepthFade = .65f;
        [Header("Tool visibility distances (metres)")]
        [Range(.1f, 1f)] public float headlampRadius = .65f;
        [Range(0, 1)] public float headlampOffset = .3f;
        [Range(.05f, .8f)] public float headlampEdge = .3f;
        [Range(1, 10)] public float flashlightRange = 5f;
        [Header("Acceptance depths; these are test checkpoints, not exposure controls")]
        public float shallowTestDepth = 5, transitionTestDepth = 15, deepTestDepth = 22;
    }
}
