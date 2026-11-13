# 0001 The API enforces the reply's shape

Status: accepted

## Context

The first extractor asked for "a JSON object only" in its system prompt and deserialised whatever came
back. A model that follows the instruction nearly always is a parser that fails sometimes: a code fence,
a sentence before the object, a field name in a different case, a value outside the taxonomy. Each has a
fix (strip fences, find the first brace, lowercase keys), and each fix is a guess about the next way it
will go wrong.

## Decision

The request carries a JSON schema in `output_config.format` (structured outputs), built from `Taxonomy`:
every field required, each enumerated field limited to its taxonomy values, no other properties. The API
constrains generation to it, so the reply is valid JSON of the right shape.

The prompt keeps what the schema cannot say: what each field means and how to decide it. The schema keeps
what the prompt cannot guarantee: the shape.

## Consequences

- No recovery code. A reply that still fails to parse raises `ExtractionFailedException` with the reply
  attached, because it means something is wrong that recovery would hide.
- The taxonomy has one definition. Adding a category changes the prompt, the schema and the dataset check
  together.
- Shape is not correctness. A schema-valid reply can still say `outage: false` for an outage, or carry a
  200-word summary, since string length is not something this schema carries. Those are checked after
  the reply arrives.
