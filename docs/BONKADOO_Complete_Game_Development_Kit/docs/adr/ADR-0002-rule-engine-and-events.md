# ADR-0002 Rule engine semantics and world events
Status: accepted (M0)

## Decision
1. **Resolution order:** candidate rules sorted by `priority` descending, then `id` ordinal ascending. Every rule is terminal; the first rule whose predicate matches wins.
2. **Eligibility:** a puzzle rule is eligible only when all of its puzzle's `prerequisites` flags are `true` and, for `once` rules, it has not fired before. The loader ANDs prerequisites into the rule; content stays unchanged.
3. **Flags:** typed values (bool / int / string). An absent boolean flag compares equal to `false`.
4. **Transient input:** keys starting with `input.` are read only from the request's input map, never from saved state. `setFlag` on `input.*` is a validation error.
5. **Request predicates (additive):** `requestVerb`, `requestTarget`, `requestSource`, so M1 puzzles can match real interaction traces (INTERACTION_IMPLEMENTATION_NOTES). Existing seed content needs no change.
6. **Atomicity:** effects are dry-run against a scratch copy of state. Any failure (removing an unheld item, unknown metric, `input.*` write) rejects the whole rule — nothing commits — and the outcome is the localized fallback with an error code.
7. **Events:** state changes are `WorldEvent`s (`sys.flag.set`, `sys.metric.set`, `sys.item.added`, `sys.item.removed`, `sys.exit.unlocked`, `sys.npc.disposition`, `sys.rule.fired`, `sys.request.committed`, `sys.scene.entered`) plus the content's narrative events (`evt.*`). IDs are `<requestId>:<index>`; payload is an ordinal-sorted string map instead of a JSON string. `queueDialogue` / `queueAnimation` are outcome outputs, not state.
8. **Idempotency:** a request ID that already committed returns a duplicate outcome with no events; applying an event ID twice is a no-op.
9. **World data:** metric and NPC-disposition ranges live in `content/world/world.json` (values from STORY_BIBLE; NPC range assumed [-3,3]).

## Consequences
World state is reproducible by replaying the journal from an empty state or a snapshot. DSL changes require a new ADR and a content migration.
