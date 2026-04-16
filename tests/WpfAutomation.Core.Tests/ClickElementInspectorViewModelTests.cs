using FluentAssertions;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.ViewModels;

namespace WpfAutomation.Core.Tests;

public sealed class ClickElementInspectorViewModelTests
{
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
