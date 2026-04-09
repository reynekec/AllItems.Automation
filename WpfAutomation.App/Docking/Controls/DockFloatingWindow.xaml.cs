using System.Windows;
using System.Windows.Input;
using WpfAutomation.App.Docking.Models;

namespace WpfAutomation.App.Docking.Controls;

public partial class DockFloatingWindow : Window
{
    private readonly string _dragFormat;
    private Point _dragStartPoint;

    public DockFloatingWindow(DockPanelState panel, string dragFormat)
    {
        InitializeComponent();
        Panel = panel;
        _dragFormat = dragFormat;

        Title = panel.Title;
        PanelTitle.Text = panel.Title;
        PanelContent.Text = panel.ContentKey;
    }

    public DockPanelState Panel { get; }

    private void DragToDockButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _dragStartPoint = eventArgs.GetPosition(this);
    }

    private void DragToDockButton_OnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = eventArgs.GetPosition(this);
        var delta = position - _dragStartPoint;

        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var payload = new DataObject();
        payload.SetData(_dragFormat, Panel.PanelId);
        var result = DragDrop.DoDragDrop((DependencyObject)sender, payload, DragDropEffects.Move);

        if (result == DragDropEffects.Move)
        {
            Close();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        Close();
    }
}
