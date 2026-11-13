namespace Ticketdesk.Extraction;

using System.Text.Json;

/// <summary>Turns a customer's email into a <see cref="TicketExtraction"/>.</summary>
public sealed class TicketExtractor(IModelGateway gateway, string model = "claude-opus-5-5")
{
    public const string SystemPrompt = """
        You read emails sent to a software company's support desk and extract what the desk needs.

        Reply with a JSON object only, with these fields:
        - category: one of outage, bug, billing, account, question, feature_request
        - outage: true if the customer cannot use the product at all right now
        - plan: one of starter, team, enterprise, unknown
        - product_area: one of api, dashboard, billing, integrations, mobile, other
        - sentiment: one of positive, neutral, frustrated, angry
        - needs_human: true if a person must read this before anything automatic happens
        - summary: one sentence, at most 200 characters
        """;

    public async Task<TicketExtraction> Extract(string email, CancellationToken cancellationToken = default)
    {
        var reply = await gateway.Complete(new ModelRequest(model, SystemPrompt, email), cancellationToken);
        return JsonSerializer.Deserialize<TicketExtraction>(reply.Text)
            ?? throw new InvalidOperationException("The model replied with JSON null.");
    }
}
