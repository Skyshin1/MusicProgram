using System.Collections.Generic;
using DeepSeaAI;
using UnityEngine;
using UnityEngine.AI;

namespace DeepSeaDemo
{
    /// <summary>Pause gameplay, not Input System tracking or simulator pose updates.</summary>
    [DisallowMultipleComponent]
    public sealed class DemoPauseCoordinator : MonoBehaviour
    {
        readonly List<Behaviour> suspended = new();
        readonly List<(Rigidbody body, Vector3 velocity, Vector3 angular)> bodies = new();
        readonly List<NavMeshAgent> agents = new();
        bool paused;
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (!value) { Restore(); return; }
            var flow = GetComponent<DemoFlow>();
            foreach (var b in flow.transform.root.GetComponentsInChildren<MonoBehaviour>())
            {
                if (b is RepairSkillCheckController check) check.CancelActiveCheck();
                if (b.enabled && (b is DeepSeaStalkerController || b is DeepSeaFishAI || b is RepairTool || b is RepairSkillCheckController || b is DemoDoor))
                { suspended.Add(b); b.enabled = false; }
            }
            foreach (var a in flow.transform.root.GetComponentsInChildren<NavMeshAgent>())
                if (a.enabled && a.isOnNavMesh && !a.isStopped) { agents.Add(a); a.isStopped = true; }
            foreach (var prop in flow.props)
            {
                if (prop == null || !prop.gameObject.activeInHierarchy || (prop.Grab != null && prop.Grab.isSelected)) continue;
                var body = prop.GetComponent<Rigidbody>();
                if (body == null || body.isKinematic) continue;
                bodies.Add((body, body.linearVelocity, body.angularVelocity)); body.isKinematic = true;
            }
        }
        void Restore()
        {
            foreach (var b in suspended) if (b != null) b.enabled = true;
            suspended.Clear();
            foreach (var a in agents) if (a != null && a.enabled && a.isOnNavMesh) a.isStopped = false;
            agents.Clear();
            foreach (var entry in bodies)
                if (entry.body != null && entry.body.gameObject.activeInHierarchy)
                { entry.body.isKinematic = false; entry.body.linearVelocity = entry.velocity; entry.body.angularVelocity = entry.angular; }
            bodies.Clear();
        }
        void OnDestroy() => Restore();
    }
}
