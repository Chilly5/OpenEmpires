# Unity Web build, hosting and browser evidence

Status: implementation checkpoint, not release or physical voice acceptance.
Observed 2026-10-08, Unity 6000.5.9f1, branch `unit_models_and_voice_control`,
HEAD `8586764e644240f93c99e2afa1c7d41c697cc879` plus preserved working changes.
See `execution-progress.md` for exact source/test/build history.

## Completed development probe and remaining build gate

Development compile/link probe `build-63b0c14d8e` targets WebGL with
`Assets/Scenes/SampleScene.unity`, strict mode and detailed report, output
`Builds/WebGrandFixProbe-20261008`. Exact job result SUCCEEDED,0errors/118warnings,
3380.3563461seconds,completed2026-10-07T23:57:23.4728639Z. Final index/loader/framework,
132,884,819-byte data and196,449,583-byte WASM exist. The capture worklet and legacy
77,691,713-byte desktop tiny model are included. Exact report and seven hashes are
in `web-probe-build-report-20261008.json` and `web-probe-artifacts-20261008.json`.
The probe is not a release build. The desktop model must be
excluded through a verified provisioning/distribution change before final Web
packaging; denying `.bin` in a server is NOT build exclusion.

Actual served in-app-browser menu and SinglePlayer1v1 load succeeded on this probe.
Commander started compact; the Escape double-dispatch failure was then reproduced.
See `web-probe-load-and-input-findings-20261008.md`. This build predates the subsequent
C2 source repairs and does not verify them. Fresh stabilized source-matched Windows
and Web builds remain required. Build118warnings still need safe classification.

## Reproducible development host

From the Unity root, using installed Node 24.13.1:

```powershell
& 'C:\Program Files\nodejs\node.exe' --test --test-reporter=tap 'Docs/CommanderFinalization/GrandFix/web-host.test.mjs'
& 'C:\Program Files\nodejs\node.exe' 'Docs/CommanderFinalization/GrandFix/web-host.mjs' 'Builds/WebGrandFixProbe-20261008' --port 8088
```

Only run the second command against a completed build. Open
`http://127.0.0.1:8088/`; do not use `file://`, an insecure remote origin, or
browser security-disabling flags. The CLI binds only loopback, checks Host, logs
no request headers/bodies and does not record audio, launch a browser, proxy a
gateway, or deploy TLS. Stop the owned host after its scoped scenario completes.

The host serves a fixed root with GET/HEAD only, rejects traversal/dotfiles,
secret-like paths, unknown extensions and symlink/junction paths. It streams
non-HTML files with a 1 GiB per-file cap and limits HTML to 1 MiB. Serve a dedicated
immutable build directory, never the repository, private cache or backend root.
This is a local test host, not a general hardened Internet service.

| Artifact | Response |
|---|---|
| `.wasm` | `application/wasm` |
| `.js`, worklet/worker | `application/javascript; charset=utf-8` |
| `.data`, `.bundle` | `application/octet-stream` |
| `.json` | `application/json` |
| `.wav` | `audio/wav` |
| `.gz` / `.br` suffix | Underlying MIME with matching gzip/Brotli Content-Encoding |
| `.unityweb` | Loader-managed octet stream, no automatic Content-Encoding |
| Native desktop `.bin` | Denied; not a browser-model distribution route |

HTML scripts receive a fresh response nonce. CSP allows same-origin scripts,
that nonce, WASM compilation and blob workers required by the proposed Unity
path; ordinary JavaScript `unsafe-eval` and `unsafe-inline` are not enabled.
Inline styles are allowed for the existing template, not inline script authority.
Default connect destinations are same-origin. `--gateway` adds only an explicitly
configured HTTPS origin (loopback HTTP allowed for local tests); it does not grant
authentication or CORS. `nosniff`, no-referrer and no-store apply. Compatibility
with the actual generated loader still requires a served-artifact test.

Default Permissions-Policy is `microphone=(self), camera=()`. No microphone is
opened at startup. Permission activation, a fresh deliberate recording action,
online audio consent, transcript Send, and gameplay approval remain separate.
The primary online route requires neither WebGPU nor cross-origin isolation.
`--isolation` adds COOP same-origin and COEP require-corp only for a selected,
verified route that needs them; that route's asset/embed compatibility is untested.

