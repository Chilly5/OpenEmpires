# Narrow Commander companion gateway

Implementation checkpoint, not a deployed service or physically verified Windows/Web voice experience.
Node 22+ (tested with 24.13.1); no npm dependencies. From this directory:

```powershell
node --test
node server.mjs
```

The second command is disabled by default: no listener, microphone or vendor call.
The secret-free `.env.example` names configuration; the server does not load it.
An operator must inject named configuration in this server process only, after
approving hosting and provider access. Do not place keys in Unity or Web assets.
Keep the ledger on persistent private storage, outside build artifacts.

## Single-player setup — no Rust or multiplayer login

Use `COMMANDER_AUTH_MODE=standalone` and
`COMMANDER_STANDALONE_SESSIONS_PATH=./private-state/service/sessions.json`.
Choose an operator quota-owner UUID (not a Unity player ID), then deliberately run:

```powershell
node standalone-auth.mjs ./private-state/service YOUR-QUOTA-OWNER-UUID
```

Use a fresh directory. This creates a one-hour service capability in
`session-token.txt` and only its SHA256/owner/expiry in `sessions.json`. No token is
printed and no HTTP/vendor call occurs. Files are exclusive: existing records are
not overwritten. On Windows, new directory ACLs are restricted and existing public
directories are refused; authentication checks registry ACL/owner too. Run issuer
and service as the same user. On POSIX, private mode bits are required. Keep these
files outside served/build assets, backups exposed to other users, and Git.

Add the quota owner to `COMMANDER_GATEWAY_OWNER_IDS`; retain the SAME persistent
budget ledger across session replacement/restarts. Inject the remaining keys,
origins, policy and verified spending estimates named in `.env.example` into the
service process only. That file is a template, not an auto-loaded secret store.
Do not put API keys into the game/build, command-line arguments or chat.

`node server.mjs` validates standalone registry/ACL/expiry before opening a listener
or process lock. Missing/corrupt/expired records fail startup. Every actual request
checks the current registry again, so removal revokes subsequent requests. Revocation
does not undo already uploaded/billable work. At most128 records/32KiB, maximum24h
expiry; the supplied issuer defaults to1h. There is no public enrollment endpoint.

Enter the privately obtained session UUID in the game's masked **Service session**
field and use the operator's gateway origin. It authenticates the service/quota,
not a multiplayer match or game ownership. Text translation and microphone/audio
consent remain separate. No Rust, matchmaking or relay is needed in this mode.
If a session expires, the operator must provision a fresh private session and update
the registry configuration without resetting the budget. Do not delete a spending
ledger to re-enable paid calls.

## Legacy backend mode — deferred multiplayer path

Only when deliberately selecting `COMMANDER_AUTH_MODE=backend`,
Rust defaults to port8081; this companion uses8082 and authenticates through
`GET http://127.0.0.1:8081/api/auth/session`. Change authBase to the actual backend
origin if its port differs. No arbitrary URL/model is accepted from a client.
The companion intentionally binds loopback: a separately configured HTTPS reverse
proxy must forward `/api/commander/*` to8082. Forward other existing backend APIs
to8081. Prefer a same-origin served Web game; otherwise allow exactly its HTTPS
origin. No proxy/deployment has been performed or validated by this checkpoint.

In legacy mode, the backend login is pseudonymous and in memory, NOT a durable paid account.
Enroll exact authenticated player UUIDs in the operator allowlist. A newly minted
session/player is not automatically entitled to funded calls. Origin is not auth.
Do not deploy this as an unrestricted public service. Durable account enrollment
and broader public abuse controls are separate deployment requirements.

## Wire contract

- `GET /api/commander/voice-policy`: public bounded processing disclosure.
- `POST /api/commander/stt`: Bearer operator-issued service session (or explicit legacy backend session); `audio/wav` binary
  PCM16 mono16k; UUID `X-Voice-Job`, exact `X-Voice-Consent` policy version and
  allowlisted `X-Voice-Language`. Returns only jobId/policyVersion/text.
- `POST /api/commander/semantic`: Bearer session; UUID `X-Commander-Job`, bounded
  JSON messages/max_tokens/reasoning and fixed `openai/gpt-6-luna` model. Audio
  must never enter this route. Existing game-owned semantic validation remains
  mandatory; an HTTP success is not gameplay authorization.

The consent header is a transport assertion, NOT proof of affirmative user choice.
The client must show disclosure and obtain explicit upload consent before capture
upload. The current Unity provider and compact Voice setup now implement that gate
(63 focused Edit checks/7 mock Play), but actual Web capture/service/physical proofs
are still pending. These counts are not deployed-gateway or ASR-quality evidence.

Upload2MiB, decoded clip0.1–60sec; receive/response65,536bytes (auth4,096),
transcript4,096characters, two active requests globally and one per owner,
deadline30sec, max64connections. A cancellation-ignoring adapter keeps its slot
until the actual promise settles; it may stay busy, but cannot spawn replacements.
Vendor cancellation does not guarantee an uploaded/billable job is reversed.

Short-lived job tombstones are saved before upstream attempts and survive restart:
256entries,120secTTL, bound to authenticated owner + route + recording UUID.
No cached transcript is returned; duplicate requests receive409. Beyond the TTL
there is no indefinite at-most-once guarantee. Never auto-retry a recording.

## Spending and retention

Durable reservations count audio seconds rounded up and each semantic HTTP call,
including eligible repairs. Maximum configured limits are600sec and6semantic
requests for this implementation run; they are lifetime counters, not daily resets.
An uncertain attempt is not refunded. No default audio/transcript retention; only
owner counters and bounded job identities/timestamps persist. Upstream retention
must be explicitly disclosed and verified by the operator, not assumed zero.

The dollar cap is based on REQUIRED operator-verified conservative upper cost
estimates, including minimums and maximum bounded tokens; it is not an audited
invoice reconciliation. Do not enable until rates/margins are verified. Enforce
provider-account spending limits as an additional backstop. No actual paid call
has been made by this checkpoint.

Use exactly one process/replica and one persistent ledger for the budget. An
exclusive process lock prevents a second server using that path. For now BOTH
normal shutdown and crashes leave `<ledger>.lock` intact. Before restarting,
verify that its recorded process is gone, then remove ONLY that exact lock.
Never delete/reset the budget file, silently use another path, or auto-break locks.
This conservative manual recovery requirement is intentional and remains a UX
limitation; no claim of horizontally scalable quotas is made.

## Remaining verification gates

Single-player mode no longer requires Rust. Legacy Rust integration is user-deferred.
Actual configured standalone session/provider/proxy setup,
actual consent/job/capture/provider runtime proof, real HTTPS proxy/CORS, served Web build,
Windows + browser microphone, vendor access/retention/quality/billing, operator
enrollment and independent hostile checks. Node loopback tests are not these proofs.
