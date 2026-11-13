namespace Ticketdesk.Extraction.Tests;

public sealed class ExtractionRulesTests
{
    private static TicketExtraction Valid => new("bug", false, "team", "api", "neutral", false, "The API is slow.");

    [Test]
    public async Task A_valid_extraction_has_no_problems() =>
        await Assert.That(ExtractionRules.Check(Valid)).IsEmpty();

    [Test]
    public async Task An_outage_category_without_an_outage_is_a_problem() =>
        await Assert.That(ExtractionRules.Check(Valid with { Category = "outage" }).Single()).Contains("outage is false");

    [Test]
    public async Task An_outage_under_billing_is_allowed_because_a_suspended_account_is_one() =>
        await Assert.That(ExtractionRules.Check(Valid with { Category = "billing", Outage = true })).IsEmpty();

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task An_empty_summary_is_a_problem(string summary) =>
        await Assert.That(ExtractionRules.Check(Valid with { Summary = summary }).Single()).Contains("empty");

    [Test]
    public async Task A_summary_of_exactly_the_limit_is_allowed_and_one_more_is_not()
    {
        await Assert.That(ExtractionRules.Check(Valid with { Summary = new string('x', Taxonomy.MaxSummaryLength) })).IsEmpty();
        await Assert.That(ExtractionRules.Check(Valid with { Summary = new string('x', Taxonomy.MaxSummaryLength + 1) })).IsNotEmpty();
    }

    [Test]
    public async Task A_value_outside_the_taxonomy_is_caught_even_without_a_schema() =>
        await Assert.That(ExtractionRules.Check(Valid with { Plan = "platinum" }).Single()).Contains("plan 'platinum'");
}

public sealed class GatewayTests
{
    [Test]
    [Arguments("claude-opus-5-5", true)]
    [Arguments("claude-sonnet-5-5", true)]
    [Arguments("claude-haiku-5-5", false)]
    public async Task Only_models_with_server_side_fallback_are_sent_one(string model, bool expected) =>
        await Assert.That(AnthropicGateway.SupportsServerFallback(model)).IsEqualTo(expected);
}
