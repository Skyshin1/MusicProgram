using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepSeaDemo
{
    // Both XR hands can point at a button. Selection from an earlier click is
    // not a hover, and one hand leaving must not clear the other hand's hover.
    public sealed class DemoUIButton : Button
    {
        public Text label;
        public Outline hoverOutline;
        public Graphic hoverMarker;
        readonly HashSet<int> hovering = new();
        readonly HashSet<int> pressing = new();

        public void RefreshVisual() => DoStateTransition(currentSelectionState, true);

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!IsActive()) return;
            bool highlighted = IsInteractable() && hovering.Count > 0;
            bool pressed = highlighted && pressing.Overlaps(hovering);
            var visualState = !IsInteractable() ? SelectionState.Disabled : pressed
                ? SelectionState.Pressed : highlighted ? SelectionState.Highlighted : SelectionState.Normal;
            base.DoStateTransition(visualState, instant);
            if (label != null) label.color = highlighted ? new Color(.025f, .06f, .09f, 1f) : Color.white;
            if (hoverOutline != null) hoverOutline.enabled = highlighted;
            if (hoverMarker != null) hoverMarker.gameObject.SetActive(highlighted);
            transform.localScale = highlighted ? Vector3.one * 1.035f : Vector3.one;
        }

        public override void OnPointerEnter(PointerEventData e)
        { hovering.Add(e.pointerId); base.OnPointerEnter(e); RefreshVisual(); }
        public override void OnPointerExit(PointerEventData e)
        { hovering.Remove(e.pointerId); base.OnPointerExit(e); RefreshVisual(); }
        public override void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) pressing.Add(e.pointerId);
            base.OnPointerDown(e); RefreshVisual();
        }
        public override void OnPointerUp(PointerEventData e)
        { pressing.Remove(e.pointerId); base.OnPointerUp(e); RefreshVisual(); }
        protected override void OnDisable()
        {
            hovering.Clear(); pressing.Clear();
            if (hoverOutline != null) hoverOutline.enabled = false;
            if (hoverMarker != null) hoverMarker.gameObject.SetActive(false);
            transform.localScale = Vector3.one;
            base.OnDisable();
        }
    }
}
