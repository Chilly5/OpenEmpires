# Task2 fix round3 — typed old-value qualifiers

Read task-2-fix2-review.md and fix2 section of task-2-report.md first.
The negated/conditional finding is addressed. Fix only the new Important issue:
NamesCurrentValue verifies an amount number but ignores its resource and explicit
stockpile/additional qualifier. Against retained400 AdditionalGathered Food,
`change 400 wood to 500 food` must not change the draft or admit a goal.

Use the existing typed graph data as authority. A named old amount must match its
retained number, resource and any explicitly named amount mode; unspecified mode
does not invent a different mode. Unknown/ambiguous context fails closed locally.
The replacement remains one typed leaf; do not infer a second resource/mode change.
Preserve ZERO HTTP, graph/ticket/known constraints/order/exact refs, six/three budget,
ordinary admission and separate compound approval. Preserve existing tested positive
`instead` and negation handling. No new universal English parser or fact database.

Minimal tests before code, using existing real UI/graph fixture: mismatched old
resource and mismatched explicit old mode leave graph/ticket unchanged with zero
new provider calls/goals/commands; later valid missing-slot reply retains400 Food
AdditionalGathered. Positive correctly qualified old-to-new amount preserves count
and other fields and changes only amount. Include positive old-to-new structure,
source and worker count in a small bounded fixture if needed to establish the
new grammar's actual behavior rather than relying on compilation. Run only new
cases and the directly covering amount/negation controls; not another14/19/40 suite.

Sole source/MCP ownership only on parent dispatch. No paid/network/audio/build/full
regression/commits/reset/cleanup/subagents/Task1/native/UI edits. Exact job recovery,
120000 initialization, one terminal0-start retry, no observer-timeout duplicates or
editor restart. Use fresh direct project/compile/method checks if cache stale.
Append fix3 evidence/commands/IDs/hashes/limitations to task-2-report.md, save raw
artifacts under evidence/task-2-fix3-*, hand ownership back. Parent scoped re-review.
