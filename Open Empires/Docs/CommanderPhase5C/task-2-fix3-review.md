# Task 2 fix round 3 — scoped re-review

## Verdict

- **Prior Important old-amount-qualifier finding: ADDRESSED.** A selected `resourceAmount` correction now checks the old phrase's number in `NamesCurrentValue`, then checks its named resource and any explicit `additional`/`stockpile` mode against the retained node (`CommanderRequestGraphDraft.cs:295-333`). The same resource/mode check applies to the replacement before the graph is cloned or reparsed (`:221-226`), so a correction cannot imply a second resource/mode leaf change. Unknown or mismatched qualifiers return null to the existing local re-ask path, leaving the graph and ticket intact with zero further HTTP.
- **New breakage in this narrow diff: none found.** The source-matched saved EditMode result `b97eb8b879224c308504a4596f158e57` is 10/10: wrong old resource/mode and wrong new resource/mode retain the 400 AdditionalGathered Food draft; a valid qualified old-to-new amount changes only the amount; direct old-to-new structure, source and count controls pass alongside existing positive amount and negated-count controls.
- **Scoped spec compliance: YES. Task quality: PASS for this fix.** This does not establish live-provider/native/UI behavior, package readiness, a full regression, or overall Phase 5C acceptance.

## Evidence and limits

Reviewed only `task-2-fix3-brief.md`, the fix3 report, frozen incremental package, changed production/test lines and the saved raw result. The live production/test SHA-256 values match the package (`A5935790BF05503C4F1FD49219135799FD0B91E5E329FB3FC42B112BD0F2E157`, `71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA`). No tests, Unity/MCP, provider calls, builds or broader unchanged-code audit were run for this review.
