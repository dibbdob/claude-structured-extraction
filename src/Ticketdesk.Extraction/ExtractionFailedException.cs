namespace Ticketdesk.Extraction;

/// <summary>An email could not be turned into an extraction. Carries the reply that caused it.</summary>
public sealed class ExtractionFailedException(string message, ModelReply reply) : Exception(message)
{
    public ModelReply Reply { get; } = reply;
}
