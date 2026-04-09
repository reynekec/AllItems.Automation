using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfAutomation.App.Commands;
using WpfAutomation.App.Models;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.NodeInspector.Contracts;
using WpfAutomation.App.NodeInspector.Models;
using WpfAutomation.App.NodeInspector.Services;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.Core.Diagnostics;

namespace WpfAutomation.App.ViewModels;

public sealed class FlowCanvasViewModel : INotifyPropertyChanged
{
    private const string DefaultDocumentDisplayName = "Canvas Flow";
    private const long DuplicateDropWindowMilliseconds = 250;
    private const double AutoDropVerticalGap = 28;
    private const double ActionNodeDefaultWidth = 380;
    private const double ActionNodeDefaultHeight = 50;
    private const double ContainerNodeDefaultWidth = 420;
    private const double ContainerNodeDefaultHeight = 150;
    private const int AutoDropMaxCollisionPasses = 512;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly IFlowEditingService _editingService;
    private readonly IFlowPersistenceService _persistenceService;
    private readonly IFlowEdgeRoutingService _routingService;
    private readonly IFlowHitTestService _hitTestService;
    private readonly IFlowLayoutService _layoutService;
    private readonly IFlowDocumentMapper<ExecutionFlowGraph> _executionMapper;
    private readonly INodeInspectorFactory _nodeInspectorFactory;
    private readonly Stack<FlowDocumentModel> _undoStack = [];
    private readonly Stack<FlowDocumentModel> _redoStack = [];

    private FlowDocumentModel _document;
    private FlowInteractionState _interactionState = new();
    private FlowClipboardModel _clipboard = new();
    private SelectedNodeInspectorState _selectedNodeInspector = SelectedNodeInspectorState.CreateNone();
    private INodeInspectorViewModel? _activeInspectorViewModel;
    private bool? _lastValidationState;
    private DropSnapshot? _lastDropSnapshot;
    private string? _lastCreatedNodeId;
    private string? _currentDocumentPath;

