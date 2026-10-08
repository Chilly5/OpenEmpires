# D1c native capture fidelity and duration checkpoint

Implemented and focused-verified, 2026-10-08. Not physical microphone, packaged
Windows, recognition-quality or final Web acceptance. Source HEAD8586764 remains
unchanged with preserved working changes. No hardware recording or paid calls.

## Changes

`UnityMicrophoneAudioCapture` now uses a typed native backend boundary. Default
backend calls the installed Unity Microphone/AudioClip APIs; tests supply scripted
device state with actual synthetic AudioClips. Only explicit Start opens hardware.
An explicitly selected missing device fails rather than choosing another device.
Native recording is non-looping, bounded to the shared60-second policy; metadata
accepts actual8–96kHz mono/stereo and caps allocation before sample extraction.
Returned clip frequency/channels, not requested frequency, define the audio.

Position is in frames; the exact recorded prefix is copied after actual native
stop. GetData failure never produces a fabricated zero waveform for STT. Invalid
position/sample values fail closed. Exactly zero signal produces retry guidance;
quiet nonzero samples are not amplified, normalized or discarded. Peak/RMS are
detached finalization diagnostics, not calibrated confidence or live mic proof.

Opening is distinct from Listening. No positive samples means opening, including
a transient false native IsRecording during startup. A five-second opening timeout
stops the owned device and requires a fresh action. Device disappearance, early
unexpected termination/reset, or stalled data becomes a safe capture failure.
Current UI refreshes readiness during recording, not only on voice-state changes.

One process-wide capture ownership slot remains claimed while native work/clip
cleanup is unfinished. A failed End does not authorize reading/freeing the live
clip or replacement capture. Cancel/dispose are repeatable; a later stopped device
can be reclaimed on dispose/new-start inspection. An unknown persistent stop/free
failure can leave native capture busy; there is no forced kill or unbounded retry.
Default capture is still non-looping. Text input and authority remain separate.

## Endpoint recovery assumption and physical gate

Some driver/end behavior can yield a zero endpoint for a completed non-looping
buffer. Recovery requires ALL: previously observed positive progress within250ms
of clip capacity, native no longer recording, and elapsed audio-start timing at
least the clip duration. A clock alone, never-started device, or early termination
does not reconstruct a full clip. Fractional requested limits are bounded before
read and need no silence padding.

This is an explicitly bounded observational recovery policy, NOT proof of every
driver's terminal behavior. Physical acceptance must check full-limit release,
device unplug/disable near the last250ms, long UI stalls and pending native startup.
If an actual driver cannot distinguish legitimate completion from lost tail, do
not claim quality acceptance: tighten/reject the ambiguous path or implement a
verified alternative capture mechanism. Do not force completion/sample flags in
the world or manufacture a physical pass from these scripted fixtures.

## Shared visible duration

Corrupt/nonfinite persisted settings normalize to the existing15-second default;
finite settings remain3–60seconds. Explicit nonfinite controller durations fail
before microphone open. Each recording freezes its actual limit independently of
later settings changes. Compact and expanded surfaces show that limit, including
3.25seconds and finer float values; progress may be approximate, the limit is not
one-decimal rounded. A bounded float promoted to double is displayed with up to
seven decimal places to avoid Mono's extra binary digits. Native sample cutoffs
have ordinary one-frame resolution. No voice operation acquires game authority.

## Evidence (complete adjacent XML retained)

| Run | Actual result | Meaning |
|---|---|---|
| Boundary84323c13 |18failed | Missing typed seam; no hardware opened |
| Behavior3f7c9a4d |1pass/17fail,0.1258047s | Real previous-rate/read/device/lifecycle defects after forwarding seam |
| First core049b5711 |18pass/3fail,0.9028383s | Core fixed; duration/settings/UI still RED |
| Duration/readinessdeb9e5fe |7failed,1.1137618s | Explicit limits/readiness cases |
| Precision/busy/startabf180d4 |1pass/4fail,2.1674251s | Fractional display, truthful busy and native startup |
| First affected63ccdf25 |95pass/1fail,13.5143116s | Mono float label precision; failure preserved |
| Final Edit60c3716758f74f05a86cb8527904e7aa |96/96pass,0fail/skip,13.2677272s | Native25/presentation29/lifetime12/async6/consent11/legacy13 |
| Final Play9fb6b63129bd4831b9b164c6cebe59ea |9/9pass,0fail/skip,3.6225978s | Two synthetic input-device cases plus seven existing mock voice routing cases |

Tests do not replace a physical signal check, model WER comparison, GPU/CPU
benchmark, actual gateway/peer run or rebuilt player. The earlier served Web probe
predates D1c/C2 repairs. No full historical EditMode/PlayMode regression was run.
Sol's narrow read-only review found the fractional precision issue; its RED/GREEN
fix is included. No branch/commit/push/deploy or credential retrieval occurred.

## API sources

Unity6.5 [Microphone.Start](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Microphone.Start.html)
defines the bounded non-loop clip and requested frequency. Unity6.5
[AudioClip.GetData](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AudioClip.GetData.html)
defines frame offsets, interleaved samples and the unsuccessful-read result.
The versioned GetPosition page was inaccessible during this checkpoint; do not
cite it as evidence for the zero-full-end recovery assumption or physical proof.

Next: provisioning/native-model Web exclusion, trusted candidate models and the
consented benchmark, remaining UI/layout/setup and B/G14/E/F repairs. Goal active.
