using FluentAssertions;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.ViewModels;
using SelectorDemo.Wpf;

namespace AllItems.Automation.Core.Tests;

public sealed class ClickElementInspectorViewModelTests
{
    [Fact]
    public void SingleBrowserTarget_Is_Selected_Automatically()
    {
        var targets = new[]
        {
            new ClickElementBrowserTargetOption("node-1", "Navigate to URL - https://example.com", "https://example.com"),
        };

        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters(),
            new ClickElementActionParameters(),
            _ => { },
            targets);

        viewModel.BrowserTargets.Should().HaveCount(1);
        viewModel.SelectedBrowserTarget.Should().BeSameAs(targets[0]);
        viewModel.SelectedBrowserTargetUrl.Should().Be("https://example.com");
        viewModel.CanOpenBrowserWindow.Should().BeTrue();
    }

    [Fact]
    public void MultipleBrowserTargets_Require_Explicit_Selection()
    {
        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters(),
            new ClickElementActionParameters(),
            _ => { },
            [
                new ClickElementBrowserTargetOption("node-1", "Login page - https://example.com/login", "https://example.com/login"),
                new ClickElementBrowserTargetOption("node-2", "Dashboard - https://example.com/dashboard", "https://example.com/dashboard"),
            ]);

        viewModel.SelectedBrowserTarget.Should().BeNull();
        viewModel.CanOpenBrowserWindow.Should().BeFalse();
        viewModel.BrowserTargetHelpText.Should().Contain("Select which flow navigation node");
    }

    [Fact]
    public void GuidedSelectorInput_Builds_Selector_From_By_Mode()
    {
        ClickElementActionParameters? committed = null;
        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters(),
            new ClickElementActionParameters(),
            parameters => committed = (ClickElementActionParameters)parameters);

        viewModel.SelectorInputValue = "submit-button";

        committed.Should().NotBeNull();
        committed!.Selector.Should().Be("[id=\"submit-button\"]");
        viewModel.SelectorPreview.Should().Be("[id=\"submit-button\"]");

        viewModel.SelectedSelectorMode = "Class";
        viewModel.SelectorInputValue = "cta primary";

        committed.Selector.Should().Be("[class~=\"cta\"][class~=\"primary\"]");
        viewModel.SelectorPreview.Should().Be("[class~=\"cta\"][class~=\"primary\"]");
    }

    [Fact]
    public void ApplySelectedElement_Prefers_Id_And_Fills_Selected_Element_Properties()
    {
        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters(),
            new ClickElementActionParameters(),
            _ => { });

        viewModel.ApplySelectedElement(new BrowserElementSelection(
            "#submit-button",
            "/html/body/button[1]",
            "https://example.com",
            "button",
            "submit-button",
            "submit",
            "primary cta",
            "<button id=\"submit-button\">Submit</button>",
            "id=submit-button",
            "display: block",
            "{}"));

        viewModel.SelectedSelectorMode.Should().Be("Id");
        viewModel.SelectorInputValue.Should().Be("submit-button");
        viewModel.SelectedElementTagName.Should().Be("button");
        viewModel.SelectedElementId.Should().Be("submit-button");
        viewModel.SelectedElementName.Should().Be("submit");
        viewModel.SelectedElementClassName.Should().Be("primary cta");
        viewModel.SelectedElementCssSelector.Should().Be("#submit-button");
        viewModel.SelectedElementXPathSelector.Should().Be("/html/body/button[1]");
        viewModel.SelectedElementFrameUrl.Should().Be("https://example.com");
    }

    [Fact]
    public void ExistingLegacyConfiguration_Is_Parsed_And_Can_Be_Cleared()
    {
        ClickElementActionParameters? committed = null;
        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters("#cta", "iframe#main", true, 5555),
            new ClickElementActionParameters(),
            parameters => committed = (ClickElementActionParameters)parameters);

        viewModel.SelectedSelectorMode.Should().Be("Id");
        viewModel.SelectorInputValue.Should().Be("cta");
        viewModel.TimeoutMs.Should().Be("5555");
        viewModel.HasUnsupportedOptions.Should().BeTrue();

        viewModel.ClearUnsupportedOptionsCommand.Execute(null);

        committed.Should().NotBeNull();
        committed!.Selector.Should().Be("#cta");
        string.IsNullOrWhiteSpace(committed.FrameSelector).Should().BeTrue();
        committed.Force.Should().BeFalse();
        viewModel.HasUnsupportedOptions.Should().BeFalse();
    }

    [Fact]
    public void TimeoutEditor_Uses_Existing_Number_Validation()
    {
        ClickElementActionParameters? committed = null;
        var viewModel = new ClickElementInspectorViewModel(
            new ClickElementActionParameters(),
            new ClickElementActionParameters(),
            parameters => committed = (ClickElementActionParameters)parameters);

        viewModel.SelectorInputValue = "submit-button";
        viewModel.TimeoutMs = "abc";

        viewModel.HasValidationErrors.Should().BeTrue();
        viewModel.ValidationErrors.Should().ContainSingle(error => error.Contains("whole number", StringComparison.Ordinal));
        committed.Should().NotBeNull();
        committed!.TimeoutMs.Should().Be(10000);
    }
}
