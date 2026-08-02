using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicGeneratedTerrain : MonoBehaviour
    {
        [SerializeField] private int conversionVersion;

        public int ConversionVersion => conversionVersion;

        public void Configure(int version)
        {
            conversionVersion = version;
        }
    }
}
