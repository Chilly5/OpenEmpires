# Phase 4E live provider acceptance

Date: 2026-09-27

Provider: OpenRouter \`openai/gpt-6-luna\`, through the existing
\`OpenRouterCommanderProvider\` endpoint and the configured
\`OPENEMPIRES_OPENROUTER_KEY\` setting. No credential value is recorded here.

The controlled corpus used four requests (one request per row, four total
requests, \`max_tokens=256\`, reasoning effort \`none\`):

| Input | HTTP | Parsed outcome | Schema result |
| --- | ---: | --- | --- |
| \`hey I want 10 spearmen\` | 200 | \`Request\` | valid, one bounded node |
| \`could you get me ten spearmen?\` | 200 | \`Request\` | valid, one bounded node |
| \`we need about 10 spears\` | 200 | \`Request\` | valid, one bounded node |
| \`make my base better\` | 200 | \`Clarify\` | valid, no nodes |

The live responses were inspected only for HTTP status, outcome enum, and
bounded node count; model text was not copied into the repository. The first
three paraphrases produced the same semantic request shape, while the
ambiguous request clarified instead of selecting an arbitrary strategy.

This is provider/semantic evidence, not proof that every mandatory scenario
has completed in a standalone build. Game authority remains in the existing
strict parser, resolver, goal manager, and normal command path.
