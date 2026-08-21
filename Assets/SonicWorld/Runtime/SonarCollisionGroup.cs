using UnityEngine;

/// <summary>
/// Marks an object as an intentional target for collision-generated sonar.
/// Hands, controllers, and ordinary scenery do not need this component, so
/// they cannot accidentally spam impact pulses while touching an object.
/// </summary>
[DisallowMultipleComponent]
public sealed class SonarCollisionGroup : MonoBehaviour
{
    [SerializeField, Min(0)] private int groupId;
    [SerializeField] private bool acceptsCollisionSonar = true;

    public int GroupId => groupId;
    public bool AcceptsCollisionSonar => acceptsCollisionSonar;
}
