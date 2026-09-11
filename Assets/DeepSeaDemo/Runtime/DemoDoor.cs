using DeepSeaAI;
using UnityEngine;
using UnityEngine.Events;

namespace DeepSeaDemo
{
    public sealed class DemoDoor : MonoBehaviour
    {
        public RepairableFacility repair;
        public Transform hinge;
        public Vector3 openEuler = new(0, -105, 0);
        public float seconds = 2.5f;
        public LayerMask obstructionMask;
        public Transform safetyCenter;
        public Vector3 safetyExtents = new(.8f, 1.1f, .6f);
        public Collider doorwayBlocker;
        public UnityEvent onUnlocked = new();
        public UnityEvent onOpened = new();
        Quaternion closedRotation;
        float progress;
        bool reported, unlocked;
        float nextBlockedNotice;
        readonly Collider[] hits = new Collider[24];
        void Awake()
        {
            closedRotation = hinge.localRotation;
            // The repair collider is authored in the scene. Preserve its Center
            // and Size so entering Play never replaces the designer's settings.
        }
        void Update()
        {
            if (repair == null || !repair.IsRepaired || progress >= 1f) return;
            if (!unlocked) { unlocked = true; onUnlocked.Invoke(); }
            if (Obstructed())
            {
                if (Time.unscaledTime >= nextBlockedNotice)
                { nextBlockedNotice = Time.unscaledTime + 3f; DemoFlow.Instance?.ui.Toast("Lock repaired. Move yourself and loose objects away from the hatch so it can open safely."); }
                return;
            }
            if (progress == 0 && Time.deltaTime > 0) DemoAudioEmitter.Play(this, DemoSound.HatchOpen);
            progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / Mathf.Max(.2f, seconds));
            hinge.localRotation = Quaternion.Slerp(closedRotation, closedRotation * Quaternion.Euler(openEuler), Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1f && !reported)
            {
                reported = true;
                if (doorwayBlocker != null) doorwayBlocker.enabled = false;
                onOpened.Invoke(); DemoFlow.Instance?.DoorOpened();
            }
        }
        bool Obstructed()
        {
            if (safetyCenter == null) return false;
            int count = Physics.OverlapBoxNonAlloc(safetyCenter.position, safetyExtents, hits, safetyCenter.rotation, obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i] == null || hits[i].transform.IsChildOf(transform) || hits[i].transform.IsChildOf(hinge)) continue;
                if (hits[i] is CharacterController || hits[i].GetComponentInParent<DemoProp>() != null) return true;
            }
            return false;
        }
        public void Restore(bool opened)
        {
            repair.ResetRepair();
            if (opened) repair.AdjustRepairProgress(1);
            progress = opened ? 1f : 0f; reported = unlocked = opened;
            hinge.localRotation = opened ? closedRotation * Quaternion.Euler(openEuler) : closedRotation;
            if (doorwayBlocker != null) doorwayBlocker.enabled = !opened;
        }
    }
}
