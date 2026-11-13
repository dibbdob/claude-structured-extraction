using System.Text.Json;
using Anthropic;
using Ticketdesk.Extraction;

// extract: read one email from standard input and print what was extracted from it.
//   echo "API down since 9am, Enterprise" | dotnet run --project src/Ticketdesk.Extraction.Cli -- extract
// This calls the Claude API and is billed. The build never runs it.

if (args is not ["extract"])
{
    Console.Error.WriteLine("usage: Ticketdesk.Extraction.Cli extract < email.txt");
    return 2;
}

var email = await Console.In.ReadToEndAsync();
var extractor = new TicketExtractor(new AnthropicGateway(new AnthropicClient()));
var result = await extractor.Extract(email);
Console.WriteLine(JsonSerializer.Serialize(result.Extraction, new JsonSerializerOptions { WriteIndented = true }));
Console.Error.WriteLine($"attempts: {result.Attempts}  answered by: {result.AnsweredBy}  tokens in/out: {result.Usage.Input}/{result.Usage.Output}");
return 0;
