# OpenEmpires Spearman Rebuild Analysis

## Investigation scope

This analysis covers the active Unity project at `D:\unity_projects\OpenEmpires\Open Empires`, the current working-tree Spearman assets, the committed gameplay/view architecture, and the prior Spearman experiments under `D:\unity_projects\OpenEmpiresTemp`.

The current working tree is intentionally treated as user-owned and dirty. Existing Spearman files are not overwritten during development, and the live `SampleScene` prefab reference remains unchanged until the animated replacement passes its promotion gates.

## Current Spearman implementation

The current working `Assets/Prefabs/Units/Spearman.prefab` is a rigid modular prefab. Its root contains `BoxCollider` and `UnitView`; its visual children are `Body_Team`, `Cylinder`, `Helmet`, `Limbs_Boots`, `RoundShield`, and `Speartip`, plus the existing `SelectionRing`.

The imported working FBX at `Assets/Models/Units/SM_Spearman.fbx` contains rigid mesh objects. The prefab has:

- no `Animator` component;
- no `Animation` component;
- no `SkinnedMeshRenderer`;
- no imported animation clips;
- multiple `MeshFilter`/`MeshRenderer` parts animated by transform scripts.

The prior temporary production report and the current `SpearmanProductionAssetIntegrationTests` explicitly describe and enforce a zero-rig, zero-Animator asset. Those assumptions belong to the previous rigid asset and must not be applied to `Spearman_Animated.prefab`.

## Current animation, movement, and combat flow

- `UnitCombatSystem` is deterministic and authoritative. It changes `UnitData`, including `LastAttackTick`, `LastDamageTick`, `State`, `IsCharging`, cooldowns, health, and death. It does not depend on renderers or Unity animation.
- `UnitMovementSystem` advances fixed-point simulation positions and movement states. It does not depend on visual animation.
- `UnitView` is the render boundary. It interpolates simulation positions, turns the visible unit, manages selection/health/fog feedback, detects attack and damage ticks, starts corpse cleanup, and currently instantiates `UnitAttackVisualAnimator`.
- `UnitAttackVisualAnimator` creates a runtime `VisualRoot`, reparents rigid visual children, discovers named weapon parts, and applies procedural transform offsets. Applying it to a Humanoid rig would wrap and transform the animated hierarchy and create double animation.
- `GameSetup` instantiates the serialized unit prefab, assigns team/stencil/silhouette materials to child `Renderer` components, initializes `UnitView`, and registers the view with selection systems.

## Animator support

Unity Animator support is available in the project through Unity's animation module. The project currently has no Animator Controller assets or animation clips, and `UnitView` has no Animator-driving path.

An Animator-based unit can coexist with existing procedural units if `UnitView` selects exactly one visual driver at initialization:

- animated prefab present: use `UnitAnimatorVisualDriver` and do not construct `UnitAttackVisualAnimator`;
- animated driver absent: preserve the existing procedural path unchanged.

Root motion must remain disabled. The deterministic simulation continues to own position, rotation intent, combat results, and multiplayer synchronization.

## SkinnedMeshRenderer support

The surrounding unit systems generally operate on `Renderer`, not specifically `MeshRenderer`, so `SkinnedMeshRenderer` is structurally compatible with spawning, damage flash, corpse fade, selection silhouettes, fog visibility, and renderer discovery.

Compatibility still requires explicit validation because the current material pipeline assumes one primary material followed by stencil and silhouette materials. The rebuilt character therefore uses a low number of single-material skinned renderers sharing one armature and atlas. The following must be tested on both LODs:

- selection outline and silhouette pass;
- textured team-color tinting;
- damage flash and restoration;
- health-bar height derived from the root collider;
- fog visibility and renderer enable/disable behavior;
- corpse fade after the Death animation.

## Required code changes

### Modify

- `Assets/Scripts/Units/UnitView.cs`: select animated or procedural presentation, feed movement/combat signals to the Animator driver, trigger Hit/Death presentation, and preserve existing cleanup while skipping only the legacy visual tip-over.
- `Assets/Scripts/Core/GameSetup.cs`: preserve textured source materials for explicit `*_Team` renderers by using cached per-player tinted variants; keep legacy `Body*`/`Sphere*` behavior unchanged.

### Add

- `UnitAnimatorVisualDriver`: a reusable, view-only Animator bridge.
- Spearman animation test harness and EditMode/PlayMode integration tests.
- `Spearman_Animated.prefab`, Animator Controller, clips, LOD meshes, materials, textures, reference sheet, and isolated validation scene.

### No gameplay changes

`UnitCombatSystem`, `UnitMovementSystem`, `UnitData`, economy logic, command serialization, and multiplayer state remain unchanged. Block is presentation-only and does not create a simulation state or alter damage.

## Promotion rule

The current `SampleScene` Spearman reference must not be changed until all new integration tests pass and the model's silhouette is approved with the real `RTSCameraController`: orthographic projection, 30-degree pitch, 45-degree default yaw, camera arm distance 55, and orthographic sizes 5, 15, and 40.
