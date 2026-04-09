using System.Collections.ObjectModel;
using System.Reflection;
using WpfAutomation.App.Models;
using WpfAutomation.Core.Abstractions.Actions;

namespace WpfAutomation.App.Services;

public sealed class ActionCatalogBuilder : IActionCatalogBuilder
{
    public IReadOnlyList<UiActionCategory> Build(IEnumerable<Assembly> assemblies)
    {
        if (assemblies is null)
        {
            return [];
        }

        var actions = assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .SelectMany(GetActionTypes)
            .Select(CreateAction)
            .Where(action => action is not null)
            .Cast<IAutomationAction>()
            .Select(action => action.Metadata)
            .ToList();

        var categories = actions
            .GroupBy(metadata => new { metadata.CategoryId, metadata.CategoryName })
            .Select(group =>
            {
                var orderedActions = group
                    .OrderBy(metadata => metadata.SortOrder)
                    .ThenBy(metadata => metadata.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new
                {
                    group.Key.CategoryId,
                    group.Key.CategoryName,
                    MinSortOrder = orderedActions.Min(metadata => metadata.SortOrder),
                    Actions = orderedActions,
                };
            })
            .OrderBy(group => group.MinSortOrder)
            .ThenBy(group => group.CategoryName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new UiActionCategory
            {
                CategoryId = group.CategoryId,
                CategoryName = group.CategoryName,
                Actions = new ObservableCollection<UiActionItem>(group.Actions.Select(metadata => new UiActionItem
                {
                    ActionId = metadata.ActionId,
                    DisplayName = metadata.DisplayName,
                    CategoryId = metadata.CategoryId,
                    CategoryName = metadata.CategoryName,
                    IconKeyOrPath = metadata.IconKeyOrPath,
                    Keywords = metadata.Keywords,
                    IsContainer = metadata.IsContainer ?? false,
                })),
            })
            .ToList();

        return categories;
    }

    private static IEnumerable<Type> GetActionTypes(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IAutomationAction).IsAssignableFrom(type));
    }

    private static IAutomationAction? CreateAction(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            return null;
        }

        return Activator.CreateInstance(type) as IAutomationAction;
    }
}