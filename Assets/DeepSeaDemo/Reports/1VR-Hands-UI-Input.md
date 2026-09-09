# Hands, inverted UI and movement — 2026-09-09

Target: DeepSeaInvestigation_1VR. No scene reconstruction or reload.

## Follow-up fix and regression — 17:45

The earlier sign-only glove correction was insufficient. `DemoHandAnimator` now aligns the LEFT visual wrist and palm landmarks to the mirrored RIGHT visual in controller-local coordinates. Tracking transforms, original mesh and supplier bone scales are not changed. Left joints use individual bend planes derived from actual bone directions and the palm normal, including the reflected wrist transform; they no longer all blindly rotate around local Z. Inspector switches `Calibrate Left Palm` and `Anatomical Left Curl` allow reverting to authored pose axes for future replacement models.

The concrete UI blocker was the far-caster layer mask: the scene stores **1792**, while every menu button is on UI layer 11 (**2048**). XRI `CurveInteractionCaster.UpdateUIModel` forwards this mask to `TrackedDeviceGraphicRaycaster`, which rejects graphics outside it. Thus even a correctly bound Trigger could not hit a button. `DemoInputRouter.Start` now includes the configured UI mask (result **3840**) after XRI has created its casters. The generator preserves the same rule. Physics/world masks are retained; no collider-based menu, input fallback or second locomotion provider was introduced.

`New Game` is still required to release the intentional opening movement lock. Walking while a menu is modal remains disabled. Actual joystick movement after closing the menu is pending headset verification, not reported as passed.

The old XR-Live action values were sampled from `EditorApplication.update`, not the player input state. They are NOT proof that the Quest controls return zero during gameplay. The real devices are registered as `QuestTouchPlusController` and action references resolve. `DemoXRRuntimeDiagnostics` now records from player `Update` for the first 60 seconds in the Editor, including action/native values, UI hover/press, mask, mission pause and movement state. It writes `Logs/DeepSeaDemo/XR-PlayerInput.txt` and is excluded from builds. It does not override any input values.

Isolated Unity regression on the supplied generated glove prefabs passed:

- Wrist, middle/index/pinky palm landmarks match the mirrored right hand within 2 mm (reported errors round to zero at six decimal places).
- All 15 left finger joints initially bend toward the palm, including the thumb.
- Executing the actual runtime ray-mask repair changes a caster from 1792 to 3840.
- Preview scene is disposed without saving/reloading the user's scene.

Detailed output: `Logs/DeepSeaDemo/Hands-UI-Regression.txt`.

Headset acceptance still required: check left wrist fit and open/closed fingers, aim at `New Game` and click Trigger, then push the left stick. Also check pause/resume clicks and that UI clicks do not emit sonar. Do not equate the isolated tests with a full VR input or comfort test.

## Earlier investigation (superseded where noted above)

## Left glove

The scene uses a left-hand pose asset and `_l` bones, not a right-hand mesh reference. Comparing the supplied left/right prefab finger local rotations and translations shows the same local axes. The supplied left rig performs mirroring at the wrist. The generated pose nevertheless inverted the finger curl sign, causing outward bending.

Changed both the reusable left pose asset and its Demo-isolated copy from positive 65/30 degrees to negative 65/30, matching the supplied finger-local axes. Updated the generator so later builds do not reintroduce the sign error. Right poses and tracking transforms unchanged. Actual hand comfort/visual pose still needs headset confirmation.

## UI orientation

Moved the isolated `Demo UI After Water` Render Objects pass from AfterRendering (1000, after final blit) to AfterRenderingPostProcessing + 10 (610). This keeps UI after water, outlines and droplets but before final XR target/viewport handling. No transform flipping or negative Canvas scaling was used. Generator updated consistently. This is a targeted correction; hardware visual verification is pending.

## Interaction and movement investigation

Latest previous hardware log shows Touch Plus devices but zero Demo hand tracking/Trigger values. Added action-enabled state, resolved controls, registered Touch Plus layout, device product/interface/class/usages to the read-only project diagnostics. An active-headset run is needed to distinguish disabled actions from failed device-layout matching.

The mission intentionally disables locomotion at the opening menu; failure to click New Game also keeps the left-stick movement locked. Do not bypass menu/mission state or add a second movement driver to hide this symptom.

Earlier validation: C# compile check passed only. See the follow-up above for the identified UI blocker, correction and current test status.
