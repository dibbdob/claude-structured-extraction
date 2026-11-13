namespace Ticketdesk.Extraction.Tests;

using Ticketdesk.Extraction.Evaluation;

public sealed class RoutedExtractorTests
{
    private const string Haiku = "claude-haiku-5-5";
    private const string Opus = "claude-opus-5-5";

    /// <summary>A gateway that answers as the model asked and replies from that model's own script.</summary>
    private sealed class PerModel(Dictionary<string, Queue<(string Text, string Stop)>> scripts) : IModelGateway
    {
        public List<string> Asked { get; } = [];

        public Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken)
        {
            this.Asked.Add(request.Model);
            var (text, stop) = scripts[request.Model].Dequeue();
            return Task.FromResult(new ModelReply(text, stop, new TokenUsage(1_000_000, 0), request.Model));
        }
    }

    private static (RoutedExtractor Route, PerModel Gateway) Route(
        (string, string)[] haiku, (string, string)[] opus)
    {
        var gateway = new PerModel(new() { [Haiku] = new(haiku), [Opus] = new(opus) });
        return (new RoutedExtractor(new TicketExtractor(gateway, Haiku), new TicketExtractor(gateway, Opus)), gateway);
    }

    private static readonly (string, string) Good = (FakeGateway.WellFormed, "end_turn");
    private static readonly (string, string) BrokeRule = (FakeGateway.Reply(outage: false), "end_turn");

    [Test]
    public async Task A_clean_first_answer_never_reaches_the_stronger_model()
    {
        var (route, gateway) = Route([Good], []);

        var result = await route.Extract("API down.");

        await Assert.That(result.AnsweredBy).IsEqualTo(Haiku);
        await Assert.That(gateway.Asked).IsEquivalentTo([Haiku]);
    }

    [Test]
    public async Task A_first_answer_that_needed_a_correction_is_escalated_and_both_models_are_paid_for()
    {
        var (route, gateway) = Route([BrokeRule, Good], [Good]);

        var result = await route.Extract("API down.");

        await Assert.That(result.AnsweredBy).IsEqualTo(Opus);
        await Assert.That(gateway.Asked).IsEquivalentTo([Haiku, Haiku, Opus]);
        // Spend is per model: Haiku's attempt and its correction are one entry.
        await Assert.That(result.Spend.Select(s => s.Model)).IsEquivalentTo([Haiku, Opus]);
        await Assert.That(result.Spend[0].Usage.Input).IsEqualTo(2_000_000);
        await Assert.That(result.Usage.Input).IsEqualTo(3_000_000);
        // Two Haiku calls at $0.10 and one Opus call at $4.00 per million input tokens.
        await Assert.That(result.Spend.Sum(s => Prices.Cost(s.Model, s.Usage))).IsEqualTo(4.20m);
    }

    [Test]
    public async Task A_refusal_from_the_first_model_is_escalated()
    {
        var (route, _) = Route([(FakeGateway.WellFormed, "refusal")], [Good]);

        var result = await route.Extract("API down.");

        await Assert.That(result.AnsweredBy).IsEqualTo(Opus);
    }

    [Test]
    public async Task When_both_fail_the_failure_carries_what_both_spent()
    {
        var (route, _) = Route([(FakeGateway.WellFormed, "refusal")], [(FakeGateway.WellFormed, "refusal")]);

        var error = await Assert.That(async () => await route.Extract("API down.")).Throws<ExtractionFailedException>();

        await Assert.That(error!.Kind).IsEqualTo(FailureKind.Refused);
        await Assert.That(error.Spend.Select(s => s.Model)).IsEquivalentTo([Haiku, Opus]);
    }
}
