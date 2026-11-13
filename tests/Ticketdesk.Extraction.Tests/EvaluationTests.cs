namespace Ticketdesk.Extraction.Tests;

using System.Text.Json;
using Ticketdesk.Extraction.Evaluation;

public sealed class EvaluationTests
{
    private static readonly LabelledTicket Outage = new("t01", "API down, Enterprise.", new("outage", true, "enterprise", "api", "frustrated", false));
    private static readonly LabelledTicket Question = new("t02", "How do I rotate a key?", new("question", false, "unknown", "api", "neutral", false));

    /// <summary>Answers each email with the reply scripted for it, by the model named for it.</summary>
    private sealed class ScriptedByEmail(Dictionary<string, (string Reply, string Stop, string Model)> script) : IModelGateway
    {
        public Task<ModelReply> Complete(ModelRequest request, CancellationToken cancellationToken)
        {
            var key = script.Keys.Single(k => request.Turns[0].Text.Contains(k, StringComparison.Ordinal));
            var (reply, stop, model) = script[key];
            return Task.FromResult(new ModelReply(reply, stop, new TokenUsage(1_000_000, 100_000), model));
        }
    }

    private static string Answer(Labels l) => JsonSerializer.Serialize(
        new TicketExtraction(l.Category, l.Outage, l.Plan, l.ProductArea, l.Sentiment, l.NeedsHuman, "A summary."));

    private static Task<EvalRun> Run(Dictionary<string, (string, string, string)> script) =>
        new Evaluator(parallelism: 1)
            .Run("test", "claude-haiku-5-5", new TicketExtractor(new ScriptedByEmail(script), "claude-haiku-5-5"), [Outage, Question]);

    [Test]
    public async Task Each_field_is_scored_on_its_own()
    {
        var wrongPlan = Outage.Expected with { Plan = "team" };
        var run = await Run(new()
        {
            [Outage.Text] = (Answer(wrongPlan), "end_turn", "claude-haiku-5-5"),
            [Question.Text] = (Answer(Question.Expected), "end_turn", "claude-haiku-5-5"),
        });

        await Assert.That(run.For("category").Correct).IsEqualTo(2);
        await Assert.That(run.For("plan").Correct).IsEqualTo(1);
        await Assert.That(run.AllFields.Correct).IsEqualTo(1);
    }

    [Test]
    public async Task A_failure_counts_against_every_field_and_costs_every_attempt()
    {
        var run = await Run(new()
        {
            [Outage.Text] = (Answer(Outage.Expected), "refusal", "claude-haiku-5-5"),
            [Question.Text] = (Answer(Question.Expected), "end_turn", "claude-haiku-5-5"),
        });

        var failed = run.Outcomes.Single(o => o.Id == "t01");
        await Assert.That(failed.Failure).IsEqualTo(FailureKind.Refused);
        await Assert.That(run.For("category").Correct).IsEqualTo(1);
        await Assert.That(failed.Cost).IsGreaterThan(0m);
        await Assert.That(run.Failures).IsEqualTo(1);
    }

    [Test]
    public async Task A_reply_from_a_fallback_model_is_priced_as_that_model_and_counted()
    {
        var run = await Run(new()
        {
            [Outage.Text] = (Answer(Outage.Expected), "end_turn", "claude-opus-4-8"),
            [Question.Text] = (Answer(Question.Expected), "end_turn", "claude-haiku-5-5"),
        });

        await Assert.That(run.AnsweredByFallback).IsEqualTo(1);
        // 1M input at $5 plus 100k output at $25, against Haiku's $0.10 and $0.05.
        await Assert.That(run.Outcomes.Single(o => o.Id == "t01").Cost).IsEqualTo(7.50m);
        await Assert.That(run.Outcomes.Single(o => o.Id == "t02").Cost).IsEqualTo(0.15m);
    }

    [Test]
    public async Task Cache_reads_and_writes_are_priced_at_a_tenth_and_one_and_a_quarter()
    {
        var price = new ModelPrice(4m, 20m);
        await Assert.That(price.Cost(new TokenUsage(0, 0, CacheRead: 1_000_000))).IsEqualTo(0.40m);
        await Assert.That(price.Cost(new TokenUsage(0, 0, CacheWrite: 1_000_000))).IsEqualTo(5.00m);
    }

    [Test]
    public async Task An_unpriced_model_is_an_error_not_a_zero()
    {
        await Assert.That(() => Prices.Cost("claude-unknown", new TokenUsage(1, 1))).Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task Thirty_out_of_thirty_still_has_an_interval_below_ninety_percent()
    {
        var (low, high) = new Accuracy(30, 30).Interval;
        await Assert.That(low).IsLessThan(0.9).And.IsGreaterThan(0.85);
        await Assert.That(high).IsEqualTo(1.0);
    }

    [Test]
    public async Task The_report_states_the_sample_caveat_and_lists_each_miss()
    {
        var run = await Run(new()
        {
            [Outage.Text] = (Answer(Outage.Expected with { Sentiment = "angry" }), "end_turn", "claude-haiku-5-5"),
            [Question.Text] = (Answer(Question.Expected), "end_turn", "claude-haiku-5-5"),
        });

        var report = EvalReport.Markdown(run);
        await Assert.That(report).Contains("have not been shown to differ");
        await Assert.That(report).Contains("| t01 | sentiment | frustrated | angry |");
        await Assert.That(report).Contains(Prices.AsOf);
        await Assert.That(report).Contains("Cost per ticket with all six fields right");
    }
}
