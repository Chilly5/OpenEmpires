# Phase 4D.2 affected-runner evidence

Runner: `/root/phase4d2_affected_runner`  
Unity: Open Empires@6d7310c7, Unity 6000.5.9f1  
Preflight: editor idle; compilation false; domain reload false; tools ready; error console returned 0 entries.

## Completed job

- Group: `OpenEmpires.Tests.CommanderPhase4D2Tests` (EditMode)
- Job: `b6ab5c7fc8b74a68af3b6c89a2dee512`
- Terminal: succeeded; 42 discovered, 42 passed, 0 failed, 0 skipped; 10.8675 seconds.
- All 42 fully qualified IDs are preserved in the terminal payload.
- Source SHA-256: `CommanderPhase4D2Tests.cs` = `6A0D517CDC9D7EAF7C5AF709EC43BC2E61D181EB27A354CF344636316B576BD2`; snapshot = `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408`; planner health = `EAFDFC54C8B98757FBDDC40554C6E60256A5100B23A34D6FAD738ACA7F88EC9B`.
- Evidence: [phase4d2-affected-terminal-payloads.json](phase4d2-affected-terminal-payloads.json), SHA-256 `6541C157B1E21B9A5F7B20A67B76B5D11BCAD985F31D34FAB3E7B1887B134F2F`.
- Unity MCP supplied no native NUnit XML; complete terminal tool payload was retained.

## Not run

D2 host PlayMode and affected Phase 4C.2/4C.4/4D.1 regression groups were not started. Root instructed the runner to stop after the current terminal job because independent Task 3 review found an Important test-adequacy gap requiring test-only changes; reruns after that fix supersede this historical result. The already-passed frozen Task 3 runtime job was not repeated.
