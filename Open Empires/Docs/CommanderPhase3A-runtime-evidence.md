# Phase 3A live runtime evidence

Unity 6000.5.9f1, SampleScene, local French player; no enemy AI. Editor-only fixture sets Age 3 and starting resources, then each goal executes solely through the existing command pipeline and live GameBootstrapper ticks. Four extra villagers are queued normally. Time scale 10 accelerates wall-clock observation without changing simulation construction/training times. Before scenario 4, wood is set to zero as fixture setup, two villagers receive human gold orders, and 930 ticks elapse before the constrained goal is submitted (the human lease lasts 900 ticks).

The first attempt was discarded after a domain reload. The following is the completed second attempt:

```text
[Phase3A QA] Live local fixture starting; no enemy AI; normal construction/training times.
[Phase3A QA] Setup: French Age 3, starting budget F4000/W4000/G1000, four villagers queued normally.
[Phase3A QA] INPUT make 10 archers RESPONSE Understood. Preparing 10 archers.
[Phase3A QA] scenario=1 tick=2380 status=WaitingForProduction reason=All compatible production queues are at Commander limit 3. archers=2 spearmen=0 woodWorkers=0 goldWorkers=0 protected=0 protectionViolated=False humanWorker=-1 humanControlViolated=False commands=7
[Phase3A QA] scenario=1 tick=4531 status=Completed reason=Owned 10/10 living units. archers=10 spearmen=0 woodWorkers=0 goldWorkers=0 protected=0 protectionViolated=False humanWorker=-1 humanControlViolated=False commands=13
[Phase3A QA] PASS scenario 1: make 10 archers
[Phase3A QA] INPUT build barracks RESPONSE Understood. I will construct 1 barracks.
[Phase3A QA] scenario=2 tick=5476 status=Completed reason=Barracks construction complete (1/1). archers=10 spearmen=0 woodWorkers=0 goldWorkers=0 protected=0 protectionViolated=False humanWorker=-1 humanControlViolated=False commands=1
[Phase3A QA] PASS scenario 2: build barracks
[Phase3A QA] Human assigned worker #0 to gold immediately before wood goal.
[Phase3A QA] INPUT put 8 villagers on wood RESPONSE Understood. I will assign at least 8 villagers to wood.
[Phase3A QA] scenario=3 tick=5548 status=WaitingForResources reason=Assigning at least 8 villagers to Wood; currently 1. archers=10 spearmen=0 woodWorkers=1 goldWorkers=1 protected=0 protectionViolated=False humanWorker=0 humanControlViolated=False commands=1
[Phase3A QA] scenario=3 tick=6136 status=Completed reason=8/8 villagers assigned to Wood. archers=10 spearmen=0 woodWorkers=8 goldWorkers=1 protected=0 protectionViolated=False humanWorker=0 humanControlViolated=False commands=8
[Phase3A QA] PASS scenario 3: put 8 villagers on wood
[Phase3A QA] INPUT make 10 spearmen don't touch gold RESPONSE Understood. Preparing 10 spearmen.
[Phase3A QA] scenario=4 tick=10276 status=Completed reason=Owned 10/10 living units. archers=10 spearmen=10 woodWorkers=8 goldWorkers=2 protected=2 protectionViolated=False humanWorker=0 humanControlViolated=False commands=13
[Phase3A QA] PASS scenario 4: make 10 spearmen don't touch gold
[Phase3A QA] ALL FOUR SCENARIOS PASSED.
[Phase3A QA] scenario=5 tick=10591 status= reason= archers=10 spearmen=10 woodWorkers=8 goldWorkers=2 protected=2 protectionViolated=False humanWorker=0 humanControlViolated=False commands=0
```

Observed completion responses:

- Your 10 archers are ready.
- The barracks is complete.
- At least 8 villagers are assigned to wood.
- Your 10 spearmen are ready.

Final screenshot: [completed live base](../Assets/Screenshots/screenshot-20260904-134920.png).

Reproduction: enter fresh Play Mode without starting a match, then select `Open Empires/Commander/Phase 3A QA/Start Four Scenarios`. Use `Log Evidence` in the same submenu for current counts. The fixture refuses to overwrite an existing simulation. Do not import scripts/assets during the run; a domain reload resets this in-memory game.

