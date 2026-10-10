# Fixture initialization triage

Job `a7493934f514460f8327b26a4d2e8492` executed zero tests: the new fixture initially called `SetVisionCheat` on GameSimulation instead of FogOfWarData and did not compile. Its zero-test Passed summary is not verification. The test-only receiver was corrected before retry; no production recovery change had yet been made.