    public FlowCanvasViewModel(
        DiagnosticsService diagnosticsService,
        IFlowEditingService editingService,
        IFlowPersistenceService persistenceService,
        IFlowEdgeRoutingService routingService,
        IFlowHitTestService hitTestService,
        IFlowLayoutService layoutService,
        IFlowDocumentMapper<ExecutionFlowGraph> executionMapper,
        INodeInspectorFactory nodeInspectorFactory)
    {
        _diagnosticsService = diagnosticsService;
        _editingService = editingService;
        _persistenceService = persistenceService;
        _routingService = routingService;
        _hitTestService = hitTestService;
        _layoutService = layoutService;
        _executionMapper = executionMapper;
        _nodeInspectorFactory = nodeInspectorFactory;

        _document = CreateEmptyDocument();
        EdgeVisuals = [];
        RefreshEdgeVisuals();

        NewCommand = new RelayCommand(NewProject);
        ToggleCollapseCommand = new RelayCommand(ToggleCollapse, parameter => parameter is string);
        DeleteSelectionCommand = new RelayCommand(DeleteSelection, _ => HasSelection);
        CopySelectionCommand = new RelayCommand(CopySelection, _ => HasSelection);
        PasteSelectionCommand = new RelayCommand(PasteSelection, _ => _clipboard.Nodes.Count > 0);
        UndoCommand = new RelayCommand(Undo, _ => _undoStack.Count > 0);
        RedoCommand = new RelayCommand(Redo, _ => _redoStack.Count > 0);
        AutoLayoutCommand = new RelayCommand(AutoLayout);
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), () => HasOpenedFile);
        SaveAsCommand = new AsyncRelayCommand(_ => SaveAsAsync(), () => true);
        OpenCommand = new AsyncRelayCommand(_ => OpenAsync(), () => true);
        ValidateForRunCommand = new RelayCommand(ValidateForRun);
        UpdateSelectedNodeInspectorState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FlowDocumentModel Document
    {
        get => _document;
        private set
        {
            if (ReferenceEquals(_document, value))
            {
                return;
            }

            _document = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Nodes));
            OnPropertyChanged(nameof(RootNodes));
            OnPropertyChanged(nameof(Edges));
            OnPropertyChanged(nameof(HasSelection));
            RefreshEdgeVisuals();
            UpdateSelectedNodeInspectorState();
            RaiseCommandStateChanged();
        }
    }

    public IReadOnlyList<FlowNodeModel> Nodes
    {
        get
        {
            var containerById = Document.Nodes
                .OfType<FlowContainerNodeModel>()
                .ToDictionary(container => container.NodeId, StringComparer.Ordinal);

            var collapsedContainerIds = containerById
                .Where(entry => entry.Value.IsCollapsed)
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);

            if (collapsedContainerIds.Count == 0)
            {
                return Document.Nodes;
            }

            return Document.Nodes
                .Where(node => IsNodeVisible(node, containerById, collapsedContainerIds))
                .ToList();
        }
    }

    public IReadOnlyList<FlowNodeModel> RootNodes => Document.Nodes
        .Where(node => string.IsNullOrWhiteSpace(node.ParentContainerNodeId))
        .ToList();

    public IReadOnlyList<FlowEdgeModel> Edges => Document.Edges;

    public ObservableCollection<FlowEdgeVisualModel> EdgeVisuals { get; }

    public FlowInteractionState InteractionState
    {
        get => _interactionState;
        private set
        {
            _interactionState = value;
            OnPropertyChanged();
        }
    }

    public SelectedNodeInspectorState SelectedNodeInspector
    {
        get => _selectedNodeInspector;
        private set
        {
            if (ReferenceEquals(_selectedNodeInspector, value))
            {
                return;
            }

            if (!Equals(_selectedNodeInspector, value))
            {
                _selectedNodeInspector = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasSelection => Document.Selection.SelectedNodeIds.Count > 0 || Document.Selection.SelectedEdgeIds.Count > 0;

    public ICommand NewCommand { get; }

    public ICommand ToggleCollapseCommand { get; }

    public ICommand DeleteSelectionCommand { get; }

    public ICommand CopySelectionCommand { get; }

    public ICommand PasteSelectionCommand { get; }

    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public ICommand AutoLayoutCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand SaveAsCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand ValidateForRunCommand { get; }

    public bool HasOpenedFile => !string.IsNullOrWhiteSpace(_currentDocumentPath);

    public ExecutionFlowGraph CreateExecutionGraph()
    {
        return _executionMapper.Map(Document);
    }

    public void HandleDrop(UiActionDragRequest request, Point dropPoint, FlowDropContextModel? dropContext = null)
    {
        var resolvedContext = dropContext ?? FlowDropContextModel.CreateRoot(dropPoint, InteractionState.HoveredEdgeId);

        if (ShouldIgnoreDuplicateDrop(request, dropPoint, resolvedContext))
        {
            return;
        }

        var resolvedDropPoint = ResolveDropPoint(request, resolvedContext);

        ApplyMutation(
            document => _editingService.AddActionNode(document, request, resolvedDropPoint.X, resolvedDropPoint.Y, resolvedContext.TargetEdgeId, resolvedContext),
            $"Added action node '{request.ActionName}'.");

        _lastCreatedNodeId = Document.Selection.PrimaryNodeId;

        if (!string.IsNullOrWhiteSpace(resolvedContext.TargetLaneId) &&
            !string.Equals(resolvedContext.TargetLaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            _diagnosticsService.Info(
                "Container-targeted drop resolved.",
                new Dictionary<string, string>
                {
                    ["targetContainerNodeId"] = resolvedContext.TargetContainerNodeId ?? string.Empty,
                    ["targetLaneId"] = resolvedContext.TargetLaneId ?? string.Empty,
                    ["targetEdgeId"] = resolvedContext.TargetEdgeId ?? string.Empty,
                });
        }

        ClearHoverState();
        _lastDropSnapshot = DropSnapshot.Create(request, dropPoint, resolvedContext);
    }

    public void SetSelection(IReadOnlyList<string> nodeIds, IReadOnlyList<string> edgeIds)
    {
        var nodeList = nodeIds.Distinct(StringComparer.Ordinal).ToList();
        var edgeList = edgeIds.Distinct(StringComparer.Ordinal).ToList();

        Document = Document with
        {
            Selection = new FlowSelectionModel
            {
                PrimaryNodeId = nodeList.FirstOrDefault(),
                PrimaryEdgeId = edgeList.FirstOrDefault(),
                SelectedNodeIds = nodeList,
                SelectedEdgeIds = edgeList,
            },
        };

        InteractionState = InteractionState with
        {
            SelectedNodeIds = nodeList,
            SelectedEdgeIds = edgeList,
        };
    }

    public void MoveSelectionToLane(string laneId, int insertIndex)
    {
        if (Document.Selection.SelectedNodeIds.Count == 0)
        {
            return;
        }

        ApplyMutation(
            document => _editingService.MoveNodesToLane(document, document.Selection.SelectedNodeIds, laneId, insertIndex),
            "Moved selected nodes.");
    }

    public void TranslateSelection(double deltaX, double deltaY)
    {
        if (Document.Selection.SelectedNodeIds.Count == 0)
        {
            return;
        }

        var translated = _editingService.TranslateNodes(Document, Document.Selection.SelectedNodeIds, deltaX, deltaY);
        Document = _layoutService.Recalculate(translated);
    }

    public void UpdateEdgeHover(Point pointer)
    {
        var edgeId = _hitTestService.HitTestEdge(EdgeVisuals, pointer, tolerance: 20);
        if (edgeId is null)
        {
            if (InteractionState.IsDropInsertPreviewVisible)
            {
                ClearHoverState();
            }

            return;
        }

        var edge = EdgeVisuals.First(e => e.EdgeId == edgeId);
        InteractionState = InteractionState with
        {
            HoveredEdgeId = edgeId,
            HoverIndicatorX = edge.MidpointX,
            HoverIndicatorY = edge.MidpointY,
            IsDropInsertPreviewVisible = true,
        };
    }

    public string? ResolveDropEdge(Point pointer, string? laneId = null)
    {
        IReadOnlyList<FlowEdgeVisualModel> candidates = EdgeVisuals;

        if (!string.IsNullOrWhiteSpace(laneId))
        {
            var laneEdgeIds = Document.Edges
                .Where(edge => edge.LaneMetadata is not null &&
                               (string.Equals(edge.LaneMetadata.SourceLaneId, laneId, StringComparison.Ordinal) ||
                                string.Equals(edge.LaneMetadata.TargetLaneId, laneId, StringComparison.Ordinal)))
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.Ordinal);

            if (laneEdgeIds.Count == 0)
            {
                return null;
            }

            candidates = EdgeVisuals
                .Where(edge => laneEdgeIds.Contains(edge.EdgeId))
                .ToList();
        }

        return _hitTestService.HitTestEdge(candidates, pointer, tolerance: 20);
    }

    public Point? ResolveEdgeMidpoint(string edgeId)
    {
        var edge = EdgeVisuals.FirstOrDefault(candidate => string.Equals(candidate.EdgeId, edgeId, StringComparison.Ordinal));
        if (edge is null)
        {
            return null;
        }

        return new Point(edge.MidpointX, edge.MidpointY);
    }

    public void SetDropPreview(Point pointer, string? edgeId = null, string? laneId = null)
    {
        if (!string.IsNullOrWhiteSpace(edgeId))
        {
            var edgeMidpoint = ResolveEdgeMidpoint(edgeId);
            if (edgeMidpoint is not null)
            {
                InteractionState = InteractionState with
                {
                    HoveredEdgeId = edgeId,
                    HoveredLaneId = laneId,
                    HoverIndicatorX = edgeMidpoint.Value.X,
                    HoverIndicatorY = edgeMidpoint.Value.Y,
                    IsDropInsertPreviewVisible = true,
                };

                return;
            }
        }

        InteractionState = InteractionState with
        {
            HoveredEdgeId = null,
            HoveredLaneId = laneId,
            HoverIndicatorX = pointer.X,
            HoverIndicatorY = pointer.Y,
            IsDropInsertPreviewVisible = true,
        };
    }

    public void SetLaneHover(string? laneId)
    {
        if (string.Equals(InteractionState.HoveredLaneId, laneId, StringComparison.Ordinal))
        {
            return;
        }

        InteractionState = InteractionState with
        {
            HoveredLaneId = laneId,
        };
    }

    public void ClearDropPreview()
    {
        ClearHoverState();
    }

    private FlowDocumentModel CreateEmptyDocument()
    {
        return _editingService.CreateEmptyDocument(DefaultDocumentDisplayName);
    }

    private void NewProject(object? _)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _clipboard = new FlowClipboardModel();
        _lastDropSnapshot = null;
        _lastCreatedNodeId = null;

        SetCurrentDocumentPath(null);
        Document = CreateEmptyDocument();
        InteractionState = new FlowInteractionState();

        _diagnosticsService.Info("Started a new empty project.");
    }

    private void ToggleCollapse(object? parameter)
    {
        if (parameter is not string nodeId)
        {
            return;
        }

        ApplyMutation(document =>
        {
            var nodes = document.Nodes.Select(node =>
            {
                if (node is not FlowContainerNodeModel container || !string.Equals(container.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return node;
                }

                return (FlowNodeModel)(container with
                {
                    IsCollapsed = !container.IsCollapsed,
                    IsExpanded = container.IsCollapsed,
                });
            }).ToList();

            return document with { Nodes = nodes };
        }, "Toggled container collapse state.");
    }

    private void DeleteSelection(object? _)
    {
        ApplyMutation(
            document => _editingService.DeleteSelection(document, document.Selection.SelectedNodeIds, document.Selection.SelectedEdgeIds),
            "Deleted selected graph items.");
    }

    private void CopySelection(object? _)
    {
        _clipboard = _editingService.CopySelection(Document, Document.Selection.SelectedNodeIds, Document.Selection.SelectedEdgeIds);
        _diagnosticsService.Info($"Copied {_clipboard.Nodes.Count} node(s) and {_clipboard.Edges.Count} edge(s).");
        RaiseCommandStateChanged();
    }

    private void PasteSelection(object? _)
    {
        ApplyMutation(document => _editingService.PasteSelection(document, _clipboard, 28, 28), "Pasted clipboard selection.");
    }

    private void Undo(object? _)
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Push(Document);
        Document = _undoStack.Pop();
        _diagnosticsService.Info("Undo applied.");
    }

    private void Redo(object? _)
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Push(Document);
        Document = _redoStack.Pop();
        _diagnosticsService.Info("Redo applied.");
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }

        var validation = FlowDocumentValidator.Validate(Document);
        if (!validation.IsValid)
        {
            _diagnosticsService.Warn("Flow save blocked because validation failed.", new Dictionary<string, string> { ["errors"] = string.Join(" | ", validation.Errors) });
            return;
        }

        try
        {
            await _persistenceService.SaveAsync(Document, _currentDocumentPath);
            _diagnosticsService.Info($"Saved flow document to '{_currentDocumentPath}'.");
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Node inspector persist failure while saving flow document.", exception, BuildInspectorContextData());
        }
    }

    private async Task SaveAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Flow JSON (*.flow.json)|*.flow.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            AddExtension = true,
            DefaultExt = ".flow.json",
            FileName = string.IsNullOrWhiteSpace(Document.DisplayName) ? "automation-flow" : Document.DisplayName,
        };

        if (!await ShowDialogWithWaitCursorAsync(dialog))
        {
            return;
        }

        var validation = FlowDocumentValidator.Validate(Document);
        if (!validation.IsValid)
        {
            _diagnosticsService.Warn("Flow save blocked because validation failed.", new Dictionary<string, string> { ["errors"] = string.Join(" | ", validation.Errors) });
            return;
        }

        try
        {
            await _persistenceService.SaveAsync(Document, dialog.FileName);
            SetCurrentDocumentPath(dialog.FileName);
            _diagnosticsService.Info($"Saved flow document to '{dialog.FileName}'.");
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Node inspector persist failure while saving flow document.", exception, BuildInspectorContextData());
        }
    }

    private async Task OpenAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Flow JSON (*.flow.json)|*.flow.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Multiselect = false,
        };

        if (!await ShowDialogWithWaitCursorAsync(dialog))
        {
            return;
        }

        try
        {
            var loaded = await _persistenceService.OpenAsync(dialog.FileName);
            var validation = FlowDocumentValidator.Validate(loaded);
            if (!validation.IsValid)
            {
                _diagnosticsService.Warn("Loaded flow has validation warnings.", new Dictionary<string, string> { ["errors"] = string.Join(" | ", validation.Errors) });
            }

            _undoStack.Push(Document);
            _redoStack.Clear();
            Document = loaded;
            SetCurrentDocumentPath(dialog.FileName);
            _diagnosticsService.Info($"Opened flow document from '{dialog.FileName}'.");
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error($"Failed to open flow document '{dialog.FileName}'.", exception);
        }
    }

    private static async Task<bool> ShowDialogWithWaitCursorAsync(CommonDialog dialog)
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var tcs = new TaskCompletionSource<bool>();

        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);

