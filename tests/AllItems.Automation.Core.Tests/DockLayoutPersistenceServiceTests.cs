using FluentAssertions;
using AllItems.Automation.Browser.App.Docking.Layout;
using AllItems.Automation.Browser.App.Docking.Services;

namespace AllItems.Automation.Core.Tests;

public sealed class DockLayoutPersistenceServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _layoutFilePath;

    public DockLayoutPersistenceServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DockLayoutPersistenceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _layoutFilePath = Path.Combine(_tempDirectory, "dock-layout.json");
    }

    [Fact]
    public async Task SaveAsync_ThenRestoreAsync_RoundTripsSnapshot()
    {
        await using var service = new AsyncDisposableAdapter(new DockLayoutPersistenceService(_layoutFilePath));
        var snapshot = new DockLayoutSnapshot
        {
            Groups =
            [
                new DockLayoutGroupSnapshot
                {
                    GroupId = "center",
                    ActivePanelId = "editor",
                    Panels = [new DockLayoutPanelSnapshot { PanelId = "editor", Title = "Editor", IsPinned = true, TabOrder = 0 }],
                },
                new DockLayoutGroupSnapshot
                {
                    GroupId = "float:solution",
                    ActivePanelId = "solution",
                    Panels = [new DockLayoutPanelSnapshot { PanelId = "solution", Title = "Solution Explorer", IsPinned = true, TabOrder = 0 }],
                },
            ],
            AutoHideItems = [new DockLayoutAutoHideSnapshot { PanelId = "output", Placement = DockLayoutAutoHidePlacement.Bottom, Order = 0 }],
            FloatingHosts = [new DockLayoutFloatingHostSnapshot { HostId = "solution", GroupId = "float:solution", Left = 25, Top = 50, Width = 300, Height = 200 }],
        };

        await service.Inner.SaveAsync(snapshot);
        var restored = await service.Inner.RestoreAsync();

        restored.Should().NotBeNull();
        restored!.Groups.Should().HaveCount(2);
        restored.AutoHideItems.Should().ContainSingle(item => item.PanelId == "output");
        restored.FloatingHosts.Should().ContainSingle(item => item.HostId == "solution");
    }

    [Fact]
    public async Task RestoreAsync_WithInvalidJson_ReturnsDefaultSnapshot()
    {
        await File.WriteAllTextAsync(_layoutFilePath, "{ invalid json");
        await using var service = new AsyncDisposableAdapter(new DockLayoutPersistenceService(_layoutFilePath));

        var restored = await service.Inner.RestoreAsync();

        restored.Should().NotBeNull();
        restored!.SchemaVersion.Should().Be(DockLayoutSnapshot.CurrentSchemaVersion);
        restored.Groups.Should().BeEmpty();
        restored.AutoHideItems.Should().BeEmpty();
        restored.FloatingHosts.Should().BeEmpty();
    }

    [Fact]
    public async Task RestoreAsync_WithFutureSchemaVersion_FallsBackToDefaultSnapshot()
    {
        var futureSnapshot = """
        {
          "schemaVersion": 999,
          "groups": [
            {
              "groupId": "center",
              "activePanelId": "editor",
              "panels": [
                {
                  "panelId": "editor",
                  "title": "Editor",
                  "isPinned": true,
                  "tabOrder": 0
                }
              ]
            }
          ],
          "floatingHosts": [],
          "autoHideItems": [],
          "nodes": []
        }
        """;

        await File.WriteAllTextAsync(_layoutFilePath, futureSnapshot);
        await using var service = new AsyncDisposableAdapter(new DockLayoutPersistenceService(_layoutFilePath));

        var restored = await service.Inner.RestoreAsync();

        restored.Should().NotBeNull();
        restored!.Groups.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private sealed class AsyncDisposableAdapter : IAsyncDisposable
    {
        public AsyncDisposableAdapter(DockLayoutPersistenceService inner)
        {
            Inner = inner;
        }

        public DockLayoutPersistenceService Inner { get; }

        public ValueTask DisposeAsync()
        {
            Inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
