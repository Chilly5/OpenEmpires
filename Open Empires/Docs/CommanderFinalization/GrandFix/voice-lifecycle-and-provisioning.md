# Voice lifecycle and provisioning status

2026-10-07: generic controller lifetime repair focused-verified; native binding/provisioning/cross-platform quality implementation still OPEN. Not a release verdict.

## Controller / host lifetime — delivered D1a

The controller stores the actual raw inference task separately from UI completion. It races that task against cancellation/deadline, returns promptly when the provider ignores the token, observes late faults, and leaves its late success inert. Cancellation/reset increments the voice session; provider success with an already-cancelled token cannot preview or submit. Busy unfinished inference rejects a replacement recording instead of queuing more work.

Dispose cancels before marking disposed, releases capture once, and defers provider cleanup until the actual raw inference ends. Cleanup resumes asynchronously on the host synchronization context for adapters requiring Unity/JavaScript affinity. Public ChatUI.Initialize invalidates voice before adopting a new simulation/manager/provider runtime. Freshness is checked both before invoking the provider and after synchronous success-state notifications before preview/auto-submit, so reentrant reset/dispose cannot forward old text.

The operation finally owns its CTS; Cancel does not prematurely dispose another active operation's cancellation source. Current source changes voice lifetime only, never consent or gameplay authorization. Existing default review remains; auto-submit still sends plain text through ordinary game-owned validation and never approves a plan.

## Duration — partially repaired G18

A shared60-second hard maximum now applies to settings, controller adapter requests, native capture clamp and default PCM converter. Converter frame bounds are applied before downmix allocation; an explicitly shorter utility conversion limit remains intentional. The prior default15-second conversion of an accepted30–60-second recording is removed; a30-second last-sample regression proves the tail is retained. This is not recognition accuracy proof.

Native/browser capture endpoint timing, exact maximum recording/full-buffer stop behavior, actual device rates/channels/meters, asynchronous Web post-stop sample readiness, invalid-media/input boundaries and user-visible limits still require implementation/runtime proof. Do not label the entire G18 or hardware capture contract closed from one numeric PCM test.

## Exact focused evidence

- RED48430b07:7cases,0pass/7fail,8.0003091s; ignored cancellation/deadline, premature dispose/token, replacement work, tail loss, public reinit preview and stale auto-submit reproduced.
- First affected GREEN01133a9c:31/31,23.7741068s.
- Transcribing-notification cancellation RED13e39c0f:2fail; guard fix GREEN0ba8efe1:33/33,24.2947181s.
- Fresh read-only review found Idle/Preview callback-reset delivery gap: REDa4b0ba04:2fail; fixed post-state-notification affinity checks.
- Capture boundary REDd9b88383:120s forwarded to injected adapter rather than60s; fixed shared controller cap.
- Final affected Editb5d83de8:36/36,0failed/0skip,24.5918051s;12new lifetime+13existing voice+11presentation cases.
- Focused Playc3beafa6:7/7,0failed/0skip,1.8837278s; mock capture/provider through normal Commander routing/edit/reset/error/QA. Not physical microphone/native inference/package/browser certification.

Complete XMLs are retained beside this document; failed snapshots remain failures and overlapping runs are not summed into a full suite.

## Native binding D1b — implemented ownership adapter, limited evidence

Pinned whisper.unity v1.4.0/e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687 has blocking wrapper GetText, private native context, finalizer-only free and no exposed managed abort. Native params have private pointer abort slots but no supported managed accessor. Actual source also allocates callbackGCHandle then returns before Free on native failure; exception paths are not protected by finally. This is a concrete source issue, not a claim that a user's clip triggered it.

Do not free/reflection-poke context or edit Library/PackageCache. A tracked pinned managed fork/embedding is the lowest-ABI-risk repair: callback handle finally, rooted ABI-verified abort callback if exposed, non-blocking dispose request, worker-owned deferred native free after actual return, and bounded model replacement/inference. Maintain native binaries/importers/provenance and required licenses.17RuntimeCS≈106KB; Windows native files≈22.6MB, all platforms≈80.4MB. Preserve release scope Windows+Web; package has no Web native plugin, so Web must use a separate actual supported capture/ASR adapter rather than load the DLL.

That paragraph records the inventory recommendation. Subsequent main verification found the existing public native ABI AND full public parameter structs reusable, so the implemented smaller adapter avoids copying ABI layouts or embedding80MB. Application code no longer calls opaque WhisperWrapper.GetText. CommanderWhisperContext explicitly owns the native pointer, uses short locks, serializes load/inference/release to one global context slot and rejects replacement until native free finishes. Dispose schedules even idle model/GPU cleanup off UI; an active call frees only after it returns. Unknown native free failure retains/quarantines the slot rather than allocating more potentially leaked models. Native parameters stay rooted through the full call with GC.KeepAlive. No managed native callbacks or GCHandles are allocated, so the upstream early-return handle leak is absent from this Commander path. Original package/native binaries unchanged; unsafe enabled for the isolated public native API adapter in existing runtime assembly.

No supported abort is exposed by the pinned managed surface; no private slot/layout hack was added. UI cancellation/deadline remains prompt but actual native inference may continue. If it never returns, local voice remains honestly busy; text input remains usable. Do not claim bounded native execution time, immediate abort or unmeasured GPU latency. Native API code is excluded from Web runtime compilation and the native provider never loads files/DLL there, but the browser route is NOT yet implemented; temporary unavailable native branch is not Web completion.

Evidence: ownership RED19943f91(4fail)→GREEN0966c70d(4/4); stale-init-status RED20cacd41 fixed; actual pinned ABI/tiny recorded/silence+ownership GREEN2ada943f(8/8,22.0455484s); unknown free failure RED46a12a9b fixedfailclosed; final affected Edit676a7b76(45/45,45.9858122s) and focused voice Play90fec1c5(7/7,1.8553407s). Three native fixtures prove representative ABI/decoder parity, not microphone, forty-clip recognition comparison, packaged build, GPU or browser support. See voice-model-and-backend-manifest.json for exact installed binary hashes/model/config identities.

Native/provider ownership and background gameplay-load repair now have the evidence above. Remaining: broader release boundary/inference/cancellation/cleanup matrices, benchmark/model selection, provisioning, trusted acquisition hashes/notices/cache/build gates, online consent/authenticated gateway, actual Windows/Web capture-to-gameplay and hardware/performance proof. Safe lifetime is not speech-quality acceptance.

## Provisioning / operator gates

D1c native capture now has actual clip rate/channels, bounded readiness/endpoints,
explicit device selection, checked reads, one actual ownership slot and frozen
visible duration. Final96Edit/9Play focused evidence and physical recovery caveats
are in `native-capture-fidelity-and-duration.md`; synthetic clips are not physical
recognition proof. Subsequent D2 browser capture/gateway source and a real served
Web development probe supersede D1b's historical "browser route not implemented"
checkpoint, but quality/physical/final-build gates remain incomplete.

Current baseline remains multilingual ggml-tiny.bin, English decoding,77691713bytes/SHA256BE07E048E1E599AD46341C8D2A135645097A538221678B7ACDD1B1919C6E1B21. No stronger engine has been selected or quality-verified. Source/developer model existence is not pinned acquisition or public package readiness. See benchmark and cross-platform architecture documents. Missing consented human corpus/online STT configuration was asked asynchronously; no audio was recorded/uploaded and no paid semantic/STT call was made.
