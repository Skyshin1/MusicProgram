#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using DeepSeaAI;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    internal static partial class DemoGameplayRegression
    {
        // Last right-hand pose from the user's failed repair attempt.
        static readonly Vector3 reportedRepairHand = new(20.24f, -19.07f, 18.42f);
        static RepairTool reachTool;
        static NearFarInteractor reachHand;
        static Transform reachPose;
        static double reachDeadline;
        static bool reachScreenshotPending;
        public static void BeginRepairPlay()
        { SessionState.SetBool(PlayKey + "Repair", true); BeginPlay(); }

        static void TickRepair()
        {
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>(); playFlow.suppressSaveForTests = true;
                playFlow.NewGame();
            }
            else if (step == 1)
            {
                playFlow.player.GetComponent<QuestLeftStickLocomotion>().SetMovementEnabled(false);
                foreach (var behaviour in playFlow.player.GetComponentsInChildren<MonoBehaviour>())
                    if (behaviour.GetType().Name.Contains("TrackedPoseDriver")) behaviour.enabled = false;
                playFlow.player.transform.position += new Vector3(20, -18.8f, 16.5f) - playFlow.player.Camera.transform.position;
                playFlow.player.Camera.transform.rotation = Quaternion.identity;
                reachTool = playFlow.props.Single(p => p.id == "locktool").GetComponent<RepairTool>();
                var trigger = playFlow.door.repair.GetComponent<BoxCollider>();
                var oldBounds = new Bounds(trigger.bounds.center, new Vector3(.7f, .24f, .7f));
                float oldDistance = Vector3.Distance(reportedRepairHand, oldBounds.ClosestPoint(reportedRepairHand));
                float newDistance = Vector3.Distance(reportedRepairHand, trigger.ClosestPoint(reportedRepairHand));
                Require(oldDistance > .32f && newDistance < .32f, "Reported rim pose does not reproduce the reach regression");
                playReport.AppendLine("PASS reported hand pose: old trigger distance=" + oldDistance + " new=" + newDistance + " tool radius=0.32");
                var find = typeof(RepairTool).GetMethod("FindNearestTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                var box = trigger.bounds;
                foreach (var point in new[] { reportedRepairHand, box.center + Vector3.up * .25f,
                    new Vector3(box.min.x + .05f, box.max.y + .1f, box.center.z),
                    new Vector3(box.max.x - .05f, box.max.y + .1f, box.center.z),
                    new Vector3(box.center.x, box.max.y + .1f, box.min.z + .05f),
                    new Vector3(box.center.x, box.max.y + .1f, box.max.z - .05f) })
                {
                    reachTool.transform.position = point; Physics.SyncTransforms();
                    Require(find.Invoke(reachTool, null) as RepairableFacility == playFlow.door.repair, "Visible hatch rim not repairable: " + point);
                }
                reachTool.transform.position = box.center + Vector3.up * 2f; Physics.SyncTransforms();
                Require(find.Invoke(reachTool, null) == null, "Repair now reaches too far away from the hatch");
                playReport.AppendLine("PASS centre and all four lid edges are repairable; a tool 2 m above remains out of range.");
                SetUpRepairHand(true);
            }
            else if (step == 2 || step == 6)
            {
                reachHand.activateInput.QueueManualState(true, 1);
                reachDeadline = EditorApplication.timeSinceStartup + 8;
            }
            else if (step == 3 || step == 7)
            {
                var prop = reachTool.GetComponent<DemoProp>();
                playReport.AppendLine("INPUT " + reachHand.name + " held=" + prop.Grab.isSelected + " trigger=" + reachHand.activateInput.ReadIsPerformed() +
                    " blocked=" + DemoInputRouter.BlockItemTrigger(reachTool.transform) + " tool=" + reachTool.transform.position + " progress=" + playFlow.door.repair.RepairProgress);
                Require(prop.Grab.isSelected && reachTool.CurrentTarget == playFlow.door.repair && playFlow.door.repair.RepairProgress > 0,
                    "Actual XRI held Trigger failed to repair at reported hand pose");
                playReport.AppendLine("PASS " + reachHand.name + " real XRI Activate input drives repair while held at the rim.");
            }
            else if (step == 4)
            {
                var check = reachTool.GetComponent<RepairSkillCheckController>();
                if (!check.IsCheckActive && EditorApplication.timeSinceStartup < reachDeadline)
                { next = EditorApplication.timeSinceStartup + .1; return; }
                Require(check.IsCheckActive, "Holding Trigger did not produce a QTE");
                ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/Repair-Rim-QTE.png");
                // ScreenCapture runs at frame end; retain the Trigger until the
                // image has rendered instead of cancelling the ring beforehand.
                reachScreenshotPending = true;
                next = EditorApplication.timeSinceStartup + .15;
                playReport.AppendLine("PASS holding Trigger at the rim opens the normal QTE.");
            }
            else if (step == 5)
            {
                if (reachScreenshotPending)
                {
                    reachHand.activateInput.QueueManualState(false, 0);
                    reachScreenshotPending = false; next = EditorApplication.timeSinceStartup + .3; return;
                }
                Require(reachTool.CurrentTarget == null && !reachTool.GetComponent<RepairSkillCheckController>().IsCheckActive,
                    "Releasing Trigger did not stop repair/QTE");
                reachTool.GetComponent<DemoProp>().Release();
                reachHand.selectInput.manualPerformed = false; reachHand.selectInput.manualValue = 0;
                playFlow.door.repair.ResetRepair();
                SetUpRepairHand(false);
                playReport.AppendLine("PASS release stops repair and hides the QTE.");
            }
            else if (step == 8)
            {
                reachHand.activateInput.QueueManualState(false, 0);
                reachTool.GetComponent<DemoProp>().Release();
                FinishPlay(); return;
            }
            step++;
        }

        static void SetUpRepairHand(bool right)
        {
            var router = playFlow.player.GetComponent<DemoInputRouter>();
            reachHand = (right ? router.rightHand : router.leftHand).GetComponentInChildren<NearFarInteractor>();
            reachHand.enableUIInteraction = false;
            reachHand.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            reachHand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            reachHand.selectInput.manualPerformed = true; reachHand.selectInput.manualValue = 1;
            reachHand.activateInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            reachHand.activateInput.QueueManualState(false, 0);
            reachPose = new GameObject("Regression repair hand pose").transform;
            reachPose.SetParent(playFlow.player.transform, true); reachPose.position = reportedRepairHand; reachPose.rotation = Quaternion.identity;
            reachHand.interactionAttachController.transformToFollow = reachPose;
            reachHand.interactionAttachController.ResetOffset();
            reachTool.transform.SetPositionAndRotation(reportedRepairHand, Quaternion.identity);
            reachHand.interactionManager.SelectEnterUnconditionally((IXRSelectInteractor)reachHand, reachTool.GetComponent<DemoProp>().Grab);
        }
    }
}
#endif
