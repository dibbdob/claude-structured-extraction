namespace Ticketdesk.Extraction.Evaluation;

/// <summary>Runs an extractor over labelled tickets and records every outcome, failures included.</summary>
public sealed class Evaluator(Func<string, TicketExtractor> extractorFor, int parallelism = 4)
{
    public async Task<EvalRun> Run(string label, string model, IReadOnlyList<LabelledTicket> tickets, CancellationToken cancellationToken = default)
    {
        var extractor = extractorFor(model);
        var outcomes = new TicketOutcome[tickets.Count];
        var started = DateTimeOffset.UtcNow;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, tickets.Count),
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = cancellationToken },
            async (i, ct) => outcomes[i] = await Score(extractor, model, tickets[i], ct));

        return new EvalRun(label, model, started, outcomes);
    }

    private static async Task<TicketOutcome> Score(TicketExtractor extractor, string model, LabelledTicket ticket, CancellationToken ct)
    {
        try
        {
            var result = await extractor.Extract(ticket.Text, ct);
            return new TicketOutcome(ticket.Id, ticket.Expected, Labels.From(result.Extraction), null, result.Attempts,
                result.Usage, result.AnsweredBy, Prices.Cost(result.AnsweredBy, result.Usage));
        }
        catch (ExtractionFailedException failure)
        {
            // A failure is an outcome, not a crash: it counts against every field, is reported by kind, and
            // costs what every attempt on it cost.
            return new TicketOutcome(ticket.Id, ticket.Expected, null, failure.Kind, failure.Attempts, failure.Usage,
                failure.Reply.Model, Prices.Cost(failure.Reply.Model, failure.Usage));
        }
    }
}
