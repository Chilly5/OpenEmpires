---
name: walls-shadow-overlap-findings
description: Why stone-wall shadow-overlap can't be fixed by UV crop or a simple depth nudge
metadata:
  type: project
---

Stone walls render as camera-facing billboard quads (one per tile) via `WallSpriteRegistry` + `Billboard.shader`. The "shadow" is baked into each sprite PNG, on the screen-left/base of the wall (invariant under camera yaw since the quad always faces the camera). Adjacent wall art (~1.6 world units wide on 1-unit tiles, scale 6.61, UvScale 0.85) overlaps neighbors by ~0.6 units, so a wall's left-base shadow bleeds onto its screen-left neighbor.

Verified findings (May 2026, editor render rig + pixel scan of `Stonewall90.png`, 2048²):
- **UV crop is useless:** dark/shadow pixels start at u 0.380, stone body at u 0.390 — shadow is fused to the body base (~1% of width left of it). Any crop that removes the shadow also clips the wall foot. Sliding `offset.x` just shifts the whole wall sideways, shadow included.
- **Simple depth-nudge (Option B) breaks unit occlusion:** wall and unit sprites share one depth buffer (the stencil depth-punch in `Billboard.shader` handles sprite-vs-terrain/unit). For N-S wall runs the screen-left neighbor is naturally *further* (depth diff ~0.61), so flipping order needs a large depth bias that, applied globally, makes walls wrongly occlude units.

Remaining viable approaches (both keep shadows — required, see [[walls-keep-baked-shadows]]): (1) proper screen-x sprite sort in a transparent pass + depth pre-pass for unit occlusion; (2) reduce sprite overlap so the shadow no longer reaches the neighbor.
