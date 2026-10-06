# Phase 4G current-source consistency

Snapshot: 2026-10-06 narrow result-binding refresh.

`source-manifest.json` records byte-level SHA-256 hashes of current production files, test sources, documentation, and relevant package/assembly inputs. The older verification block is retained under `historical_verification`; it is not evidence for these new hashes. Fresh focused job IDs and boundaries are in `narrow-verification-2026-10-06.json`.

`source-consistency.json` is a separate final seal containing the manifest hash and the result of recomputing every listed hash. It is deliberately excluded from the manifest to avoid a circular hash. The manifest likewise does not hash itself. Any later source/document changes invalidate that seal until recomputed.

This is consistency evidence, not a full regression, security audit, standalone build, provider contract certification, or canonical-data migration. Existing dirty UI/provider/voice/package changes are preserved and may appear in integration hashes; their presence must not be misread as verification of those features.

AntiGravity must import/reload the recorded snapshot, confirm hashes, then generate its own current acceptance evidence. If a listed hash differs, identify the changed file and update/revalidate the evidence before relying on any result.
