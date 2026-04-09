using WpfAutomation.App.Docking.Layout;

namespace WpfAutomation.App.Docking.Services;

public sealed class InMemoryDockLayoutStateStore : IDockLayoutStateStore
{
    private readonly object _gate = new();
    private DockLayoutSnapshot? _current;

    public DockLayoutSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<DockLayoutSnapshot>? LayoutChanged;

    public ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        lock (_gate)
        {
            _current = snapshot;
        }

        LayoutChanged?.Invoke(this, snapshot);
        return ValueTask.CompletedTask;
    }

    public ValueTask<DockLayoutSnapshot?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return ValueTask.FromResult(_current);
        }
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _current = null;
        }

        return ValueTask.CompletedTask;
    }
}
