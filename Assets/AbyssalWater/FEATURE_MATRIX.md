# Feature matrix

| Demo requirement | Implementation |
| --- | --- |
| Procedural LOD mesh | Camera-relative concentric square clipmap with outer skirt |
| Controllable Gerstner waves | Deterministic band spectrum, two editable curves, manual waves |
| Ocean anti-tiling | Differentiable spectrum phase warp plus stochastic rotated normal sampling |
| Optical micro waves | Separate multi-direction, logarithmic short-wave spectrum with depth filtering |
| Water absorption | Linear-colour-to-coefficient Beer–Lambert optical path |
| Planar reflection | Mono center-eye URP reflection shared by XR eyes |
| Refraction / Fresnel / highlight | IOR-driven Schlick Fresnel, screen refraction, main-light specular |
| Crest transmission | Back-lit crest transmission from physical wave crest/compression |
| Crest foam | Height and horizontal Jacobian compression |
| Shore / contact foam | Opaque-depth shoreline plus dynamic velocity/contact response |
| Waterline above and below | Shared displaced surface function and iterative ray intersection |
| Underwater fog / scatter | Exponential extinction plus HG phase approximation |
| Underwater caustics | Macro + micro surface curvature, refracted neighbouring-ray area focus, depth filtering and IOR dispersion |
| Dynamic object interaction | Compute height-field propagation with moving-domain shift |
| Player/fish/submarine emitters | Reusable `AbyssalWaterInteractor` |
| Buoyancy/query API | CPU-matched analytic spectrum sampling |
| Stylized option | Available by retuning the profile/material; upstream textures remain available |
| PCVR / Quest presets | Profile-controlled wave, simulation and reflection budgets |

The referenced video lists improved interaction, caustics and shoreline foam as
future work. Abyssal Water implements all three as extensions rather than
preserving those limitations.
