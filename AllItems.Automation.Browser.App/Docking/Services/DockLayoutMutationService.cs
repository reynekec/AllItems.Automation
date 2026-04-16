using AllItems.Automation.Browser.App.Docking.Layout;

namespace AllItems.Automation.Browser.App.Docking.Services;

/// <summary>
/// Pure snapshot mutations for dock layout tests and non-visual state transforms.
/// </summary>
public sealed class DockLayoutMutationService
{
    public DockLayoutSnapshot Dock(DockLayoutSnapshot snapshot, DockLayoutPanelSnapshot panel, string targetGroupId, int targetIndex = -1)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(panel);

        var groups = RemovePanel(snapshot.Groups, panel.PanelId).ToList();
        var targetGroup = groups.FirstOrDefault(group => string.Equals(group.GroupId, targetGroupId, StringComparison.Ordinal))
            ?? new DockLayoutGroupSnapshot { GroupId = targetGroupId, Panels = [] };

        groups.RemoveAll(group => string.Equals(group.GroupId, targetGroupId, StringComparison.Ordinal));

        var panels = targetGroup.Panels.ToList();
        if (targetIndex < 0 || targetIndex > panels.Count)
        {
            targetIndex = panels.Count;
        }

        panels.Insert(targetIndex, panel with { IsPinned = true });
        panels = NormalizeOrder(panels);

        groups.Add(targetGroup with
        {
            Panels = panels,
            ActivePanelId = panel.PanelId,
        });

        return snapshot with
        {
            Groups = groups.OrderBy(group => group.GroupId, StringComparer.Ordinal).ToList(),
            AutoHideItems = snapshot.AutoHideItems.Where(item => !string.Equals(item.PanelId, panel.PanelId, StringComparison.Ordinal)).ToList(),
            FloatingHosts = snapshot.FloatingHosts.Where(host => !string.Equals(host.GroupId, $"float:{panel.PanelId}", StringComparison.Ordinal)).ToList(),
        };
    }

    public DockLayoutSnapshot Unpin(DockLayoutSnapshot snapshot, string panelId, DockLayoutAutoHidePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(panelId);

        var groups = snapshot.Groups
            .Select(group => group with
            {
                Panels = group.Panels.Where(panel => !string.Equals(panel.PanelId, panelId, StringComparison.Ordinal)).ToList(),
                ActivePanelId = string.Equals(group.ActivePanelId, panelId, StringComparison.Ordinal)
                    ? group.Panels.FirstOrDefault(panel => !string.Equals(panel.PanelId, panelId, StringComparison.Ordinal))?.PanelId ?? string.Empty
                    : group.ActivePanelId,
            })
            .ToList();

        var autoHide = snapshot.AutoHideItems
            .Where(item => !string.Equals(item.PanelId, panelId, StringComparison.Ordinal))
            .Append(new DockLayoutAutoHideSnapshot
            {
                PanelId = panelId,
                Placement = placement,
                Order = snapshot.AutoHideItems.Count(item => item.Placement == placement),
            })
            .ToList();

        return snapshot with { Groups = groups, AutoHideItems = autoHide };
    }

    public DockLayoutSnapshot Float(DockLayoutSnapshot snapshot, DockLayoutPanelSnapshot panel, DockLayoutFloatingHostSnapshot host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(host);

        var baseSnapshot = Dock(snapshot, panel, host.GroupId);
        var floatingHosts = baseSnapshot.FloatingHosts
            .Where(existing => !string.Equals(existing.HostId, host.HostId, StringComparison.Ordinal))
            .Append(host)
            .ToList();

        return baseSnapshot with { FloatingHosts = floatingHosts };
    }

    public DockLayoutSnapshot Reorder(DockLayoutSnapshot snapshot, string groupId, string panelId, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var groups = snapshot.Groups.Select(group =>
        {
            if (!string.Equals(group.GroupId, groupId, StringComparison.Ordinal))
            {
                return group;
            }

            var panels = group.Panels.ToList();
            var currentIndex = panels.FindIndex(panel => string.Equals(panel.PanelId, panelId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return group;
            }

            var panel = panels[currentIndex];
            panels.RemoveAt(currentIndex);
            if (targetIndex < 0 || targetIndex > panels.Count)
            {
                targetIndex = panels.Count;
            }

            panels.Insert(targetIndex, panel);
            panels = NormalizeOrder(panels);

            return group with { Panels = panels, ActivePanelId = panelId };
        }).ToList();

        return snapshot with { Groups = groups };
    }

    private static IEnumerable<DockLayoutGroupSnapshot> RemovePanel(IEnumerable<DockLayoutGroupSnapshot> groups, string panelId)
    {
        foreach (var group in groups)
        {
            var panels = group.Panels.Where(panel => !string.Equals(panel.PanelId, panelId, StringComparison.Ordinal)).ToList();
            yield return group with
            {
                Panels = NormalizeOrder(panels),
                ActivePanelId = string.Equals(group.ActivePanelId, panelId, StringComparison.Ordinal)
                    ? panels.FirstOrDefault()?.PanelId ?? string.Empty
                    : group.ActivePanelId,
            };
        }
    }

    private static List<DockLayoutPanelSnapshot> NormalizeOrder(IEnumerable<DockLayoutPanelSnapshot> panels)
    {
        return panels
            .Select((panel, index) => panel with { TabOrder = index })
            .ToList();
    }
}
