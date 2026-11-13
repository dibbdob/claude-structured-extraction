# Evals

`eval` scores one model on the thirty labelled tickets in `data/tickets.jsonl` and writes:

- `eval/results/<label>.md`: accuracy per field with a 95% interval, failures by kind, retries, how many
  tickets a fallback model answered, tokens including cache reads and writes, cost, and every miss.
  Commit these.
- `eval/runs/<label>-<time>.json`: every outcome in full. Ignored by git.

```bash
# Prints an estimate and stops; nothing is sent.
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku

# Spends it.
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku --yes
```

Credentials come from `ANTHROPIC_API_KEY` or an `ant auth login` profile. Set a monthly spend limit on the
key before the first run.

**No results are committed yet.** Every number in `eval/results/` comes from a real run; none is
estimated or written by hand.