#pragma warning disable CS4014
            dispatcher.BeginInvoke(
                () =>
                {
                    try
                    {
                        var result = dialog.ShowDialog() == true;
                        tcs.SetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                },
                DispatcherPriority.Normal);
#pragma warning restore CS4014

            return await tcs.Task;
        }
        catch
        {
            Mouse.OverrideCursor = null;
            throw;
        }
    }

    private void AutoLayout(object? _)
    {
        ApplyMutation(
            document => _layoutService.AutoLayout(document),
            "Auto layout applied to all nodes.");
    }

    private void ValidateForRun(object? _)
    {
        try
        {
            var graph = CreateExecutionGraph();
            _diagnosticsService.Info($"Flow runtime mapping ready. Nodes={graph.Nodes.Count}, Edges={graph.Edges.Count}.");
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Flow runtime mapping failed.", exception);
        }
    }

    private void ApplyMutation(Func<FlowDocumentModel, FlowDocumentModel> mutation, string logMessage)
    {
        var next = mutation(Document);
        next = _layoutService.Recalculate(next);

        var validation = FlowDocumentValidator.Validate(next);
        if (!validation.IsValid)
        {
            _diagnosticsService.Warn("Flow mutation rejected by validation.", new Dictionary<string, string> { ["errors"] = string.Join(" | ", validation.Errors) });
            return;
        }

        _undoStack.Push(Document);
        _redoStack.Clear();
        Document = next;
        _diagnosticsService.Info(logMessage);
    }

    private void RefreshEdgeVisuals()
    {
        EdgeVisuals.Clear();

        var nodeLookup = Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        foreach (var edge in Document.Edges)
        {
            if (!nodeLookup.TryGetValue(edge.FromNodeId, out var fromNode) || !nodeLookup.TryGetValue(edge.ToNodeId, out var toNode))
            {
                continue;
            }

            var route = _routingService.BuildRoute(fromNode.Bounds, toNode.Bounds);
            if (route.Count == 0)
            {
                continue;
            }

            var pathSegments = string.Join(" ", route.Select((point, index) =>
                FormattableString.Invariant($"{(index == 0 ? 'M' : 'L')} {point.X:0.##},{point.Y:0.##}")));
            var start = route[0];
            var end = route[^1];
            var midpoint = new Point((start.X + end.X) / 2d, (start.Y + end.Y) / 2d);

            EdgeVisuals.Add(new FlowEdgeVisualModel
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                PathData = pathSegments,
                RoutePoints = route,
                MidpointX = midpoint.X,
                MidpointY = midpoint.Y,
            });
        }
    }

    private static bool IsNodeVisible(
        FlowNodeModel node,
        IReadOnlyDictionary<string, FlowContainerNodeModel> containerById,
        IReadOnlySet<string> collapsedContainerIds)
    {
        var currentParentId = node.ParentContainerNodeId;

        while (!string.IsNullOrWhiteSpace(currentParentId))
        {
            if (collapsedContainerIds.Contains(currentParentId))
            {
                return false;
            }

            if (!containerById.TryGetValue(currentParentId, out var parentContainer))
            {
                break;
            }

            currentParentId = parentContainer.ParentContainerNodeId;
        }

        return true;
    }

    private void UpdateSelectedNodeInspectorState()
    {
        SelectedNodeInspector = ResolveSelectedNodeInspectorState();
        AttachInspectorValidationListener(SelectedNodeInspector.InspectorViewModel);
    }

    private SelectedNodeInspectorState ResolveSelectedNodeInspectorState()
    {
        var selection = Document.Selection;

        if (!string.IsNullOrWhiteSpace(selection.PrimaryNodeId))
        {
            var selectedNode = Document.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selection.PrimaryNodeId, StringComparison.Ordinal));
            if (selectedNode is null)
            {
                _diagnosticsService.Warn("Inspector lifecycle: selected node was not found.", new Dictionary<string, string> { ["nodeId"] = selection.PrimaryNodeId! });
                return SelectedNodeInspectorState.CreateNone();
            }

            if (selectedNode is FlowContainerNodeModel containerNode)
            {
                _diagnosticsService.Info("Inspector lifecycle: container node selected.", new Dictionary<string, string> { ["nodeId"] = containerNode.NodeId, ["kind"] = containerNode.ContainerKind.ToString() });
                var descriptor = _nodeInspectorFactory.CreateContainerDescriptor(containerNode);
                var inspector = _nodeInspectorFactory.CreateContainerInspector(containerNode, parameters => CommitContainerParameters(containerNode.NodeId, parameters));
                _diagnosticsService.Info("Inspector lifecycle: container inspector resolved.", new Dictionary<string, string> { ["nodeId"] = containerNode.NodeId, ["kind"] = containerNode.ContainerKind.ToString(), ["inspectorTitle"] = inspector.Title });
                return SelectedNodeInspectorState.CreateActionInspector(containerNode.NodeId, descriptor, inspector);
            }

            if (selectedNode is FlowActionNodeModel actionNode)
            {
                _diagnosticsService.Info("Inspector lifecycle: action node selected.", new Dictionary<string, string> { ["nodeId"] = actionNode.NodeId, ["actionId"] = actionNode.ActionReference.ActionId });
                var descriptor = _nodeInspectorFactory.CreateDescriptor(actionNode);
                var inspector = _nodeInspectorFactory.CreateInspector(actionNode, parameters => CommitActionParameters(actionNode.NodeId, parameters));

                _diagnosticsService.Info("Inspector lifecycle: inspector resolved.", new Dictionary<string, string> { ["nodeId"] = actionNode.NodeId, ["actionId"] = actionNode.ActionReference.ActionId, ["inspectorTitle"] = inspector.Title });
                return SelectedNodeInspectorState.CreateActionInspector(actionNode.NodeId, descriptor, inspector);
            }

            return SelectedNodeInspectorState.CreateNone();
        }

        if (!string.IsNullOrWhiteSpace(selection.PrimaryEdgeId) || selection.SelectedEdgeIds.Count > 0)
        {
            var edgeId = selection.PrimaryEdgeId ?? selection.SelectedEdgeIds[0];
            _diagnosticsService.Info("Inspector lifecycle: edge selected.", new Dictionary<string, string> { ["edgeId"] = edgeId });
            return SelectedNodeInspectorState.CreateEdgeSelected(edgeId);
        }

        return SelectedNodeInspectorState.CreateNone();
    }

    private void AttachInspectorValidationListener(INodeInspectorViewModel? inspector)
    {
        if (_activeInspectorViewModel is not null)
        {
            _activeInspectorViewModel.PropertyChanged -= OnInspectorPropertyChanged;
        }

        _activeInspectorViewModel = inspector;
        _lastValidationState = inspector?.HasValidationErrors;

        if (_activeInspectorViewModel is null)
        {
            return;
        }

        _activeInspectorViewModel.PropertyChanged += OnInspectorPropertyChanged;
        _diagnosticsService.Info(
            "Inspector lifecycle: validation state changed.",
            new Dictionary<string, string>
            {
                ["nodeId"] = SelectedNodeInspector.NodeId ?? string.Empty,
                ["hasErrors"] = _activeInspectorViewModel.HasValidationErrors.ToString(),
            });
    }

    private void OnInspectorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not INodeInspectorViewModel inspector)
        {
            return;
        }

        if (!string.Equals(eventArgs.PropertyName, nameof(INodeInspectorViewModel.HasValidationErrors), StringComparison.Ordinal))
        {
            return;
        }

        if (_lastValidationState == inspector.HasValidationErrors)
        {
            return;
        }

        _lastValidationState = inspector.HasValidationErrors;
        _diagnosticsService.Info(
            "Inspector lifecycle: validation state changed.",
            new Dictionary<string, string>
            {
                ["nodeId"] = SelectedNodeInspector.NodeId ?? string.Empty,
                ["hasErrors"] = inspector.HasValidationErrors.ToString(),
            });
    }

    private Dictionary<string, string> BuildInspectorContextData()
    {
        return new Dictionary<string, string>
        {
            ["displayKind"] = SelectedNodeInspector.DisplayKind.ToString(),
            ["nodeId"] = SelectedNodeInspector.NodeId ?? string.Empty,
            ["actionId"] = SelectedNodeInspector.Descriptor?.ActionId ?? string.Empty,
        };
    }

    private void CommitActionParameters(string nodeId, ActionParameters parameters)
    {
        ApplyMutation(
            document => document.ReplaceActionParameters(nodeId, parameters),
            $"Updated parameters for node '{nodeId}'.");
    }

    private void CommitContainerParameters(string nodeId, ContainerParameters parameters)
    {
        ApplyMutation(
            document => document.ReplaceContainerParameters(nodeId, parameters),
            $"Updated container parameters for node '{nodeId}'.");
    }

    private void ClearHoverState()
    {
        InteractionState = InteractionState with
        {
            HoveredEdgeId = null,
            HoveredLaneId = null,
            IsDropInsertPreviewVisible = false,
        };
    }

    private void RaiseCommandStateChanged()
    {
        if (SaveCommand is AsyncRelayCommand save)
        {
            save.RaiseCanExecuteChanged();
        }

        if (DeleteSelectionCommand is RelayCommand delete)
        {
            delete.RaiseCanExecuteChanged();
        }

        if (CopySelectionCommand is RelayCommand copy)
        {
            copy.RaiseCanExecuteChanged();
        }

        if (PasteSelectionCommand is RelayCommand paste)
        {
            paste.RaiseCanExecuteChanged();
        }

        if (UndoCommand is RelayCommand undo)
        {
            undo.RaiseCanExecuteChanged();
        }

        if (RedoCommand is RelayCommand redo)
        {
            redo.RaiseCanExecuteChanged();
        }
    }

    private void SetCurrentDocumentPath(string? filePath)
    {
        if (string.Equals(_currentDocumentPath, filePath, StringComparison.Ordinal))
        {
            return;
        }

        _currentDocumentPath = filePath;
        OnPropertyChanged(nameof(HasOpenedFile));

        if (SaveCommand is AsyncRelayCommand save)
        {
            save.RaiseCanExecuteChanged();
        }
    }

    private bool ShouldIgnoreDuplicateDrop(UiActionDragRequest request, Point dropPoint, FlowDropContextModel context)
    {
        if (_lastDropSnapshot is null)
        {
            return false;
        }

        var elapsed = Environment.TickCount64 - _lastDropSnapshot.Timestamp;
        if (elapsed > DuplicateDropWindowMilliseconds)
        {
            return false;
        }

        return string.Equals(_lastDropSnapshot.ActionId, request.ActionId, StringComparison.Ordinal) &&
               string.Equals(_lastDropSnapshot.CategoryId, request.CategoryId, StringComparison.Ordinal) &&
                             _lastDropSnapshot.IsContainer == request.IsContainer &&
                             string.Equals(_lastDropSnapshot.TargetLaneId, context.TargetLaneId, StringComparison.Ordinal) &&
                             string.Equals(_lastDropSnapshot.TargetEdgeId, context.TargetEdgeId, StringComparison.Ordinal) &&
                             string.Equals(_lastDropSnapshot.TargetContainerNodeId, context.TargetContainerNodeId, StringComparison.Ordinal) &&
               Math.Abs(_lastDropSnapshot.X - dropPoint.X) < 0.5 &&
               Math.Abs(_lastDropSnapshot.Y - dropPoint.Y) < 0.5;
    }

    private Point ResolveDropPoint(UiActionDragRequest request, FlowDropContextModel dropContext)
    {
        var requestedDropPoint = dropContext.DropPoint;

        if (!string.IsNullOrWhiteSpace(dropContext.TargetLaneId) &&
            !string.Equals(dropContext.TargetLaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return requestedDropPoint;
        }

        if (!string.IsNullOrWhiteSpace(dropContext.TargetEdgeId))
        {
            return requestedDropPoint;
        }

        var anchorNode = ResolveAnchorNode();
        if (anchorNode is null)
        {
            return requestedDropPoint;
        }

        var (candidateWidth, candidateHeight) = request.IsContainer
            ? (ContainerNodeDefaultWidth, ContainerNodeDefaultHeight)
            : (ActionNodeDefaultWidth, ActionNodeDefaultHeight);

        var targetX = anchorNode.Bounds.X + ((anchorNode.Bounds.Width - candidateWidth) / 2d);
        var targetY = anchorNode.Bounds.Y + anchorNode.Bounds.Height + AutoDropVerticalGap;
        var candidate = new Rect(targetX, targetY, candidateWidth, candidateHeight);

        for (var pass = 0; pass < AutoDropMaxCollisionPasses && CollidesWithExistingNode(candidate); pass++)
        {
            candidate = new Rect(candidate.X, ResolveNextCandidateY(candidate), candidate.Width, candidate.Height);
        }

        return new Point(candidate.X, candidate.Y);
    }

    private double ResolveNextCandidateY(Rect candidate)
    {
        var blockingBottom = Document.Nodes
            .Select(node => new Rect(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height))
            .Where(existing => candidate.IntersectsWith(existing))
            .Select(existing => existing.Bottom)
            .DefaultIfEmpty(candidate.Bottom)
            .Max();

        return blockingBottom + AutoDropVerticalGap;
    }

    private FlowNodeModel? ResolveAnchorNode()
    {
        if (!string.IsNullOrWhiteSpace(_lastCreatedNodeId))
        {
            var createdNode = Document.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, _lastCreatedNodeId, StringComparison.Ordinal));
            if (createdNode is not null)
            {
                return createdNode;
            }
        }

        if (Document.RootLane.NodeIds.Count == 0)
        {
            return null;
        }

        var tailNodeId = Document.RootLane.NodeIds[^1];
        return Document.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, tailNodeId, StringComparison.Ordinal));
    }

    private bool CollidesWithExistingNode(Rect candidate)
    {
        foreach (var node in Document.Nodes)
        {
            var bounds = node.Bounds;
            var nodeRect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            if (candidate.IntersectsWith(nodeRect))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record DropSnapshot(
        string ActionId,
        string CategoryId,
        bool IsContainer,
        string? TargetLaneId,
        string? TargetEdgeId,
        string? TargetContainerNodeId,
        double X,
        double Y,
        long Timestamp)
    {
        public static DropSnapshot Create(UiActionDragRequest request, Point dropPoint, FlowDropContextModel context)
        {
            return new DropSnapshot(
                request.ActionId,
                request.CategoryId,
                request.IsContainer,
                context.TargetLaneId,
                context.TargetEdgeId,
                context.TargetContainerNodeId,
                dropPoint.X,
                dropPoint.Y,
                Environment.TickCount64);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static FlowCanvasViewModel CreateDefault(DiagnosticsService diagnosticsService)
    {
        return new FlowCanvasViewModel(
            diagnosticsService,
            new FlowEditingService(),
            new FlowPersistenceService(),
            new FlowEdgeRoutingService(),
            new FlowHitTestService(),
            new FlowLayoutService(),
            new FlowExecutionMapper(),
            new DefaultNodeInspectorFactory());
    }
}
