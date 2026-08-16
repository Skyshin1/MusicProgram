# Abyssal Water — Unity 6 URP / OpenXR

A compact, profile-driven ocean system for the underwater game prototype. The
normal/foam textures and texture vocabulary are derived from the MIT-licensed
**Uber Stylized Water v1.1.2** project, while the realistic optical model,
camera-relative LOD ocean, waterline, dynamic waves and wave-curvature caustics
are implemented in `Assets/AbyssalWater`.

## Open the showcase

Open `Assets/AbyssalWater/Samples/AbyssalWaterShowcase.unity` and enter Play
Mode.

- `1`: above-water view (planar reflection and crest transmission)
- `2`: waterline view
- `3`: underwater view (Beer–Lambert absorption and physical caustics)
- `Space`: inject a large dynamic ripple

The animated submarine, buoy and floating cargo carry `AbyssalWaterInteractor`
components and disturb the near-field height simulation.

## Editing workflow

Select `AbyssalWater_PC_VR_High.asset`.

Simple mode exposes the controls used most often: wave height/scale/speed,
wind, choppiness, transmission colour/reference distance, reflection,
caustics, foam and underwater density. Expand **Advanced** for the full video
feature set:

- wavelength-to-amplitude and wavelength-to-direction-spread curves;
- generated spectrum bands plus manual Gerstner waves;
- optional continuous phase-warp anti-tiling and stochastic rotated normal tiles;
- a separate multi-direction optical micro-wave spectrum;
- water IOR, refraction, Fresnel reflection and roughness;
- Beer–Lambert absorption, scattering colour/strength and anisotropy;
- crest transmission, crest/shore/contact foam and meniscus controls;
- Jacobian caustic focus, scale, dispersion and depth fade;
- bidirectional waterline, underwater distortion and god rays;
- dynamic wave area, resolution, propagation speed, damping and substeps;
- infinite-ocean LOD count, density, base size and outer skirt.

## Physically motivated absorption

The profile stores an intuitive transmission colour `C` at reference distance
`d`. It converts this to the linear-space absorption coefficient:

`sigmaA = -log(max(C, epsilon)) / d`

The surface and underwater passes then evaluate the actual view-ray path
length `L`:

`T = exp(-sigmaA * L)`

This is not a shallow/deep colour interpolation. The surface uses opaque depth
behind the refracted water pixel; the underwater pass uses the distance from
the camera to opaque geometry or to the displaced water surface.

## Wave-driven caustics

The shader refracts the main light through the combined macro- and micro-wave
normal and traces it to the receiver depth. Three neighbouring rays form the
optical mapping Jacobian; inverse mapped area produces the focus, with depth
attenuation and IOR-based RGB dispersion. The multi-direction micro spectrum
creates irregular caustic cells and is filtered progressively with depth.
Dynamic height-field velocity contributes a smaller local focus term, so
object wakes also perturb the underwater light pattern. No scrolling caustic
texture is used by the Abyssal shaders.

The finite analytic spectrum is de-tiled with a differentiable phase warp. Its
analytic derivative feeds the same displaced normal and caustic calculation,
so the pattern changes continuously without square patch seams. Separately,
the two material normal maps use optional stochastic cell rotation and blending.

## VR quality tiers

- **PCVR High**: up to 12 macro + 8 micro waves, full stochastic normals, 256–512 dynamic texture, reflection every frame.
- **VR Balanced**: up to 12 macro + 5 micro waves, half stochastic blend, at most 256, half-rate reflection.
- **Quest Standalone**: 6 macro + 3 micro waves, phase warp only, at most 128, half-rate low-resolution reflection.

Planar reflection is rendered once from the center eye and shared between both
XR eyes. The underwater pass uses URP XR texture macros and RenderGraph.

## Runtime API

- `AbyssalWaterSystem.SampleSurface`: analytic position, normal and velocity.
- `AbyssalWaterSystem.GetWaterHeight`: displaced surface height.
- `AbyssalWaterSystem.IsUnderwater`: camera/gameplay water state.
- `AbyssalWaterSystem.EnqueueImpulse`: direct dynamic-wave injection.
- `AbyssalWaterInteractor`: automatic wake and surface-crossing impulses.
- `AbyssalBuoyancy`: multi-point lightweight buoyancy.

Dynamic-wave GPU readback is intentionally not used for buoyancy; it would add
latency and synchronization stalls in VR. Buoyancy samples the same analytic
wave spectrum, while the render surface includes both analytic and simulated
displacement.

## Third-party license

The imported normal/foam textures and upstream license remain in
`Assets/ThirdParty/UberStylizedWater`. See its `LICENSE.txt`. The upstream
Shader Graphs, prefabs and demo are intentionally omitted: they hard-code their
original package path and are replaced by the URP/VR implementation in this folder.
