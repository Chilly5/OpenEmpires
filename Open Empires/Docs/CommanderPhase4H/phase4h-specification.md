# OpenEmpires AI Commander — Phase 4H Specification
## Local Voice / Speech-to-Text Input Specification

### 1. Purpose & Scope
Phase 4H delivers push-to-talk local speech-to-text input for the OpenEmpires AI Commander.
The core architectural invariant is that voice is strictly an alternate input modality to produce plain text strings:
```text
MICROPHONE
    ↓
audio capture (UnityMicrophoneAudioCapture)
    ↓
audio conversion (CommanderAudioConverter: mono, 16 kHz)
    ↓
local STT (WhisperCommanderSpeechToTextProvider via whisper.unity)
    ↓
plain text string
    ↓
EXISTING Commander text-input submission path (CommanderChatUI.SubmitMessageAsync)
    ↓
Phase 4E / 4F / 4G Bounded Semantic Graph Pipeline
```

### 2. Authority Invariant
The voice layer:
- Does NOT understand gameplay.
- Does NOT interact with `GameSimulation`.
- Does NOT create `ICommand` or goal instances.
- Does NOT modify `CommandBuffer`.
- Does NOT bypass deterministic game validation.
- Does NOT call the semantic LLM directly.

Typing `build a mill near the berries` and speaking `build a mill near the berries` converge into the exact same entrypoint:
`CommanderChatUI.SubmitMessageAsync(string message)`.

### 3. Key Architectural Requirements
1. **Push-To-Talk Default**: Recording starts when the PTT key/button is pressed and held, stops when released, and transcribes.
2. **Deterministic Resampling**: Microphones recording at non-16 kHz rates (such as native 48 kHz hardware) are deterministically downmixed to mono and resampled to 16,000 Hz using linear interpolation before inference.
3. **Detached Representations**: `CommanderAudioData` and `CommanderSpeechToTextResult` are pure data containers without engine or simulation references.
4. **Fail-Closed Reset Safety**: Any match or conversation reset increments a session counter, discarding in-flight transcriptions upon completion to prevent stale gameplay mutations.
5. **Preview & Auto-Submit Modes**:
   - Auto-Submit: Valid transcript immediately passes into `SubmitMessageAsync`.
   - Preview: Valid transcript populates the input field and displays in `CommanderVoiceState.Preview` for manual confirmation or editing.
6. **Graceful Error Isolation**: STT failure, missing model, or blank audio returns to `CommanderVoiceState.Error` or `Idle` without degrading keyboard Commander functionality.
