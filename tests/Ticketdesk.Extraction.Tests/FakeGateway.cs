namespace Ticketdesk.Extraction.Tests;

/// <summary>A gateway that replies from a script and records what it was asked. No network, no cost.</summary>
public sealed class FakeGateway : IModelGateway
{
    private readonly Queue<(string Text, string StopReason)> replies;

    public FakeGateway(params string[] replies)
        : this(replies.Select(r => (r, "end_turn")).ToArray())
    {
    }

    public FakeGateway(params (string Text, string StopReason)[] replies) => this.replies = new(replies);

    public List<ModelRequest> Requests { get; } = [];

    public Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken)
    {
        this.Requests.Add(request);
        var (text, stop) = this.replies.Count > 0 ? this.replies.Dequeue() : throw new InvalidOperationException("No scripted reply left.");
        return Task.FromResult(new ModelReply(text, stop, new TokenUsage(500, 80), request.Model));
    }

    public static string Reply(string category = "outage", bool outage = true, string summary = "Every call to the orders API returns 503; checkout is offline.") =>
        System.Text.Json.JsonSerializer.Serialize(new TicketExtraction(category, outage, "enterprise", "api", "frustrated", false, summary));

    public static readonly string WellFormed = Reply();
}
