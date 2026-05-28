---
name: walls-keep-baked-shadows
description: Do not remove or edit out the baked shadows on stone/palisade wall sprites — shadows must stay
metadata:
  type: feedback
---

The baked-in shadows on the wall sprite textures (`Stonewall*.png`, palisade) must be kept. Do NOT propose or implement removing/erasing/softening them, and do not edit the texture PNGs to fix shadow-overlap problems.

**Why:** When stone wall shadows overlap onto adjacent walls, the user wants the overlap fixed via rendering/layout — not by altering the art. They explicitly rejected a texture-edit approach.

**How to apply:** Solve wall shadow-overlap with draw order (screen-left neighbor on top) or by reducing wall sprite overlap (scale/UV), never by changing the textures. See [[walls-shadow-overlap-findings]] for why the obvious quick fixes don't work.
