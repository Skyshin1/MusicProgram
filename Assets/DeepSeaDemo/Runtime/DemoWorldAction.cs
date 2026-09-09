using UnityEngine;

namespace DeepSeaDemo
{
    public enum DemoActionKind { Document, Alarm, Equip, Board, Terminal, EvidenceDock, BlackBoxDock, Menu, Dive }
    public sealed class DemoWorldAction : MonoBehaviour
    {
        public DemoActionKind kind;
        public string fact;
        public string title;
        public string titleId, bodyId;
        [TextArea(5, 16)] public string body;
        public Transform destination;
        public string requiredPropId;
        public string menuCommand;
        public bool ActiveForPlayer => DemoFlow.Instance != null;
        public string Prompt => kind switch
        {
            DemoActionKind.Document => "Read Log", DemoActionKind.Alarm => "Check Alarm Record",
            DemoActionKind.Equip => "Equip Suit", DemoActionKind.Board => "Board Platform",
            DemoActionKind.Dive => "Begin Dive",
            DemoActionKind.BlackBoxDock => "Insert Black Box", DemoActionKind.EvidenceDock => "Submit Evidence",
            _ => "Read Terminal"
        };
        public void Interact(bool rightHand)
        {
            var flow = DemoFlow.Instance; if (flow == null || flow.Busy) return;
            if (kind == DemoActionKind.Menu) { flow.ui.Command(menuCommand); return; }
            if (!flow.Running || flow.Paused) return;
            switch (kind)
            {
                case DemoActionKind.Document:
                case DemoActionKind.Alarm:
                case DemoActionKind.Terminal:
                    flow.Record(fact); flow.ui.ShowMessage(string.IsNullOrEmpty(titleId) ? title : DemoTextCatalog.Get(titleId), string.IsNullOrEmpty(bodyId) ? body : DemoTextCatalog.Get(bodyId), true); break;
                case DemoActionKind.Equip:
                    if (!flow.State.Has("flashlight")) { flow.ui.Toast(DemoTextCatalog.Get("runtime.078")); return; }
                    flow.Record("suit"); flow.ui.Toast(DemoTextCatalog.Get("runtime.079")); break;
                case DemoActionKind.Board:
                    if (destination != null) flow.Board(destination);
                    break;
                case DemoActionKind.Dive:
                    if (!flow.Equipped || flow.State.stage == DemoStage.Platform) { flow.ui.Toast("Read the first log, check the alarm, take a flashlight and equip your suit first."); return; }
                    if (destination != null) flow.Board(destination);
                    break;
                case DemoActionKind.EvidenceDock:
                case DemoActionKind.BlackBoxDock:
                    var prop = DemoInputRouter.Instance.HeldProp(rightHand);
                    if (prop == null || prop.id != requiredPropId) { flow.ui.Toast(DemoTextCatalog.Get("runtime.080")); return; }
                    Accept(prop); break;
            }
        }
        void OnTriggerEnter(Collider other)
        {
            if (kind != DemoActionKind.EvidenceDock && kind != DemoActionKind.BlackBoxDock) return;
            var prop = other.GetComponentInParent<DemoProp>();
            if (prop != null && prop.id == requiredPropId) Accept(prop);
        }
        public void Accept(DemoProp prop)
        {
            var flow = DemoFlow.Instance;
            if (flow == null || !flow.Running || flow.Paused || prop.Submitted) return;
            if (kind == DemoActionKind.BlackBoxDock)
            {
                if (!flow.State.Has("blackbox")) return;
                // Actual XRSocket and BlackBoxPlaybackDock own attachment/playback. This action
                // only opens the narrative panel, never consumes/duplicates the black box.
                flow.ui.Toast(DemoTextCatalog.Get("runtime.081"));
            }
            else { flow.Record(fact); prop.Submit(); flow.ui.Toast(DemoTextCatalog.Get("runtime.082") + title); }
        }
    }
}
