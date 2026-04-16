using System.Reflection;
using AllItems.Automation.Browser.Actions.Browser;
using FluentAssertions;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace WpfAutomation.Core.Tests;

public sealed class ActionCatalogBuilderTests
{
    private const string ContainerCategoryId = "test-container";

    [Fact]
    public void Build_WithBrowserAssembly_ReturnsExpectedCategories()
    {
        var builder = new ActionCatalogBuilder();

        var categories = builder.Build([typeof(OpenBrowserAction).Assembly]);

        categories.Should().HaveCount(6);
        categories.Should().ContainSingle(category => category.CategoryId == "automation" && category.Actions.Count == 1);
        categories.Should().ContainSingle(category => category.CategoryId == "browser" && category.Actions.Count == 3);
        categories.Should().ContainSingle(category => category.CategoryId == "navigation" && category.Actions.Count == 5);
        categories.Should().ContainSingle(category => category.CategoryId == "elements" && category.Actions.Count == 5);
        categories.Should().ContainSingle(category => category.CategoryId == "assertions" && category.Actions.Count == 4);
        categories.Should().ContainSingle(category => category.CategoryId == "control-flow" && category.Actions.Count == 3);
    }

    [Fact]
    public void Build_WithBrowserAssembly_ContainsRequiredActionMetadataFields()
    {
        var builder = new ActionCatalogBuilder();

        var categories = builder.Build([typeof(OpenBrowserAction).Assembly]);

        categories.SelectMany(category => category.Actions).Should().OnlyContain(action =>
            !string.IsNullOrWhiteSpace(action.ActionId) &&
            !string.IsNullOrWhiteSpace(action.DisplayName) &&
            !string.IsNullOrWhiteSpace(action.CategoryId));
    }

    [Fact]
    public void Build_WithNoAssemblies_ReturnsEmptyList()
    {
        var builder = new ActionCatalogBuilder();

        var categories = builder.Build(Array.Empty<Assembly>());

        categories.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithDuplicateAssemblies_DeduplicatesDiscoveredActions()
    {
        var builder = new ActionCatalogBuilder();
        var browserAssembly = typeof(OpenBrowserAction).Assembly;

        var categories = builder.Build([browserAssembly, browserAssembly]);

        categories.Should().HaveCount(6);
        categories.SelectMany(category => category.Actions).Should().HaveCount(21);
    }

    [Fact]
    public void BrowserAssembly_AutomationActions_HaveUniqueActionIds()
    {
        var actionTypes = typeof(OpenBrowserAction).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IAutomationAction).IsAssignableFrom(type))
            .ToList();

        actionTypes.Should().HaveCount(21);

        var actionIds = actionTypes
            .Select(type => (IAutomationAction)Activator.CreateInstance(type)!)
            .Select(action => action.Metadata.ActionId)
            .ToList();

        actionIds.Should().OnlyContain(actionId => !string.IsNullOrWhiteSpace(actionId));
        actionIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Build_MapsIsContainer_FromActionMetadata()
    {
        var builder = new ActionCatalogBuilder();

        var categories = builder.Build([typeof(ContainerCapableTestAction).Assembly]);

        var containerAction = categories
            .SelectMany(category => category.Actions)
            .Single(action => action.ActionId == "container-action");

        var nonContainerAction = categories
            .SelectMany(category => category.Actions)
            .Single(action => action.ActionId == "regular-action");

        containerAction.IsContainer.Should().BeTrue();
        nonContainerAction.IsContainer.Should().BeFalse();
    }

    public sealed class ContainerCapableTestAction : IAutomationAction
    {
        public ActionMetadata Metadata { get; } = new(
            ActionId: "container-action",
            DisplayName: "Container Action",
            CategoryId: ContainerCategoryId,
            CategoryName: "Container Test",
            IconKeyOrPath: "container-icon",
            Keywords: ["container"],
            SortOrder: 1,
            IsContainer: true);
    }

    public sealed class RegularTestAction : IAutomationAction
    {
        public ActionMetadata Metadata { get; } = new(
            ActionId: "regular-action",
            DisplayName: "Regular Action",
            CategoryId: ContainerCategoryId,
            CategoryName: "Container Test",
            IconKeyOrPath: "regular-icon",
            Keywords: ["regular"],
            SortOrder: 2,
            IsContainer: false);
    }
}
