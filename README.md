# Structured extraction with the Claude API

A support desk receives emails. Before a person reads one, the desk wants to know: is it an outage, which
plan is the customer on, what kind of problem is it, which part of the product, how do they feel, and
does a person need to see it before anything automatic happens. This repository turns an email into those
fields with the Claude API, from C# (.NET 10, the official `Anthropic` package), and measures how well
it does.

The first three fields are exactly what the sibling repository's priority rules take (`outage`, `plan`,
`category`), so the two fit together: this one reads the email, that one decides how fast it must be
answered.

## The history is the point

Each stage is a branch merged into `main` with a merge message written like a pull request. Each one
exists because the stage before it left something unguarded.

```bash
git log --first-parent --reverse --format='%h %s' main
git log --graph --format='%h %s%n%b' main
```

| # | Stage | The problem it answers |
|---|---|---|
| 1 | A naive JSON prompt, a gateway boundary, labelled tickets | (none yet) |
| 2 | `structured-output` | A reply in a code fence or after a sentence broke the parser |
| 3 | `validate-and-retry` | A well-shaped reply can be cut off, refused, or break a business rule |
| 4 | `modular-cached-prompt` | The prompt listed values without meanings, the email was not marked as data, and the stable prefix was paid for in full on every call |
| 5 | `eval-harness` | Nothing so far had been measured |
| 6 | `batch-backfill` | A backlog nobody is waiting on was priced as if someone were |
| 7 | `model-routing` | Sending every email to the strongest model may pay for strength most do not need, or may not |

Some findings in the history came from checking the checks: a failed extraction's cost counted only its
last reply; a fallback model's reply would have crashed the pricing; a test asserted the wrong shape of
spend. Each is in the commit that fixed it.

## What it demonstrates

| Area | Where to look |
|---|---|
| **Structured outputs**: a JSON schema built from one taxonomy, enforced by the API | [`ExtractionSchema`](src/Ticketdesk.Extraction/ExtractionSchema.cs), [ADR 0001](docs/adr/0001-the-api-enforces-the-shape.md) |
| **Validation and one corrective retry**, with the problems named to the model | [`ExtractionRules`](src/Ticketdesk.Extraction/ExtractionRules.cs), [`TicketExtractor`](src/Ticketdesk.Extraction/TicketExtractor.cs), [ADR 0002](docs/adr/0002-check-the-stop-reason-then-the-rules-then-retry-once.md) |
| **Stop reasons**: refusal and `max_tokens` read before the text | [`TicketExtractor.Parse`](src/Ticketdesk.Extraction/TicketExtractor.cs) |
| **Server-side refusal fallback**, only on models that support it | [`AnthropicGateway`](src/Ticketdesk.Extraction/AnthropicGateway.cs) |
| **A modular system prompt** with definitions, judgement rules and few-shot examples | [`ExtractionPrompt`](src/Ticketdesk.Extraction/ExtractionPrompt.cs), [ADR 0003](docs/adr/0003-a-stable-prompt-in-sections-with-the-email-fenced.md) |
| **Prompt injection**: the email fenced as data, an instruction inside it treated as a signal | [`ExtractionPrompt`](src/Ticketdesk.Extraction/ExtractionPrompt.cs), ticket t11 in [the data](data/tickets.jsonl) |
| **Prompt caching**: a byte-stable prefix with a breakpoint, cache reads reported, not assumed | [`AnthropicGateway`](src/Ticketdesk.Extraction/AnthropicGateway.cs), [prompt tests](tests/Ticketdesk.Extraction.Tests/ExtractionPromptTests.cs) |
| **An eval with error bars**: per-field accuracy, 95% intervals, every miss, cost per correct ticket | [`Evaluation/`](src/Ticketdesk.Extraction/Evaluation), [ADR 0004](docs/adr/0004-an-eval-that-says-how-sure-it-is.md), [`eval/`](eval) |
| **Example leakage guarded**: no few-shot example may be a ticket the eval scores | [prompt tests](tests/Ticketdesk.Extraction.Tests/ExtractionPromptTests.cs) |
| **Message Batches**: half price, matched by `custom_id`, nothing silently lost | [`Batches/`](src/Ticketdesk.Extraction/Batches), [ADR 0005](docs/adr/0005-a-backlog-goes-through-batches-and-corrections-go-live.md) |
| **Model routing**, priced per model and held to a measurement | [`RoutedExtractor`](src/Ticketdesk.Extraction/RoutedExtractor.cs), [ADR 0006](docs/adr/0006-a-route-has-to-beat-both-of-its-models.md) |
| **Cost control**: estimates before any live run, nothing sent without `--yes`, dated prices | [CLI](src/Ticketdesk.Extraction.Cli/Program.cs), [`Prices`](src/Ticketdesk.Extraction/Evaluation/Prices.cs) |
| **Tests that never call the API**: one gateway interface, scripted replies | [`IModelGateway`](src/Ticketdesk.Extraction/ModelGateway.cs), [`tests/`](tests/Ticketdesk.Extraction.Tests) |

## Run it

Needs the .NET 10 SDK.

```bash
./build.sh
```

The build is the whole gate and never calls the API: 44 tests, warnings as errors, and checks on the
labelled data and the prompt.

The CLI does call the API, and is billed. Credentials come from `ANTHROPIC_API_KEY` or an
`ant auth login` profile. Set a spend limit on the key first. Every command that spends prints an
estimate and stops unless given `--yes`.

```bash
echo "Every API call returns 503 since 9am. Enterprise." | dotnet run --project src/Ticketdesk.Extraction.Cli -- extract
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku       # estimate only
dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku --yes
```

Estimated cost of one eval over the thirty tickets, at list prices as of 2026-11-03: about $0.01 on
Haiku 5.5, $0.21 on Sonnet 5.5, $0.43 on Opus 5.5. The three runs ADR 0006 asks for come to well under a
dollar.

## What has and has not been verified

- **Verified by the build:** the schema, rules, retry, stop-reason handling, prompt construction, data
  checks, scoring, intervals, pricing, batch matching and routing, each against scripted replies. Four
  guards were also shown failing on a planted breach: the refusal check, the one-retry limit, the
  example-leak check and the missing-batch-result check.
- **Verified by compiling against `Anthropic` 13.8.0:** the request and response shapes, including the
  beta fallback fields and the batch types.
- **Run on GitHub:** `./build.sh`, by the `build` workflow on every push to `main` and every pull request.
- **Run live, on 2026-11-13:** the three evals in ADR 0006, on all thirty tickets, for about 12 cents in
  all. Haiku and Opus at low effort each got 23 of 30 fully right; the route escalated nothing. ADR 0006 is
  rejected, and the CLI's default model is now Haiku. The first run also found that the SDK sends a null
  field as JSON null, which the API refused; the gateway now leaves the fallback fields out entirely, and
  a test reads the request to check it.
- **Not yet verified by a live call:** the Message Batches backfill, and a refusal answered by the
  server-side fallback; no ticket in the set triggered one.

## Layout

```
data/            thirty synthetic, hand-labelled emails, and how they were chosen
docs/adr/        why each decision was made
eval/            how to run an eval; results land here
src/Ticketdesk.Extraction/
  Batches/       Message Batches backfill
  Evaluation/    scoring, intervals, prices, report
                 schema, rules, prompt, extractor, router, gateway
src/Ticketdesk.Extraction.Cli/   extract, eval, backfill
tests/           every test, none of which calls the API
build.sh         the gate
```
