# 0005 A backlog goes through batches, and corrections go live

Status: accepted

## Context

Turning on the extractor leaves a backlog: every open email received before it existed. Nobody is waiting
on those answers in seconds, and sending them one by one at the live rate pays for a speed nobody needs.

## Decision

`Backfill` submits the backlog as one Message Batch, which is billed at half the live rate and returns
within a day. It sends exactly the request the live extractor first sends (`TicketExtractor.RequestFor`),
so the batch reads the same cached prefix and is scored by the same rules.

- **Results are matched by `custom_id`.** A batch returns results in any order; matching by position
  would attach one customer's extraction to another's email without any error.
- **`custom_id`s are checked before anything is sent.** One that the API would refuse, or two emails sharing
  one, fails before submission instead of after a wait.
- **A batch has no second turn,** so a reply that breaks a rule is not corrected inside it. It goes to
  `NeedsAnotherPass` with its reason, alongside refusals, errors, expiries and any email the batch never
  reported on. That short list goes through the live extractor, which can correct a broken rule once.
- **No server-side fallback in a batch.** The Batches API does not accept it. A refusal there comes back
  as a refusal, and the live pass, which has fallback, is where it gets a second chance.

## Consequences

- Most of a backlog costs half as much. The share that needs a live pass is paid at the live rate, and
  the eval's retry and failure rates are the estimate of how big that share will be.
- Nothing in the backlog is silently lost: every email ends either extracted or listed with a reason.
