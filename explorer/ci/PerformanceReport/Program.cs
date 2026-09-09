using System.Text.Json;
using System.Text.Json.Nodes;
using ExplorerAutomation.CI;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: PerformanceReport <capture-root> <row.json> <true|false> <test|fixture>");
    return 1;
}
var row = JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject();
row["performance"] = PerformanceSummary.Build(args[0], bool.Parse(args[2]), args[3]);
File.WriteAllText(args[1], row.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(row["performance"]!.ToJsonString());
return 0;
