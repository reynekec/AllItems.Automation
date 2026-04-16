using System.Text.Json;
using AllItems.Automation.Browser.Core.Configuration;

namespace AllItems.Automation.Browser.Core.Inspection;

public sealed class InspectionSerializer
{
    private readonly BrowserOptions _options;

    public InspectionSerializer(BrowserOptions options)
    {
        _options = options;
    }

    public async Task<string> ExportJsonAsync(object report, string? filename = null)
    {
        var directory = _options.InspectionExportDirectory
            ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "inspection");

        Directory.CreateDirectory(directory);

        var safeName = string.IsNullOrWhiteSpace(filename)
            ? "inspection"
            : string.Concat(filename.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "inspection";
        }

        var path = Path.Combine(directory, $"{safeName}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, report, report.GetType(), new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        return Path.GetFullPath(path);
    }
}
