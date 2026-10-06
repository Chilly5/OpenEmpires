# OpenEmpires AI Commander — Voice UI & User Experience

## User Interface & Control Integration

### 1. Unified Chat Interface Integration
Rather than introducing a separate or divergent voice modal window, the voice controls are integrated directly into `CommanderChatUI`:
- A dedicated voice status banner sits adjacent to the text input field.
- Visual state indicator displays current voice status:
  - `Idle`: `🎙 Hold [V] to Talk`
  - `Recording`: `🔴 Recording (V held)...`
  - `Transcribing`: `⏳ Transcribing audio...`
  - `Preview`: `Transcript: "..." [Submit] [Cancel]`
  - `Error`: `❌ {UserFacingError}`

### 2. Push-To-Talk Input Bindings
1. **Keyboard Binding**:
   - Integrated into `KeybindManager` via `CommunicationActionDefs`:
     - Action ID: `CommanderPTT`
     - Action Name: `"Commander Push-To-Talk"`
     - Default Binding: `"<Keyboard>/v"`
2. **Mouse / Screen Button**:
   - An interactive `PTT Button` is provided on the UI for touch or mouse-driven interaction (`PointerDown` starts recording, `PointerUp` ends recording).

### 3. Submission Workflow
1. **Auto-Submit Mode (`CommanderVoiceSettings.VoiceAutoSubmit = true`)**:
   - Transcribed text is placed into the input field for visual confirmation.
   - The message is submitted through `SubmitMessageAsync(transcript)`.
   - Upon completion, the input field is cleanly cleared.
2. **Preview Mode (`CommanderVoiceSettings.VoiceAutoSubmit = false`)**:
   - Transcribed text is placed into `CommanderChatUI.InputText`.
   - State enters `CommanderVoiceState.Preview`.
   - The user can review, edit, or press `Enter` / click `Submit` to confirm, or click `Cancel` / press `Escape` to discard.
