# Spearman Rebuild Report

## Result

The Animator-based Spearman is implemented and promoted for normal gameplay in `SampleScene`. The legacy `Spearman.prefab` remains available as the procedural rollback/reference asset, and recovery scenes were not promoted.

## Architecture

- Simulation, combat balance, movement, economy, commands, serialization, and network schemas are unchanged.
- `UnitAnimatorVisualDriver` is a view-only bridge. `UnitView` uses it when present and retains `UnitAttackVisualAnimator` as the fallback for legacy units.
- Animator root motion is disabled. Block is only the `InCombat` guard presentation and has no simulation effect.
- Death plays the rigged presentation first, then preserves the existing corpse wait, fade, collider disable, and removal lifecycle.

## Asset and Rig

- Editable source: `D:\unity_projects\OpenEmpiresTemp\Spearman_Rebuild_Source.blend`
- Unity model: `Assets/Models/Units/Spearman/SM_Spearman.fbx`
- Concept source of truth: `Assets/Models/Units/Spearman/Reference/Spearman_Reference_Sheet.png`
- One 2K atlas: `T_Spearman_Atlas.png`
- Four unit materials: body, metal, skin, and textured team colour.
- Valid Unity Humanoid Avatar using standard deformation bones only.
- `SpearSocket` is parented to `RightHand`. To clearly convey light infantry class identity, no offhand shield is equipped.
- LOD0: 7,994 triangles. LOD1: 3,192 triangles.
- LOD transitions: 0.08 / 0.005. The lower final threshold is required to keep the unit visible at orthographic size 40.

## Animation

`AC_Spearman.controller` contains Idle, Walk, RunCharge, Attack, Block, Hit, and Death clips at 30 FPS.
- **Idle**: Upright agile soldier posture with subtle breathing and firm weapon grip.
- **Walk**: Natural light infantry marching stride with fluid arm swing cadence.
- **RunCharge**: Athletic sprint with spear leveled forward.
- **Attack**: Sharp spear thrust and recovery.
- **Block**: Two-handed defensive spear parry guard across torso.
- **Hit**: Agile flinch and recovery.
- **Death**: Dramatic fall and weapon drop.

## Unity Integration

- Candidate prefab: `Assets/Models/Units/Spearman/Spearman_Animated.prefab`
- Test scene: `Assets/Scenes/Tests/SpearmanAnimationTest.unity`
- `SampleScene` now references the candidate prefab GUID `d7103a8835ae1d34792b09a4dbba8d8a`.
- The prefab includes `Animator`, `UnitAnimatorVisualDriver`, `UnitView`, one root `LODGroup`, both LOD renderer sets, attachment transforms, the existing selection ring, and the 0.5 x 1.1 x 0.5 collider.
- `GameSetup` preserves textured source materials on explicit `_Team` renderers and caches player-tinted copies. Legacy `Body*`/`Sphere*` behavior is unchanged.
- Existing renderer-based selection silhouette, damage flash, health-bar height, corpse fade, and fog activation work with both LODs' `SkinnedMeshRenderer` components.

## Validation Evidence

- Model import: valid Human Avatar; all seven clips present; root motion disabled.
- EditMode: 29/29 tests passed (21 existing regression tests plus 8 candidate tests).
- PlayMode: 1/1 candidate integration test passed through the actual `GameSetup` spawn path, selection, health-bar creation, textured team tint, guard/charge/attack/hit presentation, fog hide/show, and death/fade/removal.
- Runtime formation scene: 1, 5, and 20 unit formations loaded with 26 animated instances and no Spearman-attributable console errors.
- Rendering snapshot reported 104 visible skinned meshes for the combined formation scene.
- Actual runtime camera used orthographic projection, 30-degree pitch, 45-degree yaw, and 55-unit arm distance.
- Captures:
  - `Validation/Spearman_Idle_Ortho5_One.png`
  - `Validation/Spearman_Idle_Ortho15-1.png`
  - `Validation/Spearman_Idle_Ortho40.png`
- The spear, shield, helmet, team-colour tabard, formation spacing, and infantry class remain identifiable at normal and strategic camera scales.

## Files Changed

- `Assets/Scripts/Units/UnitAnimatorVisualDriver.cs`
- `Assets/Scripts/Units/UnitView.cs`
- `Assets/Scripts/Core/GameSetup.cs`
- `Assets/Scripts/Units/SpearmanAnimationTestController.cs`
- `Assets/Editor/SpearmanRebuildAssetBuilder.cs`
- `Assets/Editor/OpenEmpires.Editor.asmdef`
- `Assets/Tests/EditMode/SpearmanAnimatedIntegrationTests.cs`
- `Assets/Tests/PlayMode/OpenEmpires.PlayModeTests.asmdef`
- `Assets/Tests/PlayMode/SpearmanAnimatedPlayModeTests.cs`
- `Assets/Models/Units/Spearman/**`
- `Assets/Scenes/Tests/SpearmanAnimationTest.unity`
- `Assets/Scenes/SampleScene.unity` (Spearman reference only)
- `Spearman_Rebuild_Analysis.md`
- `Spearman_Rebuild_Report.md`

## Known Issues / Remaining Manual Check

- A two-client multiplayer smoke session was not available in this editor run. No deterministic code, data schema, root motion, combat calculation, or network serialization was changed; multiplayer behavior therefore remains on the existing simulation path.
- Unity MCP stopped answering after the external `SampleScene` reference refresh. The promoted reference was verified directly in scene YAML; the candidate itself had already passed EditMode and PlayMode integration before promotion.
