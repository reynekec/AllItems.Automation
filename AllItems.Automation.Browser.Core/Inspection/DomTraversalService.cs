using Microsoft.Playwright;
using System.Text.Json;
using AllItems.Automation.Browser.Core.Exceptions;
using AllItems.Automation.Browser.Core.Reports;

namespace AllItems.Automation.Browser.Core.Inspection;

public sealed class DomTraversalService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string LoadScript(string fileName)
    {
        var scriptPath = ResolveScriptPath(fileName);
        return File.ReadAllText(scriptPath);
    }

    public async Task<ElementNodeReport?> InspectElementAsync(IFrame frame, string elementExpression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var script = LoadScript("InspectElement.js");
        var json = await frame.EvaluateAsync<string>($"() => {{ {script}; const node = {elementExpression}; const result = inspectElement(node); return JSON.stringify(result); }}");
        return Deserialize<ElementNodeReport>(json);
    }

    public async Task<ElementNodeReport?> InspectElementAsync(ILocator locator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var script = LoadScript("InspectElement.js");
        var json = await locator.EvaluateAsync<string>($"node => {{ {script}; const result = inspectElement(node); return JSON.stringify(result); }}");
        return Deserialize<ElementNodeReport>(json);
    }

    public async Task<ElementNodeReport?> InspectShadowDomAsync(ILocator locator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var script = LoadScript("InspectShadowDom.js");
        var json = await locator.EvaluateAsync<string>($"node => {{ {script}; const result = inspectShadowDom(node); return JSON.stringify(result); }}");
        return Deserialize<ElementNodeReport>(json);
    }

    public async Task<AccessibilityReport?> GetAccessibilityAsync(ILocator locator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var script = LoadScript("GetAccessibilityData.js");
        var json = await locator.EvaluateAsync<string>($"node => {{ {script}; const result = getAccessibilityData(node); return JSON.stringify(result); }}");
        return Deserialize<AccessibilityReport>(json);
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, "null", StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string ResolveScriptPath(string fileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Inspection", "JavaScript", fileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var repoPath = Path.Combine(current.FullName, "AllItems.Automation.Browser.Core", "Inspection", "JavaScript", fileName);
            if (File.Exists(repoPath))
            {
                return repoPath;
            }

            current = current.Parent;
        }

        throw new InspectionException($"Inspection script '{fileName}' could not be found.", actionName: "ResolveInspectionScript");
    }
}
