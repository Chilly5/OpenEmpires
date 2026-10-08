# Web capture and bridge contract — implementation checkpoint

Status: **PARTIAL**. Browser transport, shared online provider, compact explicit
consent/activation UI, owned browser capture/worklet and semantic adapter source
exist. Actual Web compile/link, served browser/device/quality proof is still required.
This document is not a declaration of operational Web voice.

## Transport delivered

`Assets/Plugins/WebGL/CommanderGateway.jslib` is the browser Fetch adapter for the
fixed gateway policy/speech/semantic paths. `CommanderGatewayTransport.cs` selects
this via a preserved, uniquely named Unity receiver on actual Web, and uses a
bounded UnityWebRequest DownloadHandlerScript in Editor/Windows. No Task.Run,
native Whisper DLL, filesystem, vendor service key, reusable browser key storage
or audio→semantic routing is introduced by the transport.

Browser upload is copied once from the Unity heap: speech<=2MiB, semantic<=64KiB.
Responses are read as a stream, byte-capped at65,536 with<=1,024chunks before
concatenation. Declared oversized bodies are cancelled without reading them.
Two actual browser operations max; cancelled/ignoring work retains its slot until
finished. Request timeout30sec; cancellation/teardown clears owned job state.

The only callback is `OnCommanderGatewayResponse`, metadata<=256characters with
owned GUID job/status/safe error. A still-pending matching request then pulls the
bounded response binary once. It applies strict UTF8; late/duplicate/foreign job
callbacks do not complete a different request. Responses remain UNTRUSTED data.
Gateway origin must be explicit HTTPS, or loopback HTTP for local development;
redirects/embedded credentials/query/hash and arbitrary route kinds are rejected.

NodeVM tests exercise the actual plugin functions with controlled Fetch streams.
They do not compile/link the .jslib through IL2CPP, prove a real browser, validate
the deployed CORS policy, or record physical audio. Those are separate gates.

## Required next capture / consent integration

Verified Unity6000.5.9f1 supports browser Microphone; this is not the old blanket
unsupported claim. Read-only inspection of installed Microphone.js showed permission
tracks disabled (not stopped) and late recordStart lacking a cancelled-generation
check. SDK remains untouched. Project-owned CommanderMicrophone.jslib/worklet now
owns temporary permission tracks, recording promise, stream, context, nodes and PCM.
The factory selects browser capture only on actual Web; Windows remains separate.

Enable microphone is permission only: temporary tracks stop, then a fresh Record/
hold-key action is required. Opening is distinct from Listening. Late cancelled
streams stop; permission completion never records. Mono PCM uses actual owned
AudioContext rate8–96kHz, capped60sec/4,096chunks, then proper16kWAV conversion.
Final partial chunk flushes before bounded binary pull; no base64/per-sample Unity
messages. Owned tracks/context close, never the Unity game audio context.

Blur/pagehide/visibility, key release, device ended and suspended context invalidate
or finalize the owned job. Safe failures show an actionable fixed reason, not raw
errors. Pending actual getUserMedia work blocks replacement until settled. Messages
validate type/sequence/frame caps and owned-job freshness. Controller async stop
waits for final data, races cancellation and rejects late buffers before STT.

Semantic gateway preserves the SAME Luna provider/strict decoder, using browser
transport rather than desktop HttpClient on Web. Explicit text+bounded-owned-context
choice is separate from audio consent. Web default stays unavailable until setup
and does not read desktop env/file credentials or silently switch Gemini.

Pending permission must never become recording after release/cancel/focus loss.
On blur/pagehide/device-ended/AudioContext loss, invalidate capture and require a
fresh action. Do not globally suppress DOM/browser shortcuts or close Unity's
shared audio context. No per-sample SendMessage, unbounded base64 or relabelled
sample rate. Local browser inference remains a separate bounded worker route.

`ICommanderVoiceSessionProvider` lets the existing controller freeze one provider
job BEFORE recording and cancel it on policy/auth/consent changes. This is a
privacy/session gate, not gameplay approval. The delivered online provider binds
immutable job, host/capture generation, destination/model/language,
policy consent revision and token session. The delivered gateway provider freezes
those fields and holds one global actual online-work slot across provider instances.
Compact Voice setup loads policy only; Allow online selects it but does not record,
submit or approve. Backend UUID token is masked/session-only (or reused from signed
in Matchmaking), not a reusable provider key. Client consent is enforced before
capture/upload; a server version header alone is not affirmative player consent.

All transcripts must enter existing review/edit/discard and shared text routing.
Release/Record/Send never approves a plan. No silent vendor/privacy fallback or
new authority is allowed. New/reset hosts remain minimized and invalidate old work.

## Still unverified

Actual Web target compile/link, browser Fetch/HTTPS/CORS/iframe permissions, real
mic allow/deny/late permission, sample rate/final-tail/device loss, selected ASR
accuracy/latency/memory/FPS, local worker probes, complete transcript→semantic→
separate approval→normal gameplay, and Windows packaged parity. No browser matrix
entry may be promoted from Node fixtures to physical end-to-end passed.
