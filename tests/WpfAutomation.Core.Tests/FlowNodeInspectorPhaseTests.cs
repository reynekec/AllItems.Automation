using System.Text.Json;
using FluentAssertions;
using WpfAutomation.App.Models;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.NodeInspector.Models;
using WpfAutomation.App.NodeInspector.ViewModels;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.App.ViewModels;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.Core.Tests;

public sealed class FlowNodeInspectorPhaseTests
{
    [Fact]
    public void Resolver_Provides_Defaults_For_All_Current_ActionIds()
    {
        var resolver = new FlowActionParameterResolver();
        foreach (var actionId in ActionIds)
        {
            var descriptor = resolver.Resolve(actionId);
            descriptor.ParameterType.Should().Be(descriptor.DefaultValue.GetType());
        }
    }

    [Fact]
    public void AddActionNode_Initializes_Typed_Default_Parameters()
    {
        var editing = new FlowEditingService(new FlowActionParameterResolver());
        var document = editing.CreateEmptyDocument("init");

        foreach (var actionId in ActionIds)
        {
            document = editing.AddActionNode(document, CreateRequest(actionId), 20, 20);
            var node = document.Nodes.OfType<FlowActionNodeModel>().Last();
            node.ActionParameters.Should().NotBeNull();
            node.ActionParameters.Should().NotBeOfType<UnknownActionParameters>();
        }
    }

