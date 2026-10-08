# D3a trusted models and native payload packaging

Checkpoint2026-10-08, not completed recognition/provisioning release. HEAD8586764
unchanged, working source preserved. No microphone recording, cloud audio or paid
semantic requests occurred. Default remains multilingual Tiny with English decoding;
no candidate is a measured winner.

## One trusted source

`Assets/Resources/CommanderVoice/whisper-models.json` pins the public
`ggerganov/whisper.cpp` model repository revision
`5359861c739e955e79d9a303bcbc70fb988958b1`. Exact sizes/SHA-256 came from its Git LFS
metadata, not checksums calculated from an arbitrary download and accepted as their
own expectation. Public metadata retrieval required network escalation; no credential
or audio was sent. The Web search fetcher could not read these pages; approved
bounded CLI retrieval did. Provenance source:
[repository metadata](https://huggingface.co/api/models/ggerganov/whisper.cpp?blobs=true).

| Model | Bytes | Current state |
|---|---:|---|
| ggml-tiny.bin |77691713|Existing baseline matches upstream pin; imported and verified |
| ggml-base.en.bin |147964211|Downloaded at pinned revision and verified; compatibility/quality comparison pending |
| ggml-small.en.bin |487614201|Two terminal download failures; not installed/promoted |
| ggml-medium.en.bin |1533774781|Pinned candidate only; not downloaded, corpus/hardware justification required |

Exact hashes are in the bundled manifest. `.en` candidates are English-only;
the native provider rejects another decoding language rather than silently changing
the language. Pinning/acquisition does not prove ABI compatibility, speed or accuracy.

## Developer acquisition/import helper

Node24 developer tooling, from Unity root:

```powershell
node Docs/CommanderFinalization/GrandFix/model-provision.mjs ggml-base.en.bin
node Docs/CommanderFinalization/GrandFix/model-provision.mjs ggml-tiny.bin --import PATH_TO_GGML_TINY_BIN
```

Default destination `LocalModels/Whisper/<expected-sha256>/<canonical-filename>`
is ignored and outside Assets. `--root` may select an explicit model cache, but
the helper is NOT yet the end-user provisioning UI. Do not claim Windows players
must install Node as the supported final solution; runtime acquisition/import/setup
is still required and remains in the roadmap.

CLI model choices come only from the bundled manifest. No arbitrary user URL,
rolling revision or accepted self-generated checksum. Import preserves its source.
Downloads/imports stream exact size/hash and reverify before same-filesystem
exclusive promotion. Existing valid/corrupt destinations are never overwritten.
Corrupt installed files report an error; do not silently accept, delete or replace
them. Disk reserve64MiB, per-model1.6GB and cache3GiB ceilings apply. The cache
inventory is bounded; path/junction escapes are rejected.

One `.provision.lock` admits one operation, no queue. Caught failures/cancellation
clean only the operation's own partial file/lock. After a process crash an old lock
may require operator inspection/removal; do not assume stale PID or steal it.
Downloads are safely restartable from zero, not yet HTTP-range resumable. There
is no silent retry; small.en's first generic transport failure and later
MODEL_NETWORK_FAILED remain failed attempts. Network/error output exposes only
safe categories, not redirect/signed URLs, headers or raw provider diagnostics.

## Runtime lookup and trust gate

`WhisperModelCatalog` is a detached read-only projection of that same bounded
bundled JSON. Native lookup only accepts catalogued canonical filenames. Explicit
missing paths fail, never downgrade to another copy. Windows player cache root is
`Application.persistentDataPath/CommanderVoice/Models`; editor developer cache is
outside Assets under LocalModels. Neither requires installation StreamingAssets
to be writable. Unknown model names and traversal cannot grant an explicit load.

Cheap path resolution runs on the Unity caller; size/SHA verification runs in the
existing bounded native worker before native creation. `WhisperModelVerifier`
holds a read FileStream lease across checksum and native loading, denying ordinary
Windows mutation/deletion during that interval. Missing/corrupt/unverified model
produces safe unavailable guidance without gameplay or online fallback. The
trusted native model parser is never invoked on a known bad-size/hash model.

## Source relocation and build gate

The original `Assets/StreamingAssets/Whisper/ggml-tiny.bin` and its155-byte metadata
were moved, not deleted, to `LocalModels/LegacyStreamingAssets/`. Both original and
new verified copy hashes were checked before moving, exact source/destination paths
were validated within the workspace, and an existing archive would stop the move.
Default editor loading remains usable through the verified cache.

`WhisperDistributionPolicy` and Editor `CommanderVoiceBuildGuard` reject native
catalogued models/Whisper .bin/libwhisper DLL payloads from shared StreamingAssets
before Web build. The worklet stays there. The actual current source scan passes.
The older `build-63b0c14d8e` artifact still contains Tiny as historical evidence;
it was NOT altered or presented as the new package. A fresh final Web build and
archive inventory are still required. Native importers/notices and optional browser
model export/cache/distribution remain separate gates.

## Focused evidence and honest failure history

- Node acquisition RED9missingmodule → GREEN9; network-category RED9pass/1fail →
  final10/10passed,199.0025ms. Complete retained TAPs, controlled synthetic payloads.
- Model trust REDeaca0ca3:8failed,0.7380587s → firstGREENf4d46f0e21/21,
  0.2524161s (8trust+13legacy). Web gate2RED retained separately.
- First affected24f6479331/32: native make-Spearmen fixture initialization failed.
  Original XML retained; its exact historical cause is NOT established. Source
  intentionally defers native free; fixture teardown now awaits its owned release
  and reports safe initialization category. Do not rewrite the original as passed.
- Final affected6d542902404b48cca03086597b00b44b32/32passed,0fail/skip,
  6.0312543s: trust/distribution10,legacy13,ownership6,native prerecorded/silence3.
  Those three native cases prove loading/representative decoder behavior after
  relocation, not40–60-clip quality, real capture, GPU or cross-platform operation.
- Luna's narrow read-only source review reported no critical/high finding; it ran
  no tests and does not certify the whole release. No full regression/build run.

Next: user-facing runtime acquisition/import/cleanup/selection with no default
quality claim; small.en network acquisition; candidate compatibility and meaningful
consented corpus/benchmarks; notices and final same-source Windows/Web artifacts.
All remaining GrandFix B/C/E/F work remains active.

Subsequent D3b delivers runtime player import/download/Useverified UI with77Edit/
9Play focused evidence, documented in `player-model-setup-and-acquisition.md`.
That supersedes the developer-only implementation gate above, not packaged/network/
recognition/layout/physical acceptance. Developer Node lock policy and player OS
lease policy are distinct; player crash recovery no longer waits on a stale marker.
