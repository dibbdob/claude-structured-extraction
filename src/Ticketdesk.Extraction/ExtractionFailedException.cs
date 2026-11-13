namespace Ticketdesk.Extraction;

/// <summary>Why an email could not be turned into an extraction.</summary>
public enum FailureKind
{
    /// <summary>The reply was not JSON of the expected shape.</summary>
    Unparseable,

    /// <summary>Generation stopped at max_tokens; the reply is cut off.</summary>
    Truncated,

    /// <summary>The model, and any fallback model, declined the request.</summary>
    Refused,

    /// <summary>The extraction broke a rule, and still did after one correction.</summary>
    InvalidAfterRetry,
}

/// <summary>
/// An email could not be turned into an extraction. Carries the last reply, what was wrong, and the tokens
/// and attempts spent across the whole extraction, so a failure's cost is not under-reported.
/// </summary>
public sealed class ExtractionFailedException(
    FailureKind kind, string message, ModelReply reply, IReadOnlyList<string>? problems = null, TokenUsage? usage = null, int attempts = 1)
    : Exception(message)
{
    public FailureKind Kind { get; } = kind;

    public ModelReply Reply { get; } = reply;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    public TokenUsage Usage { get; } = usage ?? reply.Usage;

    public int Attempts { get; } = attempts;

    public ExtractionFailedException Spent(TokenUsage total, int attemptsSoFar) =>
        new(this.Kind, this.Message, this.Reply, this.Problems, total, attemptsSoFar);
}
