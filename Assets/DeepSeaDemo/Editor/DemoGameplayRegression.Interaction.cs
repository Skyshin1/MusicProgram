#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    internal static partial class DemoGameplayRegression
    {
        static DemoUIButton hoverButton;
        static PointerEventData leftPointer, rightPointer;
        public static void BeginInteractionPlay()
        { SessionState.SetBool(PlayKey + "Interaction", true); BeginPlay(); }

        static void Require(bool value, string message)
        { if (!value) throw new Exception(message); }

        static void TickInteraction()
        {
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>(); playFlow.suppressSaveForTests = true;
                playFlow.ui.ShowMain();
                // Drive the actual pointer event handlers, without a connected
                // headset's live rays overwriting this deterministic comparison.
                foreach (var module in Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None)) module.enabled = false;
                hoverButton = Object.FindObjectsByType<DemoUIButton>(FindObjectsSortMode.None).First(b => b.name == "Button new");
                leftPointer = new PointerEventData(EventSystem.current) { pointerId = 401 };
                rightPointer = new PointerEventData(EventSystem.current) { pointerId = 402 };
                Color normal = hoverButton.targetGraphic.canvasRenderer.GetColor();
                ExecuteEvents.Execute(hoverButton.gameObject, leftPointer, ExecuteEvents.pointerEnterHandler);
                Color hoverColor = hoverButton.targetGraphic.canvasRenderer.GetColor();
                Require(hoverColor.grayscale > normal.grayscale * 3f && hoverButton.hoverOutline.enabled,
                    "Hover must visibly brighten the entire actual button and show its border");
                ExecuteEvents.Execute(hoverButton.gameObject, rightPointer, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(hoverButton.gameObject, leftPointer, ExecuteEvents.pointerExitHandler);
                Require(hoverButton.hoverOutline.enabled, "One hand leaving cleared the other hand's hover");
                ExecuteEvents.Execute(hoverButton.gameObject, rightPointer, ExecuteEvents.pointerDownHandler);
                Require(hoverButton.targetGraphic.canvasRenderer.GetColor().r > .95f, "Pressed feedback lost");
                ExecuteEvents.Execute(hoverButton.gameObject, rightPointer, ExecuteEvents.pointerUpHandler);
                Require(hoverButton.hoverOutline.enabled, "Release inside must return to hover");
                ExecuteEvents.Execute(hoverButton.gameObject, rightPointer, ExecuteEvents.pointerExitHandler);
                Require(!hoverButton.hoverOutline.enabled && hoverButton.targetGraphic.canvasRenderer.GetColor().grayscale < .3f,
                    "Clicked/selected button stayed highlighted after ray left");
                ExecuteEvents.Execute(hoverButton.gameObject, leftPointer, ExecuteEvents.pointerEnterHandler);
                playReport.AppendLine("PASS actual menu pointer enter/down/up/exit; two hands; stale selection clears. Fill luminance " + normal.grayscale + " -> " + hoverColor.grayscale);
            }
            else if (step == 1) ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/UI-Hover.png");
            else if (step == 2)
            {
                ExecuteEvents.Execute(hoverButton.gameObject, leftPointer, ExecuteEvents.pointerClickHandler);
                Require(playFlow.Running && !playFlow.ui.Modal, "New Game pointer click no longer activates");
            }
            else if (step == 3)
            {
                playFlow.SetPaused(true); playFlow.ui.ShowHUD();
                VerifyHandAnchors();
                var dock = Object.FindFirstObjectByType<DemoSceneBindings>().dock;
                var sample = Object.FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None).First(a => a.kind == DemoActionKind.EvidenceDock);
                playReport.AppendLine("LAYOUT actual dock=" + dock.transform.position + " water sample=" + sample.transform.position + " start head=" + playFlow.checkpoints[0].position + " analysis checkpoint=" + playFlow.checkpoints[3].position);
                Require(Mathf.Abs(dock.transform.position.y - playFlow.checkpoints[0].position.y) < 2,
                    "Analysis dock is not on the user's first-floor layout");
                Require(Mathf.Abs(sample.transform.position.y - dock.transform.position.y) < 1,
                    "Sample receiver is not on the same floor as the black box dock");
                Require(Mathf.Abs(playFlow.checkpoints[3].position.y - dock.transform.position.y) < 2,
                    "Return checkpoint still sends the player upstairs");
                Require(playFlow.player.GetComponent<DemoPlayerSafety>().CanPlaceHead(playFlow.checkpoints[3].position),
                    "First-floor return checkpoint is obstructed");
                playReport.AppendLine("PASS user-placed black box and sample receivers remain on first floor.");
                playReport.AppendLine("PASS first-floor return checkpoint is clear and uses the existing safe platform spawn.");
                FinishPlay(); return;
            }
            step++;
        }

        static void VerifyHandAnchors()
        {
            var router = playFlow.player.GetComponent<DemoInputRouter>();
            var props = playFlow.props.Where(p => p != null && p.Grab != null).ToArray();
            foreach (var prop in props.Where(p => p.grabMode == DemoPropGrabMode.StableAtHand))
                Require(prop.Grab.farAttachMode == InteractableFarAttachMode.Near, prop.name + " overrides hand attachment");
            foreach (var stone in props.Where(p => p.id != null && p.id.StartsWith("stone")))
            {
                Require(stone.grabMode == DemoPropGrabMode.ThrowableAtDistance &&
                    stone.Grab.farAttachMode == InteractableFarAttachMode.Far && stone.Grab.throwVelocityScale >= 1.4f,
                    stone.name + " did not retain the ray lever arm and assisted throw velocity");
            }
            var flashlight = props.First(p => p.GetComponent<GrabFlashlight>() != null);
            foreach (var interactor in playFlow.player.GetComponentsInChildren<NearFarInteractor>())
            {
                var attach = (InteractionAttachController)interactor.interactionAttachController;
                Require(interactor.farAttachMode == InteractorFarAttachMode.Near, "Interactor retains remote distance");
                // Test with the remote offset that originally enabled both bugs.
                // Keep velocity input large while moving/rotating the real follow
                // transform; only artificial extension/twist must stay absent.
                var reader = attach.manipulationInput;
                reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
                reader.manualValue = Vector2.one;
                var follow = new GameObject("Attachment test tracked pose").transform;
                follow.SetPositionAndRotation(interactor.transform.position, interactor.transform.rotation);
                Transform previousFollow = attach.transformToFollow;
                attach.transformToFollow = follow;
                IInteractionAttachController controller = attach;
                try
                {
                    // With a retained remote offset, the old Starter Assets
                    // binding would extend and rotate the anchor every frame.
                    controller.MoveTo(follow.position + follow.forward * 2f);
                    for (int i = 0; i < 120; i++) controller.DoUpdate(1f / 72f);
                    var remoteAnchor = controller.GetOrCreateAnchorTransform();
                    Require(Mathf.Abs(Vector3.Distance(remoteAnchor.position, follow.position) - 2f) < .02f &&
                        Quaternion.Angle(remoteAnchor.rotation, follow.rotation) < .01f,
                        "Full stick input still pushes or rotates an offset grab anchor");
                    foreach (float distance in new[] { .4f, 1.5f, 3f })
                    {
                        controller.MoveTo(follow.position + follow.forward * distance);
                        controller.ResetOffset();
                        for (int i = 0; i < 30; i++)
                        {
                            follow.position += follow.right * (i % 2 == 0 ? .12f : -.1f);
                            follow.rotation = Quaternion.AngleAxis(7f, Vector3.up) * follow.rotation;
                            controller.DoUpdate(1f / 72f);
                        }
                        var anchor = controller.GetOrCreateAnchorTransform();
                        Require(!controller.hasOffset && Vector3.Distance(anchor.position, follow.position) < .001f,
                            "Swinging hand changed the near grip distance");
                        Require(Quaternion.Angle(anchor.rotation, follow.rotation) < .01f,
                            "Held item rotates relative to the hand when stick is pushed");
                    }
                    // Exercise selection + the grab transformer's resulting target
                    // on both real hands; pose offsets are evaluated by XRI.
                    var manager = interactor.interactionManager;
                    foreach (float distance in new[] { .4f, 2.5f })
                    {
                        flashlight.transform.position = follow.position + follow.forward * distance;
                        manager.SelectEnterUnconditionally((IXRSelectInteractor)interactor, flashlight.Grab);
                        controller.DoUpdate(1f / 72f);
                        // Smooth attach takes a few dynamic updates to settle.
                        flashlight.Grab.attachEaseInTime = 0;
                        for (int i = 0; i < 4; i++) flashlight.Grab.ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase.Dynamic);
                        var target = flashlight.Grab.GetTargetPose();
                        Transform grip = flashlight.Grab.GetAttachTransform(interactor);
                        Vector3 localGrip = flashlight.transform.InverseTransformPoint(grip.position);
                        Vector3 resultingGrip = target.position + target.rotation * Vector3.Scale(localGrip, flashlight.transform.lossyScale);
                        Require(Vector3.Distance(resultingGrip, controller.GetOrCreateAnchorTransform().position) < .03f,
                            "XRI flashlight target does not place the grip at the hand");
                        manager.SelectExit((IXRSelectInteractor)interactor, flashlight.Grab);
                    }
                    playReport.AppendLine("PASS " + interactor.name + " hand: near/far pickup maps flashlight grip to hand; repeated swings and full stick input add no extension or local twist.");
                }
                finally
                {
                    if (flashlight.Grab.isSelected) flashlight.Release();
                    attach.transformToFollow = previousFollow; controller.ResetOffset(); Object.Destroy(follow.gameObject);
                }
            }
        }
    }
}
#endif
