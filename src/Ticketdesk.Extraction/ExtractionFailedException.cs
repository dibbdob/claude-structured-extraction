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

/// <summary>An email could not be turned into an extraction. Carries the last reply and what was wrong.</summary>
public sealed class ExtractionFailedException(FailureKind kind, string message, ModelReply reply, IReadOnlyList<string>? problems = null)
    : Exception(message)
{
    public FailureKind Kind { get; } = kind;

    public ModelReply Reply { get; } = reply;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}
