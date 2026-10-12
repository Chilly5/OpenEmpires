# Provider metadata check (credential-free)

2026-10-10 public GET:
https://openrouter.ai/api/v1/models/openai/gpt-6-luna/endpoints

Model metadata returned the existing Luna ID and advertised response_format and
structured_outputs on listed endpoints. Architecture tokenizer was only "GPT",
not a verified encoding for exact local counting. This is public metadata, not
proof of the actual routed provider for a paid completion. No paid setup probe,
credential inspection, model/routing change or structured-output enablement.
The current gateway allow-list lacks response_format; no uncounted schema is added.

Usage metrics can be read from the completion response without another request:
[official usage documentation](https://openrouter.ai/docs/cookbook/administration/usage-accounting).
Missing usage remains unknown. Cached prompt tokens count toward input. Preflight
UTF8-unit estimates remain distinct from actual reported tokens; live Windows/Web
calibration and <4000 actual-count proof are still open.
