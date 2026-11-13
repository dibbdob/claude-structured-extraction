namespace Ticketdesk.Extraction.Tests;

/// <summary>A gateway that replies from a script and records what it was asked. No network, no cost.</summary>
public sealed class FakeGateway(params string[] replies) : IModelGateway
{
    private readonly Queue<string> replies = new(replies);

    public List<ModelRequest> Requests { get; } = [];

    public string StopReason { get; set; } = "end_turn";

    public Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken)
    {
        this.Requests.Add(request);
        var text = this.replies.Count > 0 ? this.replies.Dequeue() : throw new InvalidOperationException("No scripted reply left.");
        return Task.FromResult(new ModelReply(text, this.StopReason, new TokenUsage(500, 80), request.Model));
    }

    public const string WellFormed = """
        {"category":"outage","outage":true,"plan":"enterprise","product_area":"api","sentiment":"frustrated","needs_human":false,"summary":"Every call to the orders API returns 503; checkout is offline."}
        """;
}
