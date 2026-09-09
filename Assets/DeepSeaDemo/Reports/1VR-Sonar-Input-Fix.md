# Underwater sonar input — 2026-09-09

Target: `DeepSeaInvestigation_1VR`. No scene reconstruction or source-scene edits.

The full Demo router silently required `flow.Equipped` (the `suit` mission fact) before accepting an underwater Trigger. Physical entry into the water does not set this fact. The normal platform task remains unchanged, but this hidden gate conflicts with permanent hand sonar and prevents free testing after a direct dive.

Removed only the suit prerequisite from the sonar input route. Running-game, pause/busy, UI priority, world-action priority, per-hand holding, QTE, underwater and global cooldown checks remain. A held flashlight/tool still owns that hand's Trigger; the other empty hand can emit. Uses the existing `EmitPlayerAt` path for water pulses and semantic AI/noise events; no duplicate emitter/input route added.

## Player controls

1. Start the game using `New Game` or continue your checkpoint.
2. Put your head underwater, leaving at least one hand empty.
3. Point the empty hand away from menus, terminals and document interaction prompts.
4. Press that hand's index-finger Trigger once, then release. Grip is for holding objects, not sonar.
5. Wait the configured cooldown (default 1.5 seconds) before pressing again.

`SONAR PULSE SENT` confirms the emission path was called. Look for the expanding sweep on nearby objects and white outlines; the full globe wireframe remains intentionally disabled. A surface/cooldown message explains those rejected presses. Mission equipment requirements still apply to progression and oxygen; sonar availability does not complete any platform task.

Editor presses are recorded in `Logs/DeepSeaDemo/Sonar-Input.txt` even after the first-minute startup probe expires. Each includes side, route result, origin, water state/depth, suit fact and holding state. These are diagnostics, not assertions that the water shader or outline rendered successfully.

Compile checks passed. Gate regression results are written to `Logs/DeepSeaDemo/Sonar-Gate-Regression.txt`. Quest Trigger-to-render and AI reactions still require Play/headset verification.
