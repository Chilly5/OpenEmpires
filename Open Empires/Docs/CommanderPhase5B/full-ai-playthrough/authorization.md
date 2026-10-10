# Fresh playthrough paid authorization

2026-10-10. Direct human authorization: “200 Paid HTTP Attempts is authorized”.

This is a fresh cap of 200 actual configured OpenRouter Luna HTTP attempts for
the full normal 1v1 playthrough, including timeouts, errors and numeric schema
repairs. Existing protected ledgers and prior allowances remain unchanged.

Gameplay stays in the normal desktop UI. The passive observer does not replace
the provider/transport or issue commands. Before each new provider submission,
reserve room for the maximum two HTTP attempts; observe the safe production trace
before releasing the unused reservation. Stop if the remaining allowance cannot
cover a submission. No timeout/retry or match setting is changed.
