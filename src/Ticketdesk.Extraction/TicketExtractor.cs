namespace Ticketdesk.Extraction;

using System.Text.Json;

/// <summary>The extraction, and what it took to get it.</summary>
public sealed record ExtractionResult(TicketExtraction Extraction, int Attempts, TokenUsage Usage, string AnsweredBy);

/// <summary>Turns a customer's email into a <see cref="TicketExtraction"/>.</summary>
public sealed class TicketExtractor(IModelGateway gateway, string model = "claude-opus-5-5")
{
    public const string SystemPrompt = """
        You read emails sent to a software company's support desk and extract what the desk needs.

        The reply's shape is enforced by a schema. What each field means:
        - category: one of outage, bug, billing, account, question, feature_request
        - outage: true if the customer cannot use the product at all right now
        - plan: one of starter, team, enterprise, unknown
        - product_area: one of api, dashboard, billing, integrations, mobile, other
        - sentiment: one of positive, neutral, frustrated, angry
        - needs_human: true if a person must read this before anything automatic happens
        - summary: one sentence, at most 200 characters
        """;

    private static readonly IReadOnlyDictionary<string, JsonElement> Schema = ExtractionSchema.Build();

    /// <summary>
    /// Extract, check the rules, and if any is broken ask once for a correction with the problems named.
    /// One retry, not a loop: a reply that is still wrong after being told exactly what is wrong is a case
    /// for a person, and another attempt only adds cost.
    /// </summary>
    public async Task<ExtractionResult> Extract(string email, CancellationToken cancellationToken = default)
    {
        var turns = new List<Turn> { Turn.User(email) };
        var usage = new TokenUsage(0, 0);

        for (var attempt = 1; ; attempt++)
        {
            var reply = await gateway.Complete(new ModelRequest(model, SystemPrompt, turns, Schema), cancellationToken);
            usage += reply.Usage;

            var extraction = Parse(reply);
            var problems = ExtractionRules.Check(extraction);
            if (problems.Count == 0)
            {
                return new ExtractionResult(extraction, attempt, usage, reply.Model);
            }

            if (attempt == 2)
            {
                throw new ExtractionFailedException(
                    FailureKind.InvalidAfterRetry, "The extraction still broke a rule after one correction.", reply, problems);
            }

            turns.Add(Turn.Assistant(reply.Text));
            turns.Add(Turn.User(
                "That extraction breaks these rules:\n- " + string.Join("\n- ", problems) +
                "\nReply with the corrected extraction for the same email."));
        }
    }

    private static TicketExtraction Parse(ModelReply reply)
    {
        // Check why generation stopped before reading what it produced: a refusal or a cut-off reply can
        // still contain text, and that text is not an answer.
        switch (reply.StopReason)
        {
            case "refusal":
                throw new ExtractionFailedException(FailureKind.Refused, "The request was declined.", reply);
            case "max_tokens":
                throw new ExtractionFailedException(FailureKind.Truncated, "The reply was cut off at max_tokens.", reply);
        }

        try
        {
            return JsonSerializer.Deserialize<TicketExtraction>(reply.Text)
                ?? throw new ExtractionFailedException(FailureKind.Unparseable, "The reply was JSON null.", reply);
        }
        catch (JsonException error)
        {
            throw new ExtractionFailedException(FailureKind.Unparseable, $"The reply did not parse: {error.Message}", reply);
        }
    }
}
