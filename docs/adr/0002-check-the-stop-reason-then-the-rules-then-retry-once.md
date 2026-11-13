# 0002 Check the stop reason, then the rules, then retry once

Status: accepted

## Context

With the schema enforced (ADR 0001), a reply has the right shape. That still leaves three ways for an
extraction to be wrong that the shape cannot catch:

- Generation stopped early. A reply cut off at `max_tokens`, or one a safety classifier declined
  (`stop_reason: refusal`), can contain text, and reading that text as an answer is the mistake.
- The extraction breaks a rule the schema cannot express: an empty or over-long summary, or the category
  `outage` with `outage: false`.
- The rule is broken because the model misread the email, and saying what is wrong fixes it.

## Decision

In this order:

1. **Stop reason first.** `refusal` and `max_tokens` fail at once, named as what they are
   (`FailureKind.Refused`, `FailureKind.Truncated`), and are not retried: a retry of the same request
   meets the same classifier or the same limit.
2. **Rules second.** `ExtractionRules.Check` returns each problem as a sentence the model can act on.
3. **One correction.** The first reply goes back as the assistant turn and the problems as the next user
   turn. A second failure is `FailureKind.InvalidAfterRetry`, with the problems attached. One retry, not a
   loop: a reply still wrong after being told exactly what is wrong is a case for a person, and a third
   attempt adds cost without adding information.

For refusals on models that support it, the gateway turns on server-side fallback
(`server-side-fallback-2026-07-01`, `fallbacks: "default"`): the API re-runs a declined request on a
fallback model inside the same call, routed by the refusal's category. Haiku has no server-side fallback,
so a request to it is sent without one. A `refusal` that still reaches the extractor means every model in
the chain declined.

## Consequences

- A caller can route on `FailureKind`: a refusal or a broken rule goes to a person; a truncation means
  `max_tokens` is set wrong.
- The result reports the attempts, the tokens used across them, and which model answered, so a fallback
  or a retry is visible in cost and in logs rather than hidden.
- The rules sit outside the model and are tested on their own; one that encodes a business decision
  (that a suspended account is an outage under billing) says so in a comment.
