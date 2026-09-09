# Quest Link startup investigation — 2026-09-09

## Observed in the local Unity Editor log

- 15:42:49: Demo manually initialized the configured OpenXR loader.
- The native report identified the Oculus runtime, version 1.207.0.
- 15:42:50.038: `xrSuggestInteractionProfileBindings: XR_ERROR_PATH_UNSUPPORTED`.
- 15:42:50.040: `xrCreateActionSpace: XR_ERROR_PATH_UNSUPPORTED`.
- The diagnostic report identifies `eyegaze/pose`, subActionPath `/user/eyes_ext`.
- The session went from READY to SYNCHRONIZED, then STOPPING, IDLE and EXITING in the same second. It did not reach VISIBLE / FOCUSED in the recorded event sequence.
- The Unity OpenXR restarter shut down the session. The Demo's old immediate `display.running` message appeared during shutdown and was not a valid acceptance result.
- 15:43:09: another initialization attempt; the following native report records IDLE -> EXITING.

These observations narrow the failure to session startup/shutdown, not missing scene models. The eye-gaze binding failure is a concrete configuration issue to remove; its causal role in the entire shutdown still requires a new hardware run.

## Changes

1. Disabled only **Eye Gaze Interaction Profile / Standalone** in `Assets/XR/Settings/OpenXRPackageSettings.asset`. This is a shared Windows OpenXR setting, not a scene-only setting. Other controller and hand-tracking profiles are unchanged. Re-enable that exact feature if an eye-tracking-capable target later needs it.
2. Demo startup diagnostics now require the display to remain running for three seconds, keep watching for display loss, and record runtime-requested exit. Exit requests are observed, not blocked.
3. No scene reload/save, platform changes, camera deletion, package upgrade or runtime-registry change.

## Validation

- Runtime/editor C# compile check: passed, existing CS0649 warnings remain.
- Headset picture, tracking, stereo and persistence of the Link session: not yet verified after this change.
- Re-enter Play after script import. Check the newest entries in `Logs/DeepSeaDemo/XR-Startup.txt` and the native OpenXR report if the session exits again.
- Existing Windows EXE predates these fixes and must be rebuilt after Editor verification.

## Follow-up: 15:57 run, Meta runtime service evidence

Eye-gaze ActionSpace errors no longer appear after the configuration change. One binding error remains for Microsoft's hand interaction profile (`/user/hand/left/input/select/value`); it is distinct from the exit request and is not yet proven to cause it.

The Meta service log (`Service_2026-09-09_15.19.09.txt`) records at 15:57:43.165, for this Unity process:

```
Package trying to render while closing.
Requesting close, again: 26836 (C_ProgramFiles_Unity6000.3.7f1_Editor_Unity)
RequestAppShouldQuit(26836)
```

The client log at 15:57:43.411 then records:

```
OXRSessionData::PollOVRSessionStatusChange exit requested from runtime UI
```

This is stronger evidence for the immediate exit than the unrelated shader/scene hypotheses: the runtime considers the existing Unity process to be closing and requests another close during registration. How it first entered this state has not been established.

Next controlled test: save any wanted scene edits, exit this Unity Editor normally, fully exit/restart Meta Link, reconnect the headset and reopen this project. Do not force-kill any editor or other Unity projects; several Unity processes are open. Re-enter Real VR mode and test Play after import. A fresh process/session is required to distinguish the stale runtime state from additional configuration issues. No further XR profile or scene changes were made in this follow-up.

## Follow-up: original scene and another project comparison

User reports another Unity VR project works, while original `1-VR` produces neither headset graphics nor controls. The original scene does not contain the Demo-only manual bootstrap, and MusicProgram's global XR startup flag was still false. Therefore lack of exit in the original scene did not establish a working XR session.

- Restored Standalone `m_InitManagerOnStart` to true in MusicProgram's XR management asset.
- Removed automatic global XR-setting writes from `DeepSeaDesktopPlayGuard`; it now warns only. It no longer calls SaveAssets during Play transitions.
- Existing Demo bootstrap reuses a loader started by Unity instead of creating a second session.
- Added read-only `DemoXRLiveDiagnostics`: on Play in `DeepSeaInvestigation_1VR`, record six snapshots to this project's `Logs/DeepSeaDemo/XR-Live.txt` with scene path, XR loader/display, camera stereo/target/allow-XR, actual simulator activation, Input System tracked devices and hand input state.
- Original `1-VR` has user/external changes since the earlier baseline; these were not reverted or edited by this follow-up.
- The global Editor.log currently belongs to another project (`EchoPuzzle`); its successful FOCUSED events are NOT acceptance evidence for MusicProgram.
- Compilation passed. Target scene headset rendering and controls still require runtime verification.

## Follow-up: 16:24, confirmed stale application identity across editor restart

Project-owned snapshots show the target Demo at 16:24:07 with AutoXR=True, active OpenXRLoader, DisplayRunning=True, Main Camera Stereo=True / Eyes=Both / AllowXR=True, RealVR input and an inactive simulator. Play exits the next second. Thus the automatic initialization fix took effect but does not clear the external exit request.

The Meta service records the new Unity PID 16524 registering at 16:24:29.115, but its AppTracker still reports `Requesting close, again: 26836` for the same Unity 6000.3.7f1 application identity and calls `RequestAppShouldQuit(16524)`. The old process's closing state is retained across editor restart. The service log/session still dates from 15:19:09.

The other successful test registered at 16:11:13.682 as Unity 6000.0.33f1 (PID 2152), a different application identity. Its success verifies working headset/Link capability, but does not disprove stale state for the 6000.3.7f1 application identity.

No more scene or rendering modifications in this follow-up. The next isolated recovery action is restarting `OVRService` (Oculus VR Runtime Service), which interrupts all active Link sessions and therefore requires user coordination/approval. Do not force-close Unity or restart the service without coordinating other VR work. After reconnection, test the target Demo in RealVR; the user's last 16:24:31 attempt selected XRSimulator, while earlier RealVR attempts also failed.
