namespace Ticketdesk.Extraction;

using Anthropic;
using Anthropic.Models.Messages;

/// <summary>
/// The gateway to the Claude API. The client reads its credentials from the environment
/// (ANTHROPIC_API_KEY, or an `ant auth login` profile); nothing here holds a key.
/// </summary>
public sealed class AnthropicGateway(AnthropicClient client) : IModelGateway
{
    public async Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken)
    {
        var response = await client.Messages.Create(
            new MessageCreateParams
            {
                Model = request.Model,
                MaxTokens = 2048,
                System = request.System,
                Messages = [new UserMessageParam { Content = request.UserText }],
                // Classification does well at low effort; Claude Opus 5.5 defaults to medium.
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = request.Schema is { } schema
                        ? new JsonOutputFormat { Schema = schema.ToDictionary() }
                        : null,
                },
            },
            cancellationToken: cancellationToken);

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        var usage = new TokenUsage(
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheReadInputTokens ?? 0,
            response.Usage.CacheCreationInputTokens ?? 0);
        return new ModelReply(text, response.StopReason?.ToString() ?? "unknown", usage, response.Model.ToString());
    }
}
