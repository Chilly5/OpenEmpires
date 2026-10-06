# OpenEmpires AI Commander — Phase 4H Known Limitations

## Known Limitations & Out-of-Scope Items

### 1. Architectural Scope Boundaries
The following capabilities were explicitly declared **out of scope** for Phase 4H and are intentionally omitted:
1. **Always-Listening / Voice Activity Detection (VAD)**:
   - Phase 4H relies strictly on explicit Push-To-Talk (hold `V` or click UI button). Constant microphone streaming and automated speech boundary detection are not implemented.
2. **Wake-Word Activation**:
   - There is no wake-word engine (e.g., "Hey Commander"). PTT guarantees zero background audio processing or accidental match interference.
3. **Partial / Streaming Transcription**:
   - Audio is transcribed in batch upon button release. Real-time partial token streaming is out of scope.
4. **Cloud Speech APIs**:
   - Phase 4H is exclusively local via `whisper.unity` and `whisper.cpp` (`ggml-tiny.bin`). No audio data is ever transmitted over the network.
5. **Multi-Language Speech Models**:
   - The default model provisioned is English (`ggml-tiny.bin` with `en` language enforcement). While the provider supports other GGML models, non-English recognition quality is dependent on larger models (`base`, `small`, `medium`) which exceed default deployment size constraints.

### 2. Platform & Hardware Constraints
1. **Microphone Device Availability**:
   - If no physical microphone device is detected by Unity (`Microphone.devices.Length == 0`), `UnityMicrophoneAudioCapture` fails gracefully and returns `NO_MICROPHONE_DEVICE` without throwing exceptions or blocking typed input.
2. **Sample Rate Conversion Overhead**:
   - Hardware recording at native 48 kHz (such as Realtek audio arrays) requires linear interpolation resampling in memory. For a 15-second recording, this consumes <2ms CPU time, which is completely negligible.
