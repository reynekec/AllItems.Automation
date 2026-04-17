using FluentAssertions;
using AllItems.Automation.Browser.App.Docking.Layout;
using AllItems.Automation.Browser.App.Docking.Services;

namespace AllItems.Automation.Core.Tests;

public sealed class DockLayoutMutationServiceTests
{
    private readonly DockLayoutMutationService _service = new();

    [Fact]
    public void Dock_MovesPanelIntoTargetGroup_AndRemovesAutoHideEntry()
    {
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
            ],
            AutoHideItems = [new DockLayoutAutoHideSnapshot { PanelId = "solution", Placement = DockLayoutAutoHidePlacement.Left, Order = 0 }],
        };

        var result = _service.Dock(snapshot, new DockLayoutPanelSnapshot { PanelId = "solution", Title = "Solution Explorer", IsPinned = false }, "left");

        result.Groups.Should().ContainSingle(group => group.GroupId == "left");
        result.Groups.Single(group => group.GroupId == "left").Panels.Should().ContainSingle(panel => panel.PanelId == "solution");
        result.AutoHideItems.Should().NotContain(item => item.PanelId == "solution");
    }

    [Fact]
    public void Unpin_RemovesPanelFromGroup_AndAddsAutoHideEntry()
    {
        var snapshot = new DockLayoutSnapshot
        {
            Groups =
            [
                new DockLayoutGroupSnapshot
                {
                    GroupId = "left",
                    ActivePanelId = "solution",
                    Panels = [new DockLayoutPanelSnapshot { PanelId = "solution", Title = "Solution Explorer", IsPinned = true, TabOrder = 0 }],
                },
            ],
        };

        var result = _service.Unpin(snapshot, "solution", DockLayoutAutoHidePlacement.Left);

        result.Groups.Single(group => group.GroupId == "left").Panels.Should().BeEmpty();
        result.AutoHideItems.Should().ContainSingle(item => item.PanelId == "solution" && item.Placement == DockLayoutAutoHidePlacement.Left);
    }

    [Fact]
    public void Float_CreatesFloatingHostAndFloatingGroup()
    {
        var snapshot = new DockLayoutSnapshot
        {
            Groups = [new DockLayoutGroupSnapshot { GroupId = "center", ActivePanelId = "editor", Panels = [] }],
        };

        var host = new DockLayoutFloatingHostSnapshot
        {
            HostId = "solution",
            GroupId = "float:solution",
            Left = 10,
            Top = 20,
            Width = 300,
            Height = 200,
        };

        var result = _service.Float(snapshot, new DockLayoutPanelSnapshot { PanelId = "solution", Title = "Solution Explorer", IsPinned = true }, host);

        result.FloatingHosts.Should().ContainSingle(item => item.HostId == "solution");
        result.Groups.Should().ContainSingle(group => group.GroupId == "float:solution");
    }

    [Fact]
    public void Reorder_UpdatesTabOrderAndActivePanel()
    {
        var snapshot = new DockLayoutSnapshot
        {
            Groups =
            [
                new DockLayoutGroupSnapshot
                {
                    GroupId = "center",
                    ActivePanelId = "a",
                    Panels =
                    [
                        new DockLayoutPanelSnapshot { PanelId = "a", Title = "A", IsPinned = true, TabOrder = 0 },
                        new DockLayoutPanelSnapshot { PanelId = "b", Title = "B", IsPinned = true, TabOrder = 1 },
                        new DockLayoutPanelSnapshot { PanelId = "c", Title = "C", IsPinned = true, TabOrder = 2 },
                    ],
                },
            ],
        };

        var result = _service.Reorder(snapshot, "center", "a", 2);
        var panels = result.Groups.Single().Panels;

        panels.Select(panel => panel.PanelId).Should().ContainInOrder("b", "c", "a");
        panels.Select(panel => panel.TabOrder).Should().ContainInOrder(0, 1, 2);
        result.Groups.Single().ActivePanelId.Should().Be("a");
    }
}
