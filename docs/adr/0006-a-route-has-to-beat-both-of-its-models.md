# 0006 A route has to beat both of its models

Status: rejected: the eval chose Claude Haiku 5.5 alone (2026-11-13)

## Context

Claude Haiku 5.5 costs a fortieth of Claude Opus 5.5 per input token. If most emails are easy, sending
every one to Opus pays for strength that most of them do not need. The obvious design is a cascade: try
Haiku, escalate to Opus when Haiku struggles.

The obvious design has costs that are easy to miss:

- An escalated ticket is paid for twice, once per model.
- The prompt cache is per model. A cascade keeps two caches warm instead of one, and a ticket that
  escalates reads neither cheaply on its first visit to the second.
- The simpler alternative is often as good: the strongest model at low effort can match a cascade's
  accuracy at a cost close to it, with one model, one cache and no routing logic.
- "Struggles" has to be measurable. A model's own statement of confidence is not reliable enough to route
  on. What is measurable here: the extraction failed, or it needed a correction.

## Decision

`RoutedExtractor` tries Haiku and escalates to Opus when Haiku fails (refused, cut off, unparseable,
still invalid after a correction) or needed a correction. Every model's spend is recorded and priced at
its own rate, so an escalated ticket's cost includes the Haiku attempt that failed.

The route is not the default. It is used only if the eval shows it beats both of these at **cost per
ticket with all six fields right**, which the report prints:

1. `claude-haiku-5-5` alone;
2. `claude-opus-5-5` alone, at low effort (the extractor's setting).

```bash
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku --yes
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-opus-5-5 --label opus-low --yes
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model route:haiku-then-opus --label route --yes
```

With thirty tickets, configurations whose intervals overlap have not been shown to differ. In that case
the decision goes to the simpler configuration, which is one model.

## Result

Run on 2026-11-13 on the thirty tickets; the reports are in `eval/results/`.

| Configuration | All six right | 95% interval | Cost per ticket with all six right |
|---|---|---|---|
| Haiku alone | 23/30 | 59 % to 88 % | $0.000119 |
| Opus alone, low effort | 23/30 | 59 % to 88 % | $0.004937 |
| Route | 23/30 | 59 % to 88 % | $0.000083 |

The route escalated no ticket: Haiku never failed and never needed a correction, so the route was Haiku
with extra steps. Its lower cost is the prompt cache, which the Haiku run had just written and the route
read. It gave the same answers as Haiku on every ticket. The three intervals are identical, so by the rule
above the decision goes to one model, and of the two, Haiku is the one that costs a fortieth as much.

The extractor's default is Claude Haiku 5.5. The route stays in the code, and the eval can rerun it when
the tickets get harder; until a ticket escalates, it has nothing to show.

Both models missed in the same place: five or four tickets labelled `frustrated` read as `neutral`. That
is either the labels or the prompt's definition of frustrated, and it is a question for `data/README.md`,
not for routing.

## Consequences

- The routing decision will rest on a measurement, and this ADR records which one.
- If Haiku alone is accurate enough, the route is unnecessary; if Opus at low effort is cheap enough, it
  is unnecessary too. Either outcome is a result, not a failure of the design.
- When the eval has run, this ADR is updated with the numbers and its status becomes accepted or
  rejected.
