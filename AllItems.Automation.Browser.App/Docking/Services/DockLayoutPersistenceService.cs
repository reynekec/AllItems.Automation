using System.Text.Json;
using System.IO;
using AllItems.Automation.Browser.App.Docking.Layout;

namespace AllItems.Automation.Browser.App.Docking.Services;

public sealed class DockLayoutPersistenceService : IDockLayoutPersistenceService, IDisposable
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(450);
    private readonly string _layoutFilePath;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly object _gate = new();
    private CancellationTokenSource? _scheduledSaveTokenSource;

    public DockLayoutPersistenceService()
        : this(null)
    {
    }

    public DockLayoutPersistenceService(string? layoutFilePath)
    {
        if (string.IsNullOrWhiteSpace(layoutFilePath))
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AllItems.Automation");
            Directory.CreateDirectory(root);
            _layoutFilePath = Path.Combine(root, "dock-layout.json");
        }
        else
        {
            var directory = Path.GetDirectoryName(layoutFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _layoutFilePath = layoutFilePath;
        }

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }

    public async ValueTask<DockLayoutSnapshot?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_layoutFilePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_layoutFilePath);
            var snapshot = await JsonSerializer.DeserializeAsync<DockLayoutSnapshot>(stream, _serializerOptions, cancellationToken);
            if (snapshot is null)
            {
                return CreateDefaultSnapshot();
            }

            if (snapshot.SchemaVersion > DockLayoutSnapshot.CurrentSchemaVersion)
            {
                return CreateDefaultSnapshot();
            }

            return snapshot;
        }
        catch (JsonException)
        {
            return CreateDefaultSnapshot();
        }
        catch (IOException)
        {
            return CreateDefaultSnapshot();
        }
    }

    public async ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = File.Create(_layoutFilePath);
        await JsonSerializer.SerializeAsync(stream, snapshot with { SchemaVersion = DockLayoutSnapshot.CurrentSchemaVersion }, _serializerOptions, cancellationToken);
    }

    public void ScheduleSave(DockLayoutSnapshot snapshot, TimeSpan? delay = null)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        CancellationTokenSource localTokenSource;
        lock (_gate)
        {
            _scheduledSaveTokenSource?.Cancel();
            _scheduledSaveTokenSource?.Dispose();
            _scheduledSaveTokenSource = new CancellationTokenSource();
            localTokenSource = _scheduledSaveTokenSource;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay ?? DefaultDebounceDelay, localTokenSource.Token);
                await SaveAsync(snapshot, localTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }, localTokenSource.Token);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(_layoutFilePath))
        {
            File.Delete(_layoutFilePath);
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _scheduledSaveTokenSource?.Cancel();
            _scheduledSaveTokenSource?.Dispose();
            _scheduledSaveTokenSource = null;
        }
    }

    private static DockLayoutSnapshot CreateDefaultSnapshot()
    {
        return new DockLayoutSnapshot
        {
            SchemaVersion = DockLayoutSnapshot.CurrentSchemaVersion,
            Groups = [],
            Nodes = [],
            FloatingHosts = [],
            AutoHideItems = [],
        };
    }
}
