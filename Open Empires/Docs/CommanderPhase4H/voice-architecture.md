# OpenEmpires AI Commander — Voice Architecture

## Architectural Hierarchy & Separation of Concerns

### 1. Component Diagram

```text
[ Hardware Microphone ]
         │ (PCM audio)
         ▼
[ UnityMicrophoneAudioCapture : ICommanderAudioCapture ]
         │ (CommanderAudioData raw)
         ▼
[ CommanderAudioConverter ] (mono downmix, 16 kHz resample, 15s clamp)
         │ (CommanderAudioData normalized 16kHz)
         ▼
[ WhisperCommanderSpeechToTextProvider : ICommanderSpeechToTextProvider ]
         │ Task.Run worker thread (whisper.unity / whisper.cpp)
         ▼
[ CommanderSpeechToTextResult ]
         │ (Accepted, Empty, Cancelled, or Failure)
         ▼
[ CommanderVoiceInputController ]
   ├── Concurrency Lock (single in-flight operation)
   ├── Session ID Token (late-transcript discard after reset)
   └── UX State Machine (Idle, Recording, Transcribing, Preview, Error)
         │
         ▼
[ CommanderChatUI.SubmitVoiceTranscriptAsync ]
         │
         ▼
[ CommanderChatUI.SubmitMessageAsync ] <─── [ Keyboard / Chat Input ]
         │
         ▼ (Single unified entrypoint)
[ Bounded Semantic AI Pipeline (Phase 4E/F/G) ]
```

### 2. State Machine Transitions

| Current State | Event | Next State | Notes |
|---|---|---|---|
| `Idle` | `StartRecording()` | `Recording` | Initializes audio capture buffer |
| `Recording` | `StopRecordingAndTranscribeAsync()` | `Transcribing` | Halts capture, passes buffer to STT |
| `Recording` | `CancelRecording()` | `Idle` | Discards capture buffer |
| `Transcribing` | `Accepted` (AutoSubmit = true) | `Idle` | Invokes `SubmitMessageAsync` |
| `Transcribing` | `Accepted` (AutoSubmit = false) | `Preview` | Populates UI text field |
| `Transcribing` | `Empty` or `[BLANK_AUDIO]` | `Idle` | Silent return, no submission |
| `Transcribing` | `Failure` | `Error` | User-facing message, returns to Idle after timeout |
| `Preview` | `ConfirmPreviewAndSubmitAsync()` | `Idle` | Submits previewed text |
| `Preview` | `CancelPreview()` | `Idle` | Clears previewed text |
| *Any* | `ResetSession()` | `Idle` | Bumps session ID, invalidates in-flight tasks |

### 3. Fail-Closed Reset Safety
When the game simulation or chat UI resets (e.g., match restart, menu exit, or `clear memory`), `CommanderVoiceInputController.ResetSession()` increments `currentSessionId`. When an asynchronous transcription finishes, its captured `sessionId` is compared against `currentSessionId`. If mismatched, the transcript is discarded immediately with zero side-effects.
