# Commander Phase 4E.6 Conversation Plan

## Goal

Support bounded semantic follow-ups and clarification context without allowing conversation text to become game authority.

## Design

- Attach a separate, bounded `CommanderSemanticConversationMemory` to the existing match-scoped `ConversationState`.
- Store only accepted semantic facts (unit target totals and structure placement language) or a parser-validated clarification message.
- Pass a detached snapshot to semantic providers as an explicitly untrusted memory projection.
- Record facts only after tactical admission creates a real goal; rejected, unsupported, stale, or strategic-only responses do not become tactical memory.
- Reset clears semantic facts together with the existing conversation memory, and the existing runtime-generation guard rejects late provider responses.

## Verification

- Bound/serialization test proves no IDs, coordinates, commands, or unbounded entries are retained.
- Follow-up test proves an accepted unit fact reaches the next provider request and disappears after reset.
- Clarification test proves a clarification is available to one follow-up but fails closed after reset.
- Existing provider and Phase 4E regressions remain required before commit.
