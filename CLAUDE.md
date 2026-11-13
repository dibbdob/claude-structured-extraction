# Ticketdesk extraction

Turns a customer's support email into the fields a support desk acts on, using the Claude API from C#
(.NET 10, the official `Anthropic` package). TUnit for tests.

## Build

```bash
./build.sh
```

The build never calls the API. Every test goes through `IModelGateway` with a scripted reply; only the
CLI reaches `AnthropicGateway`, and running it is billed.

## Conventions

- An email is untrusted data. It goes in the user turn, never in the system prompt.
- The allowed values for each field live in `Taxonomy`; the prompt and the dataset check read them there.
- `data/tickets.jsonl` is synthetic and hand-labelled; see `data/README.md` before changing a label.
