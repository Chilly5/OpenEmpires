# G12 client compatibility foundation — PARTIAL

2026-10-08. Full GrandFix remains ACTIVE. This is **not** backend enforcement,
genuine peer certification, relay-retention closure or release acceptance.

## Delivered client behavior

Authentication declares protocol revision2, native encoding `legacy-or-sourcekind-v1`,
expected server recovery policy `bounded-full-history-v1` and a generated SHA256
source/data identity. Existing JSON and binary `CommandSerializer` bytes are unchanged:
unrestricted legacy batches remain legacy; restricted batches retain their existing
int.MinValue marker and version1 source-kind envelope. No new authoritative AI packet.

`NetworkCompatibilityBuildGuard` generates `Assets/Resources/Network/LockstepCompatibility.json`
before a build, and exposes **Open Empires / Network / Generate source-matched compatibility
identity** for Editor. Runtime source/configuration/assets and metadata, Web runtime
scripts/plugins, package identities and ProjectSettings participate in a deterministic
ordinal-path/content digest. Editor rejects stale generated data. Windows/Web builds
from frozen common inputs should share the identity; fresh dual-target proof is pending.
This is release compatibility, not authentication, anti-cheat or executable integrity.

Inventory caps:20000 selected Asset files,128MiB per selected file,4MiB canonical
digest inventory; inputs are streamed, not loaded as whole files. Editor/test
inputs, shared StreamingAssets, generated identity/self-metadata, credential stores
and media/models are excluded as documented in the code. Linked input directories/
files fail closed. Source/data bytes, not HEAD or executable filename, determine ID.
Regenerate after future source/data changes; historical checkpoint IDs do not certify
future edits. Package/release security inventories remain separate.

Client authentication requires an exact profile acknowledgement while Authenticating.
Legacy/missing/mismatched profiles cannot admit queue/ready/start/command traffic.
Unacknowledged match messages never reach the simulation callbacks. Duplicate or
late acknowledgements cannot rewind/reopen accepted state. Queued native messages
are scoped to connection epoch; connect/disconnect clears old receive/send queues,
and receive loops retain their own socket/token/epoch rather than adopting replacements.
Browser callbacks check current socket identity, so replaced/closed sockets cannot
publish late events or clear a newer socket.

Auth JSON uses escaped serialization. Login response logging no longer prints the
credential-bearing body; malformed server-parser and auth-rejection diagnostics omit
payload/exception prose. No real tokens, license values, environment dumps or process
command lines were inspected. G01 credential invalidation remains operator-owned.

## Evidence

- Initial `network-compatibility-red-c7199a72.xml`:9/9 failed for absent identity,
  legacy admission, unacknowledged callbacks, unsafe auth interpolation and payload echo.
- Intermediate `network-compatibility-first-green-524bf199.xml`:27/27,11.7788314s.
- Scoped Sol review found late/duplicate acknowledgement and missing Web `.jslib`
  inputs. `network-compatibility-review-red-24b114f5.xml`:4/4 failed, also proving
  queued messages survived disconnect. Corrected fixtures explicitly begin in
  Authenticating so mismatch checks cannot pass by ignoring a disconnected message.
- `network-compatibility-affected-edit-67645995.xml`:46/46,31.2433458s; network13,
  session-provider15, gateway3 and economy15 (including existing restriction codec).
- Final `network-compatibility-final-edit-71233c74.xml`:13/13,5.1680873s,
  tightened authentication-state fixtures,0failed/skipped.
- `node --test Docs/CommanderFinalization/GrandFix/websocket-affinity.test.mjs`:
  two actual-plugin VM cases RED2fail (four stale callbacks each), then GREEN2/2.
  This is mocked browser transport, not a physical browser or actual socket connection.

Test assembly explicitly references the existing Newtonsoft/NUnit DLLs, following
TestSupport's pattern; no package/version change. All test source compiled; no full
historical regression or PlayMode run was performed in this slice. Complete Unity
XMLs preserved before subsequent jobs. No imported Assets changed during live jobs.
No real socket/backend, paid provider, microphone, deployment or peer run occurred.

## Backend / G23 inventory and next executable work

Backend is at `D:/unity_projects/OpenEmpires/backend` (outside Unity cwd).
`relay.rs` retains every broadcast frame, clones all history on reconnect under a
session write lock; `ws.rs` maps another replay collection before sending. Recovery
assumes replay from the initial world; no verified snapshot/cursor path was found.
Reconnect currently reattaches/clears disconnection **before** history clone. Match
completion/abandonment does not call relay `remove_session`; its mapping cleanup
only considers currently connected IDs, missing soft-disconnected mappings.

Next implementation:

1. Backend accepts/validates the profile before binding player/session/sender; reply
   carries the actual expected release profile. Missing legacy profile must fail
   before queue/play, including repeated authentication. Configure expected identity
   from the final client release artifact; no arbitrary echo-as-compatibility.
2. Bound complete history by frames and measured serialized bytes at retention time.
   Once exceeded, release history and permanently mark full recovery unavailable for
   that match. Continue ordinary live broadcasts; never return a tail as complete.
3. Refuse unrecoverable/missing/already-attached reconnect before sender mutation,
   clone or serialization. Expose a safe recovery reason/policy; retain the120s
   existing disconnect expiry unless evidence justifies a change. Bound concurrent
   reconnect allocation and verify refusal does not alter live traffic.
4. Remove relay history, deadlines and all session mappings (including disconnected
   IDs) on normal completion/abandonment. Test concurrent/expired/missing cleanup.
5. Rust focused tests + local socket/server tests, then genuine matched/mismatched
   Windows/Web peers with restricted gather, production/result binding, human queues/
   takeover, delayed controls and reset. Paired simulation checks are not real peers.

Current Rust verification gate: Cargo/rustc absent from PATH and usual user paths;
VS Installer exists but its VC-toolchain query returned no installation; elevated
read-only WSL enumeration returned no distribution. No compiler was installed and
no backend source was changed in this client checkpoint. Ask for an existing toolchain
path or arrange a scoped local build toolchain; do not label Rust code compiled from
C# tests. Native remote-close/thread dispatch/send ordering and fragment handling
still need actual connection tests; epoch/source tests do not certify the whole adapter.

The existing deployed server has no profile acknowledgement. New clients deliberately
refuse it with an explicit update message until the matching backend is implemented,
verified and operator-deployed. Deployment is not authorized automatically. G12 stays
PARTIAL; G11/G23 remain open. Single-player's local launch path does not call the
multiplayer StartGame gate (source inspected, not a fresh packaged startup proof).

See `network-client-checkpoint-20261008.json` for frozen hashes and exact jobs.
