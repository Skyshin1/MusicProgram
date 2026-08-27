# SonicWorld

VR 水下移动、浮力工具、手电筒、黑匣子、Trigger 声纳与出水水痕的中文说明：
`VR水下玩法使用说明.md`。

`Scenes/SonicWorldDemo.unity` is generated from the project's existing
`SampleScene`, so it retains the configured XR player, actions, interaction
manager, renderer, and OpenXR settings.

## Run

1. Open `Assets/SonicWorld/Scenes/SonicWorldDemo.unity`.
2. Enter Play Mode with the configured PCVR runtime, or use the installed XR
   Device Simulator.
3. Grab and swing or collide the five material objects.

The BGM starts automatically and loops. In desktop Play Mode:

- `Space`: play/pause BGM
- `N`: next track (when more tracks are added)
- `R`: reset all Sonic objects

## Audio mapping

- `Wood.mp3`: wood
- `Metal.mp3`: metal
- `Glass.mp3`: glass
- `Rock.mp3`: stone
- `Slime.mp3`: soft/slime
- `BGM.mp3`: looping stereo music

Collision pitch, gain, filtering, and resonance are continuously derived from
the point velocities of both bodies. Different surfaces play two processed
layers plus a third fused resonance. A grabbed object that is swung without a
collision plays only its own processed material sound when it stops or is
released.

## Integration

Add `SonicImpactEmitter`, `SonicSwingTracker`, `XRGrabInteractable`, and
`SonicMKToonTarget` to a Rigidbody object. Assign one of the generated surface
profiles. `SonicAudioBus` analyses the final listener mix, while
`SonicMKToonWorldDriver` updates runtime-only copies of marked MK Toon
materials.

## Visual behavior

`SonicPointWave` renders 28 layered neon lines over a six-point, editable
Catmull-Rom curve. Player-generated sounds are projected onto the nearest
point of the curve and produce stable pulses that travel in both directions.
The receiver has full strength inside 0.75 m and smoothly fades to zero at
6 m. BGM is analyzed separately and produces a low-strength, left-to-right
background flow. With no active pulse every line is still.

The cyan control points and their translucent polygon are always visible.
In VR, move a controller within 0.16 m and hold Trigger to move a point; Grip
continues to grab normal scene objects. A nearby point turns yellow and a
grabbed point turns magenta.

The control-point count can be edited from the `SonicPointWave` Inspector.
Select an existing point and use `Add Point` to insert beside it, or
`Remove Selected` to delete it. With no selected point, Add splits the
longest control-polygon section. The supported range is 4–16 points and all
operations support Unity Undo.

All five test objects and the marked environment surfaces share
`Assets/Test.mat` (`MK/Toon/URP/Standard/Simple + Outline`). Their audio
surface profiles remain different; only their base shader appearance and
global sound response are unified.

## Monochrome world

`SonicGrayscaleReveal.shader` runs as an XR-compatible URP Full Screen Pass
after post-processing. The world rests in grayscale while all objects keep
using their original materials underneath. Collision and held-object swing
events emit world-space reveal shells from their actual event positions.
Only surfaces crossed by a shell crest and its short trail temporarily show
their original color; the looping BGM does not emit reveal shells.

The wave lines, control polygon, and interactive control nodes are isolated
on the `WaveColor`/`CurveControl` layers. A dedicated transparent render pass
draws them after the grayscale pass, so their cyan-to-magenta colors are
always preserved in both XR eyes while normal world geometry remains under
the monochrome/reveal effect.

Tune the spatial reveal on `SonicColorRevealDriver`: `waveSpeed` controls
expansion, `waveWidth` controls the crest, `trailLength` controls the brief
color trail, and `maximumRadius` controls its reach. MK Toon color properties
remain fixed; sound still drives non-color outline, deformation, and light
band parameters.

## Surface ripples and terrain

Every demo material object keeps `Test.mat` and receives an additional
transparent ripple shell. Its own collision or swing creates the strongest
ring; other spatial sounds fade over 5 m. Up to four rings can overlap and
apply a small normal displacement without changing the source mesh.

MK Toon does not include a native Unity Terrain splat shader in this project.
Use `Tools > Sonic World > Convert Selected Terrain To MK Toon Mesh` to bake
the Terrain Layers into an albedo texture and generate 64×64-quad mesh chunks
with MeshColliders. The original Terrain is retained but disabled. The demo
scene includes a converted 18×18 m example with four chunks.
