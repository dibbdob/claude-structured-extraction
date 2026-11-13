# Eval: opus-low

- Model asked: `claude-opus-5-5`
- Run: 2026-11-13 18:28 UTC, 30 tickets
- Failures: 0; retried: 1; answered by a fallback model: 0
- Tokens: 1,669 in, 2,627 out, 47,628 cache read, 7,056 cache write
- Cost: $0.1135 at list prices as of 2026-11-03; $0.003785 per ticket
- Cost per ticket with all six fields right: $0.004937 (the number to compare configurations on)

| Field | Correct | Accuracy | 95% interval |
|---|---|---|---|
| category | 28/30 | 93 % | 79 % to 98 % |
| outage | 29/30 | 97 % | 83 % to 99 % |
| plan | 30/30 | 100 % | 89 % to 100 % |
| product_area | 30/30 | 100 % | 89 % to 100 % |
| sentiment | 25/30 | 83 % | 66 % to 93 % |
| needs_human | 30/30 | 100 % | 89 % to 100 % |
| **all six** | 23/30 | 77 % | 59 % to 88 % |

With 30 tickets an interval this wide is expected; two configurations whose intervals overlap have not been shown to differ.

## Misses

| Ticket | Field | Expected | Got |
|---|---|---|---|
| t03 | sentiment | frustrated | neutral |
| t06 | outage | false | true |
| t06 | sentiment | frustrated | neutral |
| t09 | category | account | bug |
| t12 | sentiment | frustrated | neutral |
| t16 | sentiment | frustrated | neutral |
| t18 | sentiment | frustrated | neutral |
| t20 | category | billing | outage |
