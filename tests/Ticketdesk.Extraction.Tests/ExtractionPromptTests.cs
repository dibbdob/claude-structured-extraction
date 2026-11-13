namespace Ticketdesk.Extraction.Tests;

using System.Text.Json;

public sealed class ExtractionPromptTests
{
    [Test]
    public async Task Every_taxonomy_value_has_a_definition_in_the_prompt()
    {
        var values = Taxonomy.Categories.Concat(Taxonomy.Plans).Concat(Taxonomy.ProductAreas).Concat(Taxonomy.Sentiments);
        var undefined = values.Where(v => !ExtractionPrompt.Definitions.ContainsKey(v)).ToList();

        await Assert.That(undefined).IsEmpty();
    }

    [Test]
    public async Task The_system_prompt_carries_nothing_that_varies_between_requests()
    {
        // The cache is a prefix match. A timestamp or an id here would make every request a cache miss.
        var prompt = ExtractionPrompt.SystemPrompt;
        await Assert.That(prompt).DoesNotContain(DateTime.UtcNow.Year.ToString());
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(prompt, "[0-9a-f]{8}-[0-9a-f]{4}")).IsFalse();
    }

    [Test]
    public async Task Every_example_is_a_valid_extraction()
    {
        var problems = ExtractionPrompt.ShownExamples.SelectMany(e => ExtractionRules.Check(e.Answer)).ToList();
        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task No_example_is_a_ticket_the_eval_scores()
    {
        // An example the eval also scores is an answer the model was shown. Compare loosely, so a
        // lightly edited copy is caught too.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Extraction.slnx")))
        {
            root = root.Parent!;
        }

        var tickets = File.ReadLines(Path.Combine(root.FullName, "data", "tickets.jsonl"))
            .Select(l => Normalise(JsonDocument.Parse(l).RootElement.GetProperty("text").GetString()!))
            .ToList();
        var leaked = ExtractionPrompt.ShownExamples.Select(e => Normalise(e.Email))
            .Where(e => tickets.Any(t => t.Contains(e[..Math.Min(40, e.Length)]) || e.Contains(t[..Math.Min(40, t.Length)])))
            .ToList();

        await Assert.That(leaked).IsEmpty();
    }

    private static string Normalise(string text) =>
        new string(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
