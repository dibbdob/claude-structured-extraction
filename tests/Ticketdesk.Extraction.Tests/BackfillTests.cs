namespace Ticketdesk.Extraction.Tests;

using System.Runtime.CompilerServices;
using Ticketdesk.Extraction.Batches;

public sealed class BackfillTests
{
    /// <summary>Reports "not ended" a set number of times, then returns the scripted items in the order given.</summary>
    private sealed class FakeBatches(IReadOnlyList<BatchItem> items, int pollsBeforeEnd = 2) : IBatchGateway
    {
        public List<(string CustomId, ModelRequest Request)> Submitted { get; } = [];

        public int Polls { get; private set; }

        public Task<string> Submit(IReadOnlyList<(string CustomId, ModelRequest Request)> requests, CancellationToken cancellationToken)
        {
            this.Submitted.AddRange(requests);
            return Task.FromResult("batch_1");
        }

        public Task<bool> HasEnded(string batchId, CancellationToken cancellationToken) => Task.FromResult(++this.Polls > pollsBeforeEnd);

        public async IAsyncEnumerable<BatchItem> Results(string batchId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }

    private static BatchItem Ok(string id, string category) =>
        new(id, new ModelReply(FakeGateway.Reply(category: category, outage: category == "outage"), "end_turn", new TokenUsage(100, 20), "claude-haiku-5-5"), null);

    private static Backfill For(FakeBatches batches) => new(batches, "claude-haiku-5-5", TimeSpan.Zero);

    private static readonly (string, string)[] Emails = [("a", "API down."), ("b", "How do I export?"), ("c", "Card declined.")];

    [Test]
    public async Task Results_are_matched_to_emails_by_custom_id_not_by_position()
    {
        var batches = new FakeBatches([Ok("c", "billing"), Ok("a", "outage"), Ok("b", "question")]);

        var result = await For(batches).Run(Emails);

        await Assert.That(result.Extracted["a"].Category).IsEqualTo("outage");
        await Assert.That(result.Extracted["b"].Category).IsEqualTo("question");
        await Assert.That(result.Extracted["c"].Category).IsEqualTo("billing");
        await Assert.That(result.NeedsAnotherPass).IsEmpty();
    }

    [Test]
    public async Task It_waits_until_the_batch_has_ended()
    {
        var batches = new FakeBatches([Ok("a", "outage"), Ok("b", "question"), Ok("c", "billing")], pollsBeforeEnd: 3);

        await For(batches).Run(Emails);

        await Assert.That(batches.Polls).IsEqualTo(4);
    }

    [Test]
    public async Task A_broken_rule_a_refusal_an_error_and_a_missing_result_each_go_to_another_pass()
    {
        var brokeRule = new BatchItem("a", new ModelReply(FakeGateway.Reply(outage: false), "end_turn", new TokenUsage(1, 1), "claude-haiku-5-5"), null);
        var refused = new BatchItem("b", new ModelReply(FakeGateway.WellFormed, "refusal", new TokenUsage(1, 1), "claude-haiku-5-5"), null);
        var errored = new BatchItem("c", null, "errored");
        var batches = new FakeBatches([brokeRule, refused, errored]);

        var result = await For(batches).Run([.. Emails, ("d", "Is the status page right?")]);

        await Assert.That(result.Extracted).IsEmpty();
        await Assert.That(result.NeedsAnotherPass["a"]).IsEqualTo(FailureKind.BrokeRule);
        await Assert.That(result.NeedsAnotherPass["b"]).IsEqualTo(FailureKind.Refused);
        await Assert.That(result.NeedsAnotherPass["c"]).IsEqualTo(FailureKind.NotRun);
        await Assert.That(result.NeedsAnotherPass["d"]).IsEqualTo(FailureKind.NotRun);
    }

    [Test]
    public async Task Each_request_is_the_same_as_the_live_extractor_first_sends()
    {
        // Same system prompt, schema and cache breakpoint, so the batch reads the same cached prefix.
        var batches = new FakeBatches([Ok("a", "outage")]);

        await For(batches).Run([("a", "API down.")]);

        var sent = batches.Submitted.Single().Request;
        var live = TicketExtractor.RequestFor("claude-haiku-5-5", "API down.");
        await Assert.That(sent.System).IsEqualTo(live.System);
        await Assert.That(sent.Turns.Single()).IsEqualTo(live.Turns.Single());
        await Assert.That(sent.Schema).IsSameReferenceAs(live.Schema);
        await Assert.That(sent.CacheSystem).IsTrue();
    }

    [Test]
    [Arguments("has space")]
    [Arguments("")]
    [Arguments("x/y")]
    public async Task A_custom_id_the_api_would_refuse_is_refused_before_anything_is_sent(string id)
    {
        var batches = new FakeBatches([]);

        await Assert.That(async () => await For(batches).Run([(id, "API down.")])).Throws<ArgumentException>();
        await Assert.That(batches.Submitted).IsEmpty();
    }

    [Test]
    public async Task Two_emails_with_one_custom_id_are_refused()
    {
        await Assert.That(async () => await For(new FakeBatches([])).Run([("a", "one"), ("a", "two")])).Throws<ArgumentException>();
    }
}
