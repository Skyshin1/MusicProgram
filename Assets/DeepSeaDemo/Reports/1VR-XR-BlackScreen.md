# VR black screen / Scene view flicker — 2026-09-09

## Confirmed failure

After the computer restart, project-owned diagnostics show OpenXR remaining active and the Main Camera rendering stereo. Unity's frame recording then throws:

`ArgumentException: Slice count for source and destination texture doesn't match.`

Call site: `LargeBodyAtmospherePass.RecordRenderGraph`, `AddCopyPass(shaftTexture, historyWrite)`, previously line 128.

The half-resolution source inherits the camera's two-slice XR texture descriptor. The persistent history uses `RTHandles.Alloc(width, height, colorFormat: ...)`, which creates a normal single-layer texture. RenderGraph rejects the copy and aborts recording the frame. This is separate from the earlier OpenXR runtime-exit problem.

## Applied safe fallback

- Skip **LargeBodyAtmosphereFeature** for stereo cameras.
- Also guard the pass itself against stereo, non-2D or multi-slice source targets before creating/copying history.
- The atmosphere shader uses mono view/projection and ordinary TEXTURE2D sampling, so allocating two history slices alone would not be a complete stereo fix.
- Desktop mono ocean light shafts remain enabled. Stereo temporarily has no optional large-body atmosphere shafts; proper stereo shafts remain deferred.
- No scene was reloaded, saved, rebuilt or rearranged. Water surface, underwater fog, sonar, lantern, flashlight and UI features were not disabled by this patch.
- These changes are in the embedded water package, so its stereo consumers share the safety guard; the original 1-VR scene asset was not edited.

## Validation

- Full water runtime C# compilation with WEBGPUWATER_URP: passed via `Tools/Test-WaterRuntimeCompile.ps1` (existing unused/serialized-field warnings only).
- Unity import and actual Quest image/flicker verification: pending user exit from Play and subsequent retest.
- This is a rendering safety fallback, not certification that all remaining water shaders, input or the game flow pass stereo acceptance.
