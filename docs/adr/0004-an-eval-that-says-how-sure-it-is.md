# 0004 An eval that says how sure it is

Status: accepted

## Context

Every change so far (the schema, the rules and retry, the prompt) was made because it should help. None
was measured. Without an eval, a prompt change is judged by trying a few emails by hand, which finds
gross failures and nothing else, and model choice is made on reputation.

The labelled set is thirty hand-written tickets. That is enough to find a field the extractor gets
badly wrong and too few to tell two good configurations apart.

## Decision

`Evaluator` runs the extractor over every labelled ticket and records each outcome: the labels it
produced, any failure by kind, attempts, tokens (cache reads and writes separately), the model that
answered, and the cost at a dated list-price table. A failure is an outcome that counts against every
field and costs every attempt it took; it is not dropped.

`EvalReport` shows each field's accuracy with a 95% Wilson interval, which stays honest at small n
(30 out of 30 is reported as 89% to 100%, not 100%), and says in the report itself that overlapping
intervals have not been shown to differ. It lists every miss, so the next change starts from what went
wrong, not from the score.

A live run prints a cost estimate and stops unless given `--yes`.

## Consequences

- A change to the prompt or the model can be measured, and the measurement says how much it can be
  trusted.
- Cost is reported per ticket and includes retries and fallbacks, so a cheaper model that needs more
  retries is not mistaken for a cheaper extraction.
- Pricing a model the table lacks is an error, not a zero. A fallback model's reply is priced as that
  model.
- The set is the limit. Separating configurations a few points apart needs more labelled tickets, and
  the report says so rather than leaving it to the reader.
