using System;
using UnityEngine;

namespace DeepSeaDemo
{
    [CreateAssetMenu(menuName = "Deep Sea Demo/Leather glove poses")]
    public sealed class DemoHandPose : ScriptableObject
    {
        [Serializable] public struct Joint
        {
            public string name;
            public Vector3 curlAxis;
            public float curlDegrees;
            public bool index;
        }
        public Joint[] joints;
        public float blendSpeed = 14f;
    }
}
