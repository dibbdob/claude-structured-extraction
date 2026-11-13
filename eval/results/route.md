# Eval: route

- Model asked: `claude-haiku-5-5`
- Run: 2026-11-13 18:28 UTC, 30 tickets
- Failures: 0; retried: 0; answered by a fallback model: 0
- Tokens: 1,480 in, 2,471 out, 52,920 cache read, 0 cache write
- Cost: $0.0019 at list prices as of 2026-11-03; $0.000064 per ticket
- Cost per ticket with all six fields right: $0.000083 (the number to compare configurations on)

| Field | Correct | Accuracy | 95% interval |
|---|---|---|---|
| category | 30/30 | 100 % | 89 % to 100 % |
| outage | 29/30 | 97 % | 83 % to 99 % |
| plan | 30/30 | 100 % | 89 % to 100 % |
| product_area | 27/30 | 90 % | 74 % to 97 % |
| sentiment | 26/30 | 87 % | 70 % to 95 % |
| needs_human | 30/30 | 100 % | 89 % to 100 % |
| **all six** | 23/30 | 77 % | 59 % to 88 % |

With 30 tickets an interval this wide is expected; two configurations whose intervals overlap have not been shown to differ.

## Misses

| Ticket | Field | Expected | Got |
|---|---|---|---|
| t03 | sentiment | frustrated | neutral |
| t06 | outage | false | true |
| t06 | sentiment | frustrated | neutral |
| t12 | sentiment | frustrated | neutral |
| t17 | product_area | billing | other |
| t18 | sentiment | frustrated | neutral |
| t22 | product_area | other | dashboard |
| t26 | product_area | api | other |