## Production/operator checklist (not deployed)

1. Deploy the exact verified build behind HTTPS with certificates and a fixed
   public origin. Use a dedicated immutable artifact root and an appropriate
   production server. Do not expose this loopback development CLI publicly.
2. Preserve correct decoded MIME and Content-Encoding for the actual Unity
   compression setting; do not double-decompress loader-managed `.unityweb`.
   Test the emitted loader, WASM, data and same-origin PCM worklet together.
3. Prefer same-origin authenticated HTTPS gateway routes. Otherwise configure
   exact gateway origin in CSP and exact allowed game origin in gateway CORS,
   including required authorization/policy headers. CORS is not authentication.
   Never inject reusable service keys in client assets, URLs or browser storage.
4. Maintain a response-specific nonce for every emitted inline bootstrap script.
   Do not disable CSP or add ordinary `unsafe-eval` to manufacture a passing load.
   Inspect violations without dumping authentication or provider payloads.
5. For an embed, explicitly permit the actual parent with `--iframe-origin` in
   local experiments or equivalent production frame-ancestors. This changes only
   embedding permission, not microphone delegation. The HTTPS parent must
   delegate microphone to the exact child origin in its Permissions-Policy and
   iframe `allow` attribute; the child must permit its own capture. Test the real
   top-level/iframe deployment and its focus/fullscreen/teardown behavior.
6. Add COOP/COEP only if the measured selected local threaded backend requires
   them. Verify all dependencies meet CORP/CORS and embedding constraints. Do not
   make the primary online voice route depend on isolation or experimental flags.
7. Verify no native desktop model/DLL is in the final Web archive, retain notices,
   exact hashes and build/source identities. Independently inspect distributions
   with values-free secret checks. This does not close historic credential expiry.
8. Run the capture-to-review-to-semantic-to-separate-approval-to-normal-gameplay
   scenarios with explicit recording consent. Collect per-browser version,
   sample rate, critical-word fidelity, latency, memory and FPS. A build/load pass
   or fake media callback is not physical microphone or recognition acceptance.

## Browser compatibility matrix

No actual browser version or OS was measured in this checkpoint. Do not infer a
browser pass from Node VM tests or Editor PlayMode.

| Target | Version/OS | Build/load | Physical end-to-end | Current evidence |
|---|---|---|---|---|
| Codex in-app browser | Exact embedded version/OS unavailable | Passed development probe/menu/local match | Not verified | Actual UI screenshot observations; Escape leak reproduced |
| Chrome desktop / Windows | Not measured | Not verified | Not verified | Await completed probe |
| Edge desktop / Windows | Not measured | Not verified | Not verified | Await completed probe |
| Firefox desktop / Windows | Not measured | Not verified | Not verified | Await completed probe |
| Safari desktop / macOS where base game supports it | Not measured | Not verified | Not verified | Hardware/base-game gate |
| Mobile Chrome/Safari | Not measured | Not verified | Not verified | Declared base-game mobile scope must first be established |
| Fullscreen / iframe | Not measured | Not verified | Not verified | Exact promised hosting mode must be exercised |

Intended primary speech route: owned browser PCM capture at actual AudioContext
rate, bounded conversion to PCM16 mono 16 kHz WAV, explicit consent and authenticated
gateway transcription, shared reviewed text and existing Commander authority.
No online provider call, physical mic recording, measured quality winner or browser
FPS result is claimed. Browser-local recognition/provisioning remains unfinished.

## Focused evidence

`web-host-red.txt` retains the missing implementation RED; `web-host-first-failure.txt`
retains the initial 4/5 result (an overbroad test assertion incorrectly forbade CSS
inline styles). The assertion now checks the script-src clause without weakening
the script policy. `web-host-green-20261008.tap` records 5/5 passed, 0 failed/skipped,
3380.7777 ms, real local HTTP with synthetic static assets. It verifies fresh nonce,
compression/MIME, private/traversal rejection, junction/Host rejection and explicit
isolation/connect configuration. It is not generated Unity/Web/microphone evidence.