    [Fact]
    public async Task Persistence_RoundTrip_Preserves_All_Action_Parameter_Records()
    {
        var resolver = new FlowActionParameterResolver();
        var editing = new FlowEditingService(resolver);
        var persistence = new FlowPersistenceService();
        var tempFile = Path.Combine(Path.GetTempPath(), $"flow-inspector-{Guid.NewGuid():N}.flow.json");

        try
        {
            var document = editing.CreateEmptyDocument("roundtrip");

            foreach (var actionId in ActionIds)
            {
                document = editing.AddActionNode(document, CreateRequest(actionId), 20, 20);
                var nodeId = document.Selection.PrimaryNodeId!;
                var customized = CreateCustomizedParameters(actionId, resolver.Resolve(actionId).DefaultValue);
                document = document.ReplaceActionParameters(nodeId, customized);
            }

            await persistence.SaveAsync(document, tempFile);
            var loaded = await persistence.OpenAsync(tempFile);

            var expected = document.Nodes.OfType<FlowActionNodeModel>().ToDictionary(node => node.NodeId, node => node.ActionParameters);
            var actual = loaded.Nodes.OfType<FlowActionNodeModel>().ToDictionary(node => node.NodeId, node => node.ActionParameters);

            actual.Count.Should().Be(expected.Count);
            foreach (var pair in expected)
            {
                actual[pair.Key].GetType().Should().Be(pair.Value.GetType());
                JsonSerializer.Serialize(actual[pair.Key]).Should().Be(JsonSerializer.Serialize(pair.Value));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void FromSnapshot_Tolerates_Missing_ActionParameterPayload()
    {
        var snapshot = new FlowDocumentSnapshot
        {
            DocumentId = "doc",
            DisplayName = "Missing payload",
            RootLane = new FlowLaneSnapshot
            {
                LaneId = FlowLaneIdentifiers.RootLaneId,
                LaneKind = FlowLaneKind.Root,
                DisplayLabel = "Root",
                NodeIds = ["node-1"],
            },
            Nodes =
            [
                new FlowNodeSnapshot
                {
                    NodeKind = FlowNodeKind.Action,
                    NodeId = "node-1",
                    DisplayLabel = "Navigate",
                    Bounds = new FlowNodeBoundsSnapshot { X = 10, Y = 10, Width = 200, Height = 50 },
                    ActionReference = new FlowActionReferenceSnapshot
                    {
                        ActionId = "navigate-to-url",
                        CategoryId = "navigation",
                        CategoryName = "Navigation",
                    },
                    ActionParameters = null,
                },
            ],
        };

        var document = FlowSnapshotMapper.FromSnapshot(snapshot);
        var actionNode = document.Nodes.OfType<FlowActionNodeModel>().Single();

        actionNode.ActionParameters.Should().BeOfType<NavigateToUrlActionParameters>();
    }

    [Fact]
    public void Inspector_Edit_Propagates_To_Document_And_Undo_Redo()
    {
        var diagnostics = new DiagnosticsService();
        var viewModel = FlowCanvasViewModel.CreateDefault(diagnostics);
        viewModel.HandleDrop(CreateRequest("navigate-to-url"), new System.Windows.Point(10, 10));

        var actionNode = viewModel.Nodes.OfType<FlowActionNodeModel>().Single();
        viewModel.SetSelection([actionNode.NodeId], []);

        var inspector = viewModel.SelectedNodeInspector.InspectorViewModel.Should().BeOfType<NavigateToUrlInspectorViewModel>().Subject;
        var urlField = inspector.Fields.Single(field => string.Equals(field.Name, "Url", StringComparison.Ordinal));
        urlField.StringValue = "https://contoso.example";

        var updated = viewModel.Nodes.OfType<FlowActionNodeModel>().Single();
        updated.ActionParameters.Should().BeOfType<NavigateToUrlActionParameters>();
        ((NavigateToUrlActionParameters)updated.ActionParameters).Url.Should().Be("https://contoso.example");

        viewModel.UndoCommand.Execute(null);
        var undone = viewModel.Nodes.OfType<FlowActionNodeModel>().Single();
        ((NavigateToUrlActionParameters)undone.ActionParameters).Url.Should().NotBe("https://contoso.example");

        viewModel.RedoCommand.Execute(null);
        var redone = viewModel.Nodes.OfType<FlowActionNodeModel>().Single();
        ((NavigateToUrlActionParameters)redone.ActionParameters).Url.Should().Be("https://contoso.example");
    }

    [Fact]
    public void PropertiesPanel_SelectionStates_Are_Intentional_And_Fallback_Is_Deterministic()
    {
        var diagnostics = new DiagnosticsService();
        var viewModel = FlowCanvasViewModel.CreateDefault(diagnostics);

        viewModel.SelectedNodeInspector.DisplayKind.Should().Be(NodeInspectorDisplayKind.NoneSelected);

        viewModel.HandleDrop(CreateRequest("unknown-action-id"), new System.Windows.Point(20, 20));
        var node = viewModel.Nodes.OfType<FlowActionNodeModel>().Single();
        viewModel.SetSelection([node.NodeId], []);

        viewModel.SelectedNodeInspector.DisplayKind.Should().Be(NodeInspectorDisplayKind.ActionInspector);
        viewModel.SelectedNodeInspector.InspectorViewModel.Should().BeOfType<UnknownActionInspectorViewModel>();

        viewModel.SetSelection([], ["edge-1"]);
        viewModel.SelectedNodeInspector.DisplayKind.Should().Be(NodeInspectorDisplayKind.EdgeSelected);
    }

    private static UiActionDragRequest CreateRequest(string actionId)
    {
        return new UiActionDragRequest
        {
            ActionId = actionId,
            ActionName = actionId,
            CategoryId = "test",
            CategoryName = "Test",
            IsContainer = false,
        };
    }

    private static ActionParameters CreateCustomizedParameters(string actionId, ActionParameters defaults)
    {
        return actionId switch
        {
            "open-browser" => new OpenBrowserActionParameters("firefox", false, 1234, 2),
            "new-page" => new NewPageActionParameters("https://example.org", false),
            "close-browser" => new CloseBrowserActionParameters(false),
            "navigate-to-url" => new NavigateToUrlActionParameters("https://example.org/page", 9876, false),
            "go-back" => new GoBackActionParameters(1111),
            "go-forward" => new GoForwardActionParameters(2222),
            "reload-page" => new ReloadPageActionParameters(true, 3333),
            "wait-for-url" => new WaitForUrlActionParameters("/done", 4444, true),
            "click-element" => new ClickElementActionParameters("#cta", "iframe#main", true, 5555),
            "fill-input" => new FillInputActionParameters("#email", "name@example.org", false, 6666),
            "hover-element" => new HoverElementActionParameters("#menu", 7777),
            "press-key" => new PressKeyActionParameters("Control+A", "#input", 8888),
            "select-option" => new SelectOptionActionParameters("#country", "US", 9999),
            "expect-enabled" => new ExpectEnabledActionParameters("#submit", 1112),
            "expect-hidden" => new ExpectHiddenActionParameters("#spinner", 1113),
            "expect-text" => new ExpectTextActionParameters("#message", "Success", true, 1114),
            "expect-visible" => new ExpectVisibleActionParameters("#panel", 1115),
            _ => defaults,
        };
    }

    private static readonly string[] ActionIds =
    [
        "open-browser",
        "new-page",
        "close-browser",
        "navigate-to-url",
        "go-back",
        "go-forward",
        "reload-page",
        "wait-for-url",
        "click-element",
        "fill-input",
        "hover-element",
        "press-key",
        "select-option",
        "expect-enabled",
        "expect-hidden",
        "expect-text",
        "expect-visible",
    ];
}
