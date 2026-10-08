# Provider deployment and bounded transport

Status2026-10-08: G10 session setup and G19 desktop receive-bound repairs have focused
Editor evidence. Web/gateway source routing exists; actual deployed/served vendor
and final packaged setup proof remain open. This is not release acceptance.

## Text AI setup — G10 focused checkpoint

Use the compact **Text AI** settings control separately from **Voice**. On Windows,
enter an OpenRouter semantic key in the masked field and choose **Use key**. It is
kept only by the current in-memory provider; never copied into PlayerPrefs, files,
browser storage, logs or conversation. No remember-key option or OS-storage promise
is implemented. Apply performs local bounded character checks, clears entry fields,
and reports *configured, availability checked on your first request*. It does not
call a paid validation endpoint or claim authentication succeeded.

On Web, direct reusable key entry and its button are excluded. Use the trusted
operator's HTTPS gateway origin and an existing signed-in backend session, or a
masked exact36-character session UUID; choose **Use gateway**. Localhost HTTP is
development-only. Reject credential-bearing origins, paths, queries, fragments and
malformed/oversized sessions before transport. The endpoint remains fixed semantic
OpenRouter/Luna; reusable operator service keys remain server-side. This path does
not load speech policy, activate a microphone or consent to audio processing.

**Disable** replaces the current translator with an explicitly unconfigured route
and rejects new gameplay interpretations without a network request. It clears the
runtime override and unapplied entry fields, but does not delete external developer
configuration. Already admitted goals continue; task controls and grounded local
questions remain available. Restart/default factory selection may still use the
operator's existing developer setup. Closing settings clears unapplied secrets,
not the applied session provider. Reset/switch invalidates late interpretations and
voice-session work through the existing generation boundary; switching is not plan
approval or cancellation of already accepted work.

Actual-request status uses allow-listed transport metadata, not exception/provider
prose: authentication/key or sign-in action, credits/operator-budget action, rate/busy
wait, forbidden/model access, connection, timeout and service availability. A validated
semantic response is not proof of admitted/completed gameplay. Failed replacement
setup does not overwrite the active provider; its local error is cleared when a new
actual request starts so real auth/network outcomes remain visible. Gateway network
errors retain the connection category rather than a fabricated HTTP503. Keys and
raw upstream errors never become status text.

Owned semantic gateway transports are released on replacement/Disable/public
reinitialize with a different provider, and destruction. Cancellation-ignoring work
keeps its bounded slot until it actually settles; late results cannot admit work.
Current session sources must remain exactly36 characters on every request, not merely
at initial setup. No broad network encoding, approval or gameplay changes.

### G10 evidence

- Initial `provider-setup-red-67bd4df8.xml`:11/11 missing-action failures. Earlier
  `0f47529b` and `730f9f4d` selected zero tests before fixture import/compile repair;
  they are not passes or RED evidence. TMP inspection uses the existing test
  reflection pattern; no runtime/test assembly dependency change was needed.
- First `provider-setup-first-green-122142b4.xml`:84/84,31.5548216s, intermediate
  source only. Sol read-only review then found transport disposal and two status bugs.
- `provider-setup-disposal-red-bfcf151d.xml`:1 failed, retired transport not disposed.
- `provider-setup-review-red-6ae02812.xml`:3 failed: stale setup error hid real401,
  gateway network error mislabeled503, padded changed UUID reached transport.
- Final `provider-setup-final-edit-adedb3a5.xml`:123/123,0fail/skip,46.8411323s.
  Fixtures: setup15, gateway3, presentation32, online voice12, tactical status23,
  Phase4E1 provider24, Phase5A provider contract14. Real production UI/provider/
  admission code with fake HTTP/gateway only; no paid vendor calls.
- `provider-setup-play-902e533a.xml`:9/9,0fail/skip,3.7770314s: seven existing voice
  review/reset/typed submission scenarios and two Escape/pointer dispatch scenarios.
  Offline speech/capture fixtures, not physical microphone or hosted browser proof.

`provider-setup-checkpoint-20261008.json` ties jobs to source hashes. Complete XML is
retained before each next run. Source stays frozen during live jobs. No full historical
regression or hostile audit. Paid semantic0/6, cloudASR0/600seconds, physical mic0,
remote deployment0 remain unchanged. No previously exposed credential was inspected.

### G10 remaining gates

Fresh Windows and current served-Web settings, masked entry/focus/high-DPI layout,
actual signed-in backend identity, deployed gateway and vendor failures remain to
verify. Rust identity route still requires compilation/live proof; a mock UUID is
not real authentication. No usable public endpoint or app-funded key is bundled.
Independent full/hostile checks and final package secret/model inventories remain.

## Current code repair — G19

Shared CommanderHttpClientTransport now requests ResponseHeadersRead, rejects oversized declared lengths, and reads at most65536 application-visible decoded bytes before constructing a UTF8 string. Each8192-byte chunk is checked before writing to the bounded buffer. Chunked/missing Content-Length and error bodies follow the same cap. HttpClient handler explicitly decodes gzip/deflate; the cap applies AFTER decompression. Cancellation is checked before/during reads and before decoding; request/response/stream/buffer are disposed on all terminal paths. Strict UTF8 decoding does not replace invalid bytes with invented text.

The response-limit exception carries only safe HTTP status, not body/header/key. OpenRouter and Gemini retain known authentication/quota/server categories even for oversized error bodies; no model/schema fallback on a size failure. Existing Luna model, semantic graph limits, shape-only one-repair policy and later character/parse bounds remain. No credentials were changed or disclosed. The separate browser Fetch/gateway route now exists in source; this historical .NET repair is still NOT evidence that Web HttpClient works or that a live served gateway/vendor exchange passed.

Evidence: genuine loopback REDd31d2d1d:7cases,1pass/6fail; compressed byte body, UTF8 byte-vs-char, declared/chunked/error caps. GREEN5dbe777f:58/58 affected provider/transport cases. Additional category/cancel RED16532b43:3cases,1pass/2fail (Gemini collapsed401/429); final GREEN2dfbd54c:61/61,0failed/0skip,32.6793458s. Complete XMLs live beside this document. Synthetic injected provider keys are test-only, never real keys. Loopback is offline/local, not paid vendor/runtime evidence. Original6b9671bc harness hang has NO result; editor was restarted by user, not silently recast as RED/GREEN.

## Remaining implementation

G10 implementation is documented above; packaged/served and independent gates remain.
Browser reusable keys must not persist in storage or bundled files; any future Windows
remember-key feature must be explicit and OS-protected. Preserve external developer
provisioning without reading/printing it. Speech/setup never chooses trusted game
owner or approval.

Web semantic path must preserve configured OpenRouter/Luna via authenticated fixed gateway or verified browser-compatible transport, include bounded receive/upload/cancel/CORS and original strict decoder. Speech audio is never semantic context. Gateway auth/quota/operator provisioning and run-wide paid accounting are separate from client gameplay authority. See cross-platform architecture and execution ledger; operator deployment is not authorized automatically.
