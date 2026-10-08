# Speech gateway security and deployment — D2 checkpoint

Status: **PARTIAL**, local code/fake HTTP verification only. No remote deployment,
paid vendor calls, captured microphone audio or recognition-quality claim.

Implementation: `backend/speech-gateway/README.md` is the detailed executable setup,
wire contract, operator security requirements and honest limitations. New Node
companion reuses the Rust backend's existing session identity rather than inventing
client trust. `backend/src/api/handlers.rs::session_identity` and the GET route in
`main.rs` are source-only: Rust compile/run remains unverified (cargo/rustc absent).

Ruling: keep one Node process beside the existing Rust service — Node is installed,
audio/network work stays out of simulation/relay, and vendor credentials remain
server-held. Loopback-only port8082 is deliberate; actual Rust default is8081.
Configured HTTPS proxy/exact-CORS access is required, not silently deployed.
Cost if this service choice is unsuitable: replace the isolated transport adapter;
never change Commander authority or canonical gameplay data.

## Focused evidence

`gateway-focused-green.tap`: eighteen tests; synthetic WAV/mock vendor fixtures
over local HTTP, no actual ASR. Includes auth/consent-version rejection, format,
duration/byte limits, model/URL restrictions, restart-persistent budgets and job
tombstones, ignored-cancellation deadline with retained capacity, rejected-origin
CORS, fixed multipart provider contract, bounded chunked responses, safe errors,
disabled configuration and corrupt-budget rejection. Original RED/failure records
are separately retained; see `execution-progress.md`.

These Node checks prove server contracts, NOT affirmative user opt-in. The subsequent
client implementation adds CommanderGatewaySpeechToTextProvider and compact Voice
setup: explicit Allow only after policy review, immutable job/session, no implicit
mic start/Send/plan approval, masked session-only token and signed-in session reuse.
63/63 focused Edit and7/7 mock Play prove this local integration; actual browser/mic/
service/retention/deployment remain unverified. No service key is added to clients.

## Deployment checklist / gates

- Compile/test Rust identity route and validate real session ownership; pseudonymous
  login is not a paid account. Configure exact owner enrollment, never all users.
- Provision server-only secrets, explicit policy/operator/upstream-retention text,
  verified conservative pricing and account-level spending backstop.
- One durable private ledger/process; operator-confirmed exact lock removal after
  shutdown is currently required. Never reset counters to recover or add replicas.
- Configure/test production HTTPS reverse proxy to8082 and exact game origin;
  same-origin is preferred. Current Render YAML deploys Rust only; it does NOT
  deploy this companion. No production host/proxy is claimed verified.
- Integrate bounded Unity/Web capture/job envelopes and explicit consent; show
  loading/busy/auth/quota/offline/timeout state, do not silently switch vendors.
- Verify actual Windows and served-browser capture→review→semantic→approval→gameplay,
  upstream retention/account access, representative held-out accuracy, versions,
  latency/memory/FPS and browser matrix. Mock results cannot close these gates.

The fixed `gpt-transcribe` route is a candidate rolling model alias, not a measured
winner. Official model/guide contracts rechecked2026-10-08:
[model](https://developers.openai.com/api/docs/models/gpt-transcribe),
[transcription guide](https://developers.openai.com/api/docs/guides/speech-to-text).
Configured availability, pricing minima and data-use settings remain operator gates.
