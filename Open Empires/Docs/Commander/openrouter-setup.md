# Configure a Commander OpenRouter key

Assume OpenEmpires is already on this computer. The only task here is to configure **your own** OpenRouter key for Commander. Do not send the key in an AI chat.

## Prompt for your AI assistant

> In my existing OpenEmpires project, configure Commander to use OpenRouter Luna. Find the Unity project root and verify the setting names in `Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs` and the provider selection in `GeminiAIProvider.cs`. Create or update the project-root `.env` without deleting unrelated entries. Ensure it has `OPENEMPIRES_COMMANDER_PROVIDER=openrouter` and an `OPENEMPIRES_OPENROUTER_KEY=` line. If the key is already set, preserve it without showing it; otherwise leave its value empty and tell me the exact local file path so **I can enter my key directly in my editor**. Do not ask me to paste the key into chat, and do not display existing secret values. Check that `.env` is ignored by Git. After I save the key, tell me to restart Unity and test one Commander text request. Do not commit or upload `.env`.

The resulting local `.env` should contain these two settings (with your real key replacing the placeholder **in your local editor**):

```text
OPENEMPIRES_COMMANDER_PROVIDER=openrouter
OPENEMPIRES_OPENROUTER_KEY=your_own_key_here
```

The `.env` file belongs beside the project's `Assets` and `ProjectSettings` folders. It is ignored by Git. The older `OPENEMPIRES_GEMINI_KEY` entry can remain; selecting `openrouter` makes Commander use the OpenRouter provider. Restart Unity after saving the file, then submit a normal Commander text request. The **Text AI** status reports authentication or connection problems after that request; entering a key alone does not verify access.

For a single session without editing `.env`, open Commander **Text AI**, enter the key in its masked field, and choose **Use key**.
