# OpenEmpires AI Commander — Whisper Integration

## Technical Integration of Local Whisper Engine

### 1. Package & Dependency Details
- **Package**: `com.whisper.unity` (v1.4.0)
- **Repository**: `https://github.com/Macoron/whisper.unity.git`
- **Pinned Commit**: `e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687`
- **Native Backend**: `whisper.cpp` compiled as native Windows x64 binary (`libwhisper.dll`)
- **Assembly Integration**: Added reference `"com.whisper.unity"` to `Assets/Scripts/OpenEmpires.Runtime.asmdef`.

### 2. Model Lifecycle & Locator (`WhisperModelLocator`)
The system resolves Whisper GGML model binaries across both development and packaged standalone deployments:
1. **Explicit Path**: Checked first if overridden by configuration or CLI argument.
2. **StreamingAssets**: `Assets/StreamingAssets/Whisper/{model}.bin` (in Editor) and `{DataPath}/StreamingAssets/Whisper/{model}.bin` (in Standalone player).
3. **PersistentData**: Application persistent data directory fallback.

Default model: `ggml-tiny.bin` (77,691,713 bytes).
Verification load time: ~550ms on cold start; reused across subsequent requests.

### 3. Git Hygiene & Artifact Safety
Model binaries (`*.bin`) are strictly excluded from the Git repository history via `.gitignore`:
```gitignore
*.bin
*.bin.meta
Assets/StreamingAssets/Whisper/*.bin
```
The model file is provisioned locally in `Assets/StreamingAssets/Whisper/` and bundled automatically by Unity's build pipeline into `OpenEmpires_Data/StreamingAssets/Whisper/`.

### 4. Audio Input Normalization & Native Tokens
- Whisper requires single-channel (mono) 16,000 Hz float PCM audio samples normalized in range `[-1.0f, 1.0f]`.
- Input audio captured from hardware (e.g. 48,000 Hz stereo or mono) is downmixed and linearly interpolated to 16 kHz by `CommanderAudioConverter`.
- Silent or background-noise segments frequently emit the native Whisper token `[BLANK_AUDIO]`. Both `WhisperCommanderSpeechToTextProvider` and `CommanderVoiceInputController.SafeCleanTranscript` detect this token and normalize it to `CommanderSpeechToTextResult.Empty()`, avoiding accidental Commander submissions.
