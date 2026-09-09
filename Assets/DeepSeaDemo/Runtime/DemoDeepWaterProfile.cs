using UnityEngine;
namespace DeepSeaDemo
{
    [CreateAssetMenu(menuName = "Deep Sea Demo/Deep Water Presentation")]
    public sealed class DemoDeepWaterProfile : ScriptableObject
    {
        [Header("Water-only absorption; surface lighting and exposure are untouched")]
        [ColorUsage(false, true)] public Color fogColor = new(.001f, .003f, .005f);
        [ColorUsage(false, true)] public Color fogExtinction = new(.62f, .26f, .22f);
        [Range(0, 8)] public float fogDensity = 1.3f;
        public Color depthExtinction = new(.45f, .3f, .23f);
        [Range(0, 8)] public float depthStrength = 1.3f;
        [Range(0, 4)] public float scatterAmbient = .04f;
        [Range(0, 4)] public float scatterSun = .08f;
        [Range(0, 8)] public float scatterIntensity = .1f;
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
