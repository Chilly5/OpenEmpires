# D3b Windows player model setup and acquisition

Source/focused checkpoint2026-10-08. This delivers the runtime player path rather
than requiring Node/Python/compiler setup. It is not a recognition-quality winner,
real network/microphone or packaged Windows/Web acceptance claim.

## Player controls

Open the compact Voice setup deliberately. Desktop LocalModels provides a bounded
catalog cycle, exact download size/language, manual .bin import path, Download,
Import file, Use verified and Cancel. Startup stays minimized; merely opening setup
or choosing a candidate does not record, upload, submit text or approve a plan.

Download/import verifies and installs without selecting the model. Use verified is
a separate explicit action: only an installed checksum-valid language-compatible
entry can change the local model preference/provider. English-only candidates do
not silently change another language. A fresh Record action remains necessary.
Unmeasured candidates are labeled honestly; Tiny remains the default preference
unless the player explicitly chooses another verified model. No best-quality claim.

This desktop path uses the bundled trusted manifest and writable per-user
`Application.persistentDataPath/CommanderVoice/Models/<sha>/<canonical-file>`, not
installation StreamingAssets. The existing editor developer cache remains a separate
lookup path. To install a developer-cached binary in the player store, explicitly
import that exact .bin or download it; developer cache presence is not a packaged
player model certificate. Web has no native model controls or desktop file loading.

## Worker and ownership

`CommanderLocalModelProvisioner` captures detached model definitions and path on
the caller thread. Network, file copy and SHA work run off UI, using64KiB chunks,
exact expected size/hash, bounded catalog/storage and fixed pinned public URLs.
No Authorization header, reusable key, audio or game context is sent for models.
Requests have a fifteen-minute bound; transfer is safely restartable, not yet range
resumable. There is no automatic retry, vendor/model fallback or activation.

One process-wide actual-work slot rejects replacement/queueing even after UI cancel
if a transport ignores cancellation. Dispose cancels and defers client release until
actual work finishes. Per-cache exclusive file lease prevents another active
acquisition. Lock-file existence alone is NOT liveness: after a crash its OS lease
is gone and a later operation may safely reopen the marker, without PID guesses.

Only after that exclusive lease, bounded exact owned `.download-<guid>.partial.bin`
files under hash directories may be removed as interrupted staging. Installed
models and unrelated notes are preserved. Current operation failure/cancellation
cleans only its own stage/lease. Verified existing models are reused; corrupt
existing files report an error and are never overwritten. Capacity checks enforce
the3GiB cache and64MiB disk reserve. Corrupt final cache removal remains a separate
deliberate operator/player file action, not an automatic destructive repair.

Temporary .partial.bin permits the same trusted verifier to recheck staging before
exclusive same-filesystem File.Move. Load still performs its independent verification
lease. Import preserves its source file. Safe errors explain missing/corrupt/cache/
network/busy states without raw exception details or signed redirect URLs.

## UI affinity and limitations

Close/focus loss/disable/reset/provider switch invalidate the model setup generation
and cancel pending work. Completion checks the same host/runtime generation before
showing/adopting anything. Progress reads bounded counters on the Unity update path;
the worker does not mutate UI. Older completion cannot choose a model for a new host.

Actual visual layout, path-entry workflow, large-download performance, application
crash recovery and language-selection UX still require packaged/runtime checks.
The added setup card uses the existing HUD style and remains deliberately opened;
this source checkpoint does not resolve the known expanded HUD overlap or certify
all720p/high-DPI layouts. Medium/small/base candidate support and measured recognition
selection remain separate tasks. small.en still has the earlier network gate.

## Focused evidence

- Initial APIRED8f3f7b67:10failed before player worker existed; complete XML retained.
- First worker/UI8356e375:7pass/5fail of12,8.7873812s; stage suffix + missing UI.
- Affected432efa70:75/75pass,49.7131815s; firstPlay3d1eda11:9/9pass,4.008026s.
- Narrow Sol review identified stale crash marker; recoveryRED10951937:2failed,
  full XML retained. Fixed exclusive lease and owned-stage cleanup; active-lock
  fixture holds a real exclusive stream instead of treating marker text as liveness.
- Final Edit67890779d689490786e474978c11c2b7:77/77pass,0fail/skip,50.2078795s.
  Worker12/presentation31/consent11/legacy13/trust10; offline controlled handlers.
- Final Playe355266af34a41dbbe6c1dd7de78e9ab:9/9pass,0fail/skip,3.6898111s.
  Two synthetic input-device cases plus seven existing mock voice routing cases.

No full historical regression, actual model download/import through a packaged UI,
physical microphone, provider request or paid transaction ran in this checkpoint.
Source hashes/checkpoint and current manifest are adjacent. Goal remains active.
