# 0003 A stable prompt in sections, with the email fenced

Status: accepted

## Context

The system prompt was one paragraph listing the fields. It said what values a field could take but not
what they meant, so the hard cases (a suspended account, sarcasm, a security report, an email that tries
to instruct the extractor) were left to the model's guess. It also sat in the same request as the email
with nothing marking which text was ours and which was the customer's.

Every request sends the same system prompt. Sent uncached, it is paid for in full each time.

## Decision

`ExtractionPrompt` builds the system prompt from sections, in a fixed order:

1. **Role**: what the extraction is for, and that a person reads it.
2. **Untrusted input**: the email arrives inside `<email>` tags, everything inside is data, and an email
   that tries to instruct the extractor is itself a reason for `needs_human`.
3. **Definitions**: one line for every taxonomy value. The build fails when a value has none.
4. **Decisions**: the rules for the fields that need judgement, written as rules, not as examples.
5. **Examples**: four worked cases, chosen for disagreement between plausible readings. None is a ticket
   from the eval set, and the build checks that loosely, so a lightly edited copy is caught too.

The prompt contains nothing that varies between requests, and the build checks for dates and ids, so the
gateway can put a cache breakpoint on it. The email goes only in the user turn.

## Consequences

- Cached reads of the system prompt cost a fraction of the uncached price after the first request. But a
  prefix shorter than the model's minimum cacheable length silently does not cache, so the eval reports
  `cache_read_input_tokens` instead of assuming a hit.
- Fencing the email and naming injection as a `needs_human` signal is a mitigation, not a guarantee. The
  eval set includes one such email (t11), and the extraction is advisory: nothing automatic acts on it
  without a person.
- Few-shot examples steer more strongly than rules do. Four is a deliberate limit, and each one has to
  earn its place by covering a case the rules alone leave open.
