# Voice recognition benchmark

Status: offline scorer plus tiny native comparison implemented; no representative
quality winner, held-out WER improvement or physical microphone result. The user's
general accuracy requirement remains unverified.

## Narrow available native comparison — 2026-10-09

`native-small-comparison-before-silence-fix-20261009.json` retains both models/all
three outputs. Existing make-ten-Spearmen/build-Mill WAVs (unknown recording/upload
provenance) plus generated2s digital silence, nativeCPU/en, pinned binding/native.
Tiny speech worker times709/737ms, base.en1339/1354ms; fresh-context initialize
1848/1744ms (not cold OS cache or hardware-performance certification). Speech text
was materially the same on both; no improved-model claim or default change. Base.en
hallucinated `you` on exact zero samples; tiny was empty. Narrow finite-sample/all-zero
native guard added; no quiet-amplitude heuristic or general cloud/noise VAD claim.
Actual RED/firstGREEN/current final evidence is in finishing report. No new audio
recording/upload, provider request, gameplay, WER reference fabrication or large corpus.

Latest user amendment2026-10-08: broad40–60clip/multi-engine/performance campaigns
are cut from this implementation closeout. They remain a future verification task,
not a passed accuracy gate. No tiny→base.en filename/size change proves improvement;
the default remains tiny. Existing local prerecorded-fixture successes prove those
specific transcripts/lifetime cases only, not comparative quality or physical capture.
See finishing report for the actual single-player route, setup and remaining gates.

## Reproducible scorer

Run from the Unity root using installed Node24.13.1:

```powershell
node --test Docs/CommanderFinalization/GrandFix/speech-benchmark.test.mjs
node Docs/CommanderFinalization/GrandFix/speech-benchmark.mjs --corpus PATH --results PATH --out REPORT_PATH
```

Corpus is a JSON array (max128clips) with id, reference(max1024chars), criticalPhrases(up to16bounded phrases), kind(speech/silence/noise/music/cancel), split(held-out/tuning), source(human/synthetic/unknown). Designated safetyNegation cases also require explicit negationPhrases present in the reference. Keep audio paths/SHA/source/license/recording and upload consent in the corpus provenance manifest; do not infer those permissions from an existing WAV.

Recognition output JSON is one result per clip per engine/run (max8engines,1024records), with id, engine, raw text(max4096chars), optional warm/releaseToReviewMs and observed submitted flag. Include failures as empty/error results with separate diagnostic metadata; do not discard them or retain only best attempts. All engines must cover identical clip IDs. Different repeated attempts need distinct run identities. Input files are capped8MiB before parsing.

WER uses hand-tested token-level substitutions/deletions/insertions weighted over reference words. Case/punctuation are normalized for scoring, NOT gameplay. Critical phrases use token boundaries and limited numeral/spelled-number equivalence for scoring; four/fourteen/forty stay distinct. Missing units are not misreported as dropped negations. Designated negation loss and no-speech false transcripts/submissions are separate counters. Tuning clips never enter held-out accuracy; missing timing is unavailable, not0ms. Warm median/p95 includes reported warm observations and lists sample count. No raw transcript is emitted into the metrics report.

Scorer evidence: initial6missing-module RED, subsequent safety/annotation2RED, final8/8GREEN; retained TAPs. These use SYNTHETIC SCORING STRINGS, not synthetic audio or any ASR run. The scorer never records/uploads audio, changes recognized text, invokes a semantic provider or creates gameplay commands. Even40human labels only permit considering a quality claim; physical operational verification is always false until independent runtime evidence supports it.

## Actual benchmark still required

Current baseline ggml-tiny.bin is multilingual with English decoding;77691713bytes/SHA256BE07E048E1E599AD46341C8D2A135645097A538221678B7ACDD1B1919C6E1B21. Installed pinned binding v1.4.0 commit e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687. Existing four prerecorded WAVs lack a documented40–60-clip human/upload-consent manifest; do not remotely submit them yet. User asked for corpus/consent and separate STT configuration asynchronously.

Compare exact baseline versus compatible base.en/small.en and stronger justified local candidate; compare consented online candidate on same held-out clips; evaluate viable pinned worker-local browser runtime separately. Record each exact model/hash/backend/decoding/privacy configuration. No predetermined winner or larger-model quality claim.

Engineering targets from brief remain: meaningful held-out25% relative WER reduction,90%exact critical-slot clean-command preservation, no designated dropped negations, zero designated silence/noise/cancel submissions; short warm release-to-review p95 around3seconds on documented hardware/network. Report misses and ceiling/sample limitations unchanged.

Separate prerecorded model isolation from real Windows/browser microphone capture. Record cold/warm times, encoding/upload/queue/inference/return stages, process/worker memory, in-match FPS/frame cost and actual OS/browser/CPU/GPU. A Windows WAV browser replay is not physical browser capture proof. No accuracy/latency/memory/FPS numbers have been measured yet; neither platform is quality-verified.
