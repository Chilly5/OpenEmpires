# Read-only legacy offline-transport test triage

This is a narrow preparation for Task7, not test execution or another general
repository inventory. Identify pre-existing test fixtures under Assets/Tests whose
setup/test path may reach real HTTP, speech/audio, microphone, native filesystem
credentials, provider initialization or external services. Start with targeted rg
transport/provider/audio patterns, inspect only matching fixtures and their relevant
setup/helper calls. Distinguish a constructed production provider with fixture
transport from one able to dispatch real HTTP. An invalid endpoint is not zero risk.

Output one compact report: offline-safe/exclude/uncertain candidate, exact class and
test/helper path with file:line, reason. Include entire known live fixtures
CommanderPhase5ALiveRuntimePlayModeTests and CommanderPhase5BFinalLivePlayModeTests.
Do not claim this is the final frozen full-name NUnit discovery list; Task7 must
revalidate it after source freeze and inspect new/changed Phase5C fixtures.

Read-only source checkout. No source/test edits, MCP, test runner, builds, network,
credentials/config secrets, environment values, process command lines, paid calls,
commits or subagents. Sole permitted write is
Docs/CommanderPhase5C/offline-test-triage-report.md via apply_patch. Task4 has the sole
source/Unity writer; don't review its incomplete new tests. Use only legacy fixtures
and mark any concurrent source drift. Return short outcome/report path/uncertainties.
