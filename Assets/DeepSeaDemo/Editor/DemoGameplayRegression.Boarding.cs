#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
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
        static DemoProp carriedBox, carriedSample;
        static DemoWorldAction boarding;
        static NearFarInteractor boardingLeft, boardingRight;
        static double boardingDeadline;
        public static void BeginBoardingPlay()
        { SessionState.SetBool(PlayKey + "Boarding", true); BeginPlay(); }

        static void TickBoarding()
        {
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>(); playFlow.suppressSaveForTests = true;
                playFlow.NewGame();
            }
            else if (step == 1)
            {
                var router = playFlow.player.GetComponent<DemoInputRouter>();
                // Synthetic trigger and tracked poses; exercise the real router,
                // scene colliders, selections and boarding coroutine.
                router.enabled = false;
                typeof(DemoInputRouter).GetField("<Actions>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(router, null);
                foreach (var behaviour in playFlow.player.GetComponentsInChildren<MonoBehaviour>())
                    if (behaviour.GetType().Name.Contains("TrackedPoseDriver")) behaviour.enabled = false;
                playFlow.player.GetComponent<QuestLeftStickLocomotion>().SetMovementEnabled(false);
                boarding = Object.FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None).First(a => a.kind == DemoActionKind.Board);
                carriedBox = playFlow.props.First(p => p.id == "blackbox");
                carriedSample = playFlow.props.First(p => p.id == "sample");
                boardingLeft = router.leftHand.GetComponentInChildren<NearFarInteractor>();
                boardingRight = router.rightHand.GetComponentInChildren<NearFarInteractor>();
                var target = boarding.GetComponent<Collider>().bounds.center;
                playFlow.player.transform.position += target - Vector3.forward * 1.3f - playFlow.player.Camera.transform.position;
                playFlow.player.Camera.transform.rotation = Quaternion.identity;
                foreach (var hand in new[] { boardingLeft, boardingRight })
                {
                    hand.enableUIInteraction = false;
                    hand.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
                    hand.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                    hand.selectInput.manualPerformed = true; hand.selectInput.manualValue = 1;
                }
                boardingLeft.interactionManager.SelectEnterUnconditionally((IXRSelectInteractor)boardingLeft, carriedBox.Grab);
                boardingRight.interactionManager.SelectEnterUnconditionally((IXRSelectInteractor)boardingRight, carriedSample.Grab);
                Require(router.Holding(false) && router.Holding(true), "Both evidence items must actually be held");
                var aim = new Ray(playFlow.player.Camera.transform.position, Vector3.forward);
                carriedBox.transform.position = aim.GetPoint(.4f); carriedSample.transform.position = aim.GetPoint(.8f);
                Physics.SyncTransforms();
                var find = typeof(DemoInputRouter).GetMethod("FindWorldAction", BindingFlags.Instance | BindingFlags.NonPublic);
                var args = new object[] { aim, Vector3.zero };
                Require(find.Invoke(router, args) as DemoWorldAction == boarding, "Carried evidence hides boarding target");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.layer = DemoUI.LayerIndex(playFlow.config.worldMask); blocker.transform.position = aim.GetPoint(1f);
                blocker.transform.localScale = Vector3.one * .25f; Physics.SyncTransforms();
                Require(find.Invoke(router, args) == null, "Boarding incorrectly works through solid geometry");
                Object.DestroyImmediate(blocker); Physics.SyncTransforms();
                var line = (LineRenderer)typeof(DemoInputRouter).GetField("rightLine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(router);
                playFlow.ladderTestInput = 1f;
                typeof(DemoInputRouter).GetMethod("Route", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router,
                    new object[] { true, true, default(UnityEngine.XR.InputDevice), line });
                Require(playFlow.Busy, "Held-hand Trigger did not begin platform boarding");
                var climb = boarding.GetComponent<DemoLadderClimb>();
                boardingDeadline = EditorApplication.timeSinceStartup + 5 +
                    Vector3.Distance(climb.bottomAnchor.position, climb.topAnchor.position) / climb.climbSpeed;
                playReport.AppendLine("BOARD start head=" + playFlow.player.Camera.transform.position + " destination=" + boarding.destination.position +
                    " destinationClear=" + playFlow.player.GetComponent<DemoPlayerSafety>().CanPlaceHead(boarding.destination.position));
                playReport.AppendLine("PASS both hands hold evidence; carried colliders do not block boarding; solid walls still block; held-hand Trigger starts actual boarding.");
            }
            else if (step == 2)
            {
                if (playFlow.Busy && EditorApplication.timeSinceStartup < boardingDeadline) { next = EditorApplication.timeSinceStartup + .25; return; }
                var router = playFlow.player.GetComponent<DemoInputRouter>();
                playReport.AppendLine("BOARD finish busy=" + playFlow.Busy + " head=" + playFlow.player.Camera.transform.position + " destination=" + boarding.destination.position);
                var arrivalOffset = playFlow.player.Camera.transform.position - boarding.GetComponent<DemoLadderClimb>().topAnchor.position;
                // The synthetic HMD has a fixed tracking height; after boarding,
                // gravity settles its capsule on the platform floor. Check the
                // landing column and floor, not the transient teleport eye Y.
                Require(!playFlow.Busy && Vector3.ProjectOnPlane(arrivalOffset, Vector3.up).magnitude < .3f &&
                    Mathf.Abs(arrivalOffset.y) < 1f,
                    "Ladder failed to release at the top anchor");
                Require(playFlow.player.GetComponent<QuestLeftStickLocomotion>().MovementEnabled &&
                    playFlow.player.GetComponent<CharacterController>().enabled,
                    "Ladder completion left locomotion or collision disabled");
                playFlow.ladderTestInput = float.NaN;
                Require(router.HeldProp(false) == carriedBox && router.HeldProp(true) == carriedSample,
                    "Evidence selection lost while boarding");
                foreach (var item in new[] { carriedBox, carriedSample })
                    Require(Vector3.Distance(item.transform.position, playFlow.player.Camera.transform.position) < 2f && !item.Submitted,
                        "Evidence did not travel to the platform with the player");
                playReport.AppendLine("PASS platform arrival retains black box and sample in their original hands; neither is consumed or left in the water.");
                // Reproduce an immediate drop after a large custom teleport.
                var destination = playFlow.checkpoints[0];
                Require(playFlow.SafeTeleport(destination.position, destination.eulerAngles.y), "Safe teleport refused");
                carriedBox.Release();
                typeof(DemoGrabInteractable).GetMethod("Detach", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(carriedBox.Grab, null);
                Require(carriedBox.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < .01f,
                    "Immediate drop used teleport distance as throw velocity");
                playReport.AppendLine("PASS immediate release after custom teleport does not launch evidence.");
                FinishPlay(); return;
            }
            step++;
        }
    }
}
#endif
