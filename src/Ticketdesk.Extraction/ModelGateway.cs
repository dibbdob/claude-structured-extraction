namespace Ticketdesk.Extraction;

/// <summary>One request to a model, in the extractor's terms rather than the SDK's.</summary>
public sealed record ModelRequest(
    string Model,
    string System,
    string UserText,
    IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Schema = null);

/// <summary>Token counts for one call, as the API reports them.</summary>
public sealed record TokenUsage(long Input, long Output, long CacheRead = 0, long CacheWrite = 0);

/// <summary>What came back: the text, why generation stopped, and what it used.</summary>
public sealed record ModelReply(string Text, string StopReason, TokenUsage Usage, string Model);

/// <summary>
/// The one place the extractor reaches a model. The tests replace it, so the build never calls the API
/// and never costs anything.
/// </summary>
public interface IModelGateway
{
    Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken);
}
