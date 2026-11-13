namespace Ticketdesk.Extraction.Tests;

using System.Text.Json;

/// <summary>The labelled tickets are data the eval trusts, so the build checks them like code.</summary>
public sealed class DatasetTests
{
    private static readonly string Path = System.IO.Path.Combine(FindRoot(), "data", "tickets.jsonl");

    [Test]
    public async Task Every_label_is_a_value_the_taxonomy_allows()
    {
        var problems = new List<string>();
        foreach (var (id, expected) in Rows())
        {
            Check(id, "category", expected.GetProperty("category").GetString(), Taxonomy.Categories, problems);
            Check(id, "plan", expected.GetProperty("plan").GetString(), Taxonomy.Plans, problems);
            Check(id, "product_area", expected.GetProperty("product_area").GetString(), Taxonomy.ProductAreas, problems);
            Check(id, "sentiment", expected.GetProperty("sentiment").GetString(), Taxonomy.Sentiments, problems);
        }

        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task Ticket_ids_are_unique()
    {
        var ids = Rows().Select(r => r.Id).ToList();
        await Assert.That(ids.Distinct().Count()).IsEqualTo(ids.Count);
    }

    private static void Check(string id, string field, string? value, IReadOnlyList<string> allowed, List<string> problems)
    {
        if (value is null || !allowed.Contains(value))
        {
            problems.Add($"{id}: {field} '{value}' is not one of {string.Join(", ", allowed)}");
        }
    }

    private static IEnumerable<(string Id, JsonElement Expected)> Rows() =>
        File.ReadLines(Path)
            .Where(l => l.Length > 0)
            .Select(l => JsonDocument.Parse(l).RootElement)
            .Select(r => (r.GetProperty("id").GetString()!, r.GetProperty("expected")));

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Extraction.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find Extraction.slnx above the test binaries.");
    }
}
