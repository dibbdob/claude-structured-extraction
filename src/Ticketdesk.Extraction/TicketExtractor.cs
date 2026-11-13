namespace Ticketdesk.Extraction;

using System.Text.Json;

/// <summary>The extraction, and what it took to get it.</summary>
public sealed record ExtractionResult(TicketExtraction Extraction, int Attempts, TokenUsage Usage, string AnsweredBy);

/// <summary>Turns a customer's email into a <see cref="TicketExtraction"/>.</summary>
public sealed class TicketExtractor(IModelGateway gateway, string model = "claude-opus-5-5")
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Schema = ExtractionSchema.Build();

    /// <summary>
    /// Extract, check the rules, and if any is broken ask once for a correction with the problems named.
    /// One retry, not a loop: a reply that is still wrong after being told exactly what is wrong is a case
    /// for a person, and another attempt only adds cost.
    /// </summary>
    public async Task<ExtractionResult> Extract(string email, CancellationToken cancellationToken = default)
    {
        var turns = new List<Turn> { Turn.User(ExtractionPrompt.UserTurn(email)) };
        var usage = new TokenUsage(0, 0);

        for (var attempt = 1; ; attempt++)
        {
            var reply = await gateway.Complete(new ModelRequest(model, ExtractionPrompt.SystemPrompt, turns, Schema, CacheSystem: true), cancellationToken);
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
