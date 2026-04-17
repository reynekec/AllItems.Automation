using System.IO;
using System.Text.Json;

namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IFlowRecentFileService
{
    string? GetLastFlowPath();

    void SetLastFlowPath(string filePath);

    void ClearLastFlowPath();
}

public sealed class FlowRecentFileService : IFlowRecentFileService
{
    private readonly string _stateFilePath;

    public FlowRecentFileService(string? stateFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath))
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllItems.Automation");
            Directory.CreateDirectory(root);
            _stateFilePath = Path.Combine(root, "last-flow.json");
            return;
        }

        _stateFilePath = stateFilePath;
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public string? GetLastFlowPath()
    {
        if (!File.Exists(_stateFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var state = JsonSerializer.Deserialize<FlowRecentFileState>(json);
            return string.IsNullOrWhiteSpace(state?.LastFlowPath)
                ? null
                : state.LastFlowPath;
        }
        catch
        {
            return null;
        }
    }

    public void SetLastFlowPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var state = new FlowRecentFileState
        {
            LastFlowPath = filePath,
        };

        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(_stateFilePath, json);
    }

    public void ClearLastFlowPath()
    {
        if (!File.Exists(_stateFilePath))
        {
            return;
        }

        File.Delete(_stateFilePath);
    }

    private sealed class FlowRecentFileState
    {
        public string? LastFlowPath { get; init; }
    }
}
