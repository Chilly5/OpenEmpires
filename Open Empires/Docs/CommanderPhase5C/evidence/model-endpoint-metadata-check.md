# Public model endpoint metadata check

Checked 2026-10-12 local date through the public, unauthenticated
[OpenRouter endpoint metadata](https://openrouter.ai/api/v1/models/openai/gpt-6-luna/endpoints).
The returned model ID is `openai/gpt-6-luna`; listed routes advertise
`response_format` and `structured_outputs`. This is a metadata snapshot, not
proof of the eventual selected route or successful strict-schema execution.
No semantic completion, credential access, paid probe, model switch or request
protocol change occurred. Phase paid attempts remain 0/10.

Do not enable new structured-output/routing behavior merely from this check.
Retain local validation and existing bounded prompt contract. Record actual
returned route/usage for authorized live requests where available; unknown
token counts remain unknown. Preserve the existing 15-second deadline.
