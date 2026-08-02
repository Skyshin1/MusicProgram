using UnityEngine;

namespace SonicWorld
{
    public enum SonicSoundEventKind
    {
        Collision,
        Swing,
        Voice
    }

    /// <summary>
    /// Allocation-free snapshot of a player-generated spatial sound.
    /// </summary>
    public readonly struct SonicSoundEvent
    {
        public readonly uint Sequence;
        public readonly float Time;
        public readonly Vector3 Position;
        public readonly float Strength;
        public readonly SonicSurfaceType Surface;
        public readonly SonicSoundEventKind Kind;
        public readonly Transform SourceA;
        public readonly Transform SourceB;
        public readonly Vector3 Bands;

        public SonicSoundEvent(
            uint sequence,
            float time,
            Vector3 position,
            float strength,
            SonicSurfaceType surface,
            SonicSoundEventKind kind,
            Transform sourceA,
            Transform sourceB,
            Vector3 bands)
        {
            Sequence = sequence;
            Time = time;
            Position = position;
            Strength = Mathf.Clamp01(strength);
            Surface = surface;
            Kind = kind;
            SourceA = sourceA;
            SourceB = sourceB;
            float total = Mathf.Max(0.0001f, bands.x + bands.y + bands.z);
            Bands = new Vector3(
                Mathf.Max(0f, bands.x) / total,
                Mathf.Max(0f, bands.y) / total,
                Mathf.Max(0f, bands.z) / total);
        }

        public bool Involves(Transform candidate)
        {
            if (candidate == null)
                return false;

            return IsSameHierarchy(SourceA, candidate) ||
                   IsSameHierarchy(SourceB, candidate);
        }

        private static bool IsSameHierarchy(Transform first, Transform second)
        {
            return first != null &&
                   (first == second ||
                    first.IsChildOf(second) ||
                    second.IsChildOf(first));
        }
    }
}
