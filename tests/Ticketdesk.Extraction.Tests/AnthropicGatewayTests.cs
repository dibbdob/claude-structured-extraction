namespace Ticketdesk.Extraction.Tests;

using System.Text.Json;

/// <summary>
/// What the gateway would send, read from the SDK's request without calling the API. The SDK sends a field
/// set to null as JSON null, and the API refuses a `fallbacks` field without its beta ("fallbacks: Extra
/// inputs are not permitted"), which the first live eval on Haiku found.
/// </summary>
public sealed class AnthropicGatewayTests
{
    private static ModelRequest Request(string model) => TicketExtractor.RequestFor(model, "Every call returns 503. Enterprise.");

    [Test]
    public async Task A_request_to_a_model_without_server_fallback_sends_no_fallback_fields()
    {
        var body = AnthropicGateway.ToParams(Request("claude-haiku-5-5")).RawBodyData;

        await Assert.That(body.Keys).DoesNotContain("fallbacks");
    }

    [Test]
    public async Task A_request_to_a_model_with_server_fallback_asks_for_it()
    {
        var parameters = AnthropicGateway.ToParams(Request("claude-opus-5-5"));

        await Assert.That(parameters.RawBodyData.Keys).Contains("fallbacks");
        await Assert.That(parameters.Betas!.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("claude-haiku-5-5")]
    [Arguments("claude-opus-5-5")]
    public async Task No_request_sends_an_empty_field(string model)
    {
        var empty = AnthropicGateway.ToParams(Request(model)).RawBodyData
            .Where(f => f.Value.ValueKind == JsonValueKind.Null).Select(f => f.Key).ToList();

        await Assert.That(empty).IsEmpty();
    }
}
