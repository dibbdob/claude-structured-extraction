using System.Globalization;
using System.Text.Json;
using Anthropic;
using Ticketdesk.Extraction;
using Ticketdesk.Extraction.Batches;
using Ticketdesk.Extraction.Evaluation;

// Both commands call the Claude API and are billed. The build never runs either.
//
//   extract   read one email from standard input and print what was extracted
//   eval      score a model on data/tickets.jsonl; prints a cost estimate and stops unless --yes is given
//   backfill  extract every email in data/tickets.jsonl through the Message Batches API, at half price
//
//   echo "API down since 9am, Enterprise" | dotnet run --project src/Ticketdesk.Extraction.Cli -- extract
//   dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku
//   dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model claude-haiku-5-5 --label haiku --yes
//   dotnet run --project src/Ticketdesk.Extraction.Cli -- eval --model route:haiku-then-opus --label route

var options = Options.Parse(args);
var gateway = new AnthropicGateway(new AnthropicClient());

switch (options.Command)
{
    case "extract":
    {
        var email = await Console.In.ReadToEndAsync();
        var result = await new TicketExtractor(gateway, options.Model).Extract(email);
        Console.WriteLine(JsonSerializer.Serialize(result.Extraction, new JsonSerializerOptions { WriteIndented = true }));
        Console.Error.WriteLine($"attempts: {result.Attempts}  answered by: {result.AnsweredBy}  tokens in/out: {result.Usage.Input}/{result.Usage.Output}");
        return 0;
    }

    case "eval":
    {
        var tickets = LabelledTicket.Load(Path.Combine("data", "tickets.jsonl")).Take(options.Limit).ToList();
        // A route can spend on both models; estimate it as if every ticket reached the stronger one.
        var estimate = options.Model == "route:haiku-then-opus"
            ? Estimate.For("claude-haiku-5-5", tickets) + Estimate.For("claude-opus-5-5", tickets)
            : Estimate.For(options.Model, tickets);
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{tickets.Count} tickets on {options.Model}: about ${estimate:0.00} at list prices as of {Prices.AsOf} (a rough upper estimate; retries and thinking vary)."));
        if (!options.Yes)
        {
            Console.Error.WriteLine("Nothing was sent. Run again with --yes to spend it.");
            return 3;
        }

        IExtractor extractor = options.Model switch
        {
            // The route under test in ADR 0006: Haiku first, Opus when Haiku fails or needs a correction.
            "route:haiku-then-opus" => new RoutedExtractor(
                new TicketExtractor(gateway, "claude-haiku-5-5"), new TicketExtractor(gateway, "claude-opus-5-5")),
            _ => new TicketExtractor(gateway, options.Model),
        };
        var asked = options.Model.StartsWith("route:", StringComparison.Ordinal) ? "claude-haiku-5-5" : options.Model;
        var run = await new Evaluator().Run(options.Label, asked, extractor, tickets);
        var stamp = run.StartedAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        Directory.CreateDirectory(Path.Combine("eval", "runs"));
        Directory.CreateDirectory(Path.Combine("eval", "results"));
        await File.WriteAllTextAsync(Path.Combine("eval", "runs", $"{options.Label}-{stamp}.json"),
            JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true }));
        var report = EvalReport.Markdown(run);
        await File.WriteAllTextAsync(Path.Combine("eval", "results", $"{options.Label}.md"), report);
        Console.WriteLine(report);
        return 0;
    }

    case "backfill":
    {
        var tickets = LabelledTicket.Load(Path.Combine("data", "tickets.jsonl")).Take(options.Limit).ToList();
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{tickets.Count} emails as one batch on {options.Model}: about ${Estimate.For(options.Model, tickets) / 2:0.00} (batch rate, half the live estimate)."));
        if (!options.Yes)
        {
            Console.Error.WriteLine("Nothing was sent. Run again with --yes to spend it.");
            return 3;
        }

        var backfill = new Backfill(new AnthropicBatchGateway(new AnthropicClient()), options.Model, TimeSpan.FromSeconds(30));
        var result = await backfill.Run(tickets.Select(t => (t.Id, t.Text)).ToList());
        Console.WriteLine($"extracted: {result.Extracted.Count}  needs another pass: {result.NeedsAnotherPass.Count}");
        foreach (var (id, kind) in result.NeedsAnotherPass.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {id}: {kind}");
        }

        return 0;
    }

    default:
        Console.Error.WriteLine("usage: extract [--model M] < email.txt | eval --model M --label L [--limit N] [--yes] | backfill --model M [--limit N] [--yes]");
        return 2;
}

internal sealed record Options(string Command, string Model, string Label, int Limit, bool Yes)
{
    public static Options Parse(string[] args)
    {
        string Value(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        var model = Value("--model", "claude-haiku-5-5");
        return new Options(
            args.Length > 0 ? args[0] : string.Empty,
            model,
            Value("--label", model),
            int.Parse(Value("--limit", "1000"), CultureInfo.InvariantCulture),
            args.Contains("--yes"));
    }
}

internal static class Estimate
{
    // Roughly 3.5 characters per token for English text, and a generous allowance for the reply and any
    // thinking at low effort. The first request writes the cache; later ones read it.
    public static decimal For(string model, IReadOnlyList<LabelledTicket> tickets)
    {
        var prompt = ExtractionPrompt.SystemPrompt.Length / 3.5m;
        var price = Prices.PerMillionTokens[model];
        var perTicket = tickets.Select(t => price.Cost(new TokenUsage((long)(t.Text.Length / 3.5m) + 400, 600, (long)prompt))).Sum();
        return perTicket + price.Cost(new TokenUsage(0, 0, 0, (long)prompt));
    }
}
