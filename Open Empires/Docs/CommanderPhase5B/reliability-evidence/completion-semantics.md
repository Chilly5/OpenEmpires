# Finite resource objective completion

Human clarification: **Leave gathering orders running**.

At the resource target, the Commander task stops monitoring/recovery and issuing
new actions and its card completes. Already issued ordinary gathering orders are
left running, with their existing source constraints, until ordinary gameplay or
the human changes them. No synthetic Stop, rollback or income credit is added.
The native positive test includes 90 post-completion ticks and asserts no further
Commander recovery command. This interpretation is explicit human direction,
not a narrower completion definition inferred by the implementation.

Second human clarification: **Player-wide Food income is intended** for
“gather 400 additional Food from berries with four villagers.” AdditionalGathered
counts the player's actual resource income since activation, including income
from other workers or Food sources. Berries and the frozen four-worker selection
restrict the Commander orders, not income attribution. Spending does not undo
credited gathering income. No per-worker/source-delivery accounting was added.
