using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace SelectorDemo.Wpf;

public class DomNode
{
    public string TagName { get; set; } = "";
    public string Id { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string InnerText { get; set; } = "";
    public bool IsSelected { get; set; }
    public List<DomNode> Children { get; set; } = new();
    public string CssSelector { get; set; } = "";

    public string DisplayText
    {
        get
        {
            var text = $"<{TagName}";
            if (!string.IsNullOrEmpty(Id))
                text += $" id=\"{Id}\"";
            if (!string.IsNullOrEmpty(ClassName))
                text += $" class=\"{ClassName}\"";
            text += ">";

            if (!string.IsNullOrWhiteSpace(InnerText))
                text += $" \"{InnerText}\"";

            return text;
        }
    }
}

public partial class BrowserSelectionControl : UserControl
{
    public delegate void TreeNodeHoverChangedEventHandler(string? selector);
    public event TreeNodeHoverChangedEventHandler? TreeNodeHoverChanged;

    public BrowserSelectionControl()
    {
        InitializeComponent();
    }

    public void SetStatus(string status)
    {
        StatusTextBlock.Text = status;
    }

    public void SetSelectors(string cssSelector, string xpathSelector)
    {
        CssSelectorTextBox.Text = cssSelector;
        XPathSelectorTextBox.Text = xpathSelector;
    }

    public void SetElementBrowserInfo(string htmlSource, string attributes, string computedStyles, string domTreeJson = "")
    {
        HtmlSourceTextBox.Text = htmlSource;
        AttributesTextBox.Text = attributes;
        ComputedStylesTextBox.Text = computedStyles;

        PopulateDomTree(domTreeJson);
    }

    private void PopulateDomTree(string domTreeJson)
    {
        try
        {
            DomTreeView.Items.Clear();
            
            if (string.IsNullOrEmpty(domTreeJson) || domTreeJson == "{}")
            {
                var emptyItem = new TreeViewItem { Header = "No DOM data available" };
                DomTreeView.Items.Add(emptyItem);
                return;
            }

            using var doc = JsonDocument.Parse(domTreeJson, new JsonDocumentOptions { MaxDepth = 512 });
            var root = doc.RootElement;
            
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tagName", out _))
            {
                var domNode = JsonToDomNode(root);
                if (domNode != null)
                {
                    var treeItem = CreateTreeItem(domNode);
                    DomTreeView.Items.Add(treeItem);
                    treeItem.IsExpanded = true;
                }
                else
                {
                    var errorItem = new TreeViewItem { Header = "Failed to create DOM nodes" };
                    DomTreeView.Items.Add(errorItem);
                }
            }
            else
            {
                var errorItem = new TreeViewItem { Header = $"Invalid DOM tree format (ValueKind: {root.ValueKind})" };
                DomTreeView.Items.Add(errorItem);
            }
        }
        catch (Exception ex)
        {
            DomTreeView.Items.Clear();
            var errorItem = new TreeViewItem { Header = $"Error: {ex.Message}" };
            DomTreeView.Items.Add(errorItem);
        }
    }

    private string GenerateCssSelector(DomNode node)
    {
        if (string.IsNullOrEmpty(node.TagName))
            return "";

        // If element has an ID, use it
        if (!string.IsNullOrEmpty(node.Id))
            return $"#{node.Id}";

        // Otherwise, use tag name with class if available
        var selector = node.TagName;
        if (!string.IsNullOrEmpty(node.ClassName))
        {
            // Handle multiple classes
            var classes = node.ClassName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in classes)
            {
                selector += $".{cls}";
            }
        }

        return selector;
    }

    private DomNode? JsonToDomNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var node = new DomNode();

        if (element.TryGetProperty("tagName", out var tagNameProp) && tagNameProp.ValueKind == JsonValueKind.String)
            node.TagName = tagNameProp.GetString() ?? "";

        if (element.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            node.Id = idProp.GetString() ?? "";

        if (element.TryGetProperty("className", out var classProp) && classProp.ValueKind == JsonValueKind.String)
            node.ClassName = classProp.GetString() ?? "";

        if (element.TryGetProperty("innerText", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            node.InnerText = textProp.GetString() ?? "";

        if (element.TryGetProperty("isSelected", out var selectedProp) && selectedProp.ValueKind == JsonValueKind.True)
            node.IsSelected = true;

        if (element.TryGetProperty("children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var childElement in childrenProp.EnumerateArray())
            {
                var childNode = JsonToDomNode(childElement);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }
        }

        // Generate CSS selector after all properties are set
        node.CssSelector = GenerateCssSelector(node);

        return node;
    }

    private TreeViewItem CreateTreeItem(DomNode domNode)
    {
        var item = new TreeViewItem();
        item.Tag = domNode.CssSelector;  // Store selector for hover events
        
        var textBlock = new TextBlock();
        textBlock.Text = domNode.DisplayText;
        textBlock.Margin = new System.Windows.Thickness(0, 2, 0, 2);
        textBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 41, 55));  // Dark gray
        
        if (domNode.IsSelected)
        {
            textBlock.FontWeight = FontWeights.Bold;
            textBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 108, 189));  // Blue
            var border = new System.Windows.Controls.Border();
            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 15, 108, 189));  // Light blue highlight
            border.Padding = new System.Windows.Thickness(2);
            border.Child = textBlock;
            item.Header = border;
        }
        else
        {
            item.Header = textBlock;
        }

        // Add hover event handlers
        item.MouseEnter += (s, e) =>
        {
            var treeItem = s as TreeViewItem;
            if (treeItem?.Tag is string selector && !string.IsNullOrEmpty(selector))
            {
                TreeNodeHoverChanged?.Invoke(selector);
            }
        };

        item.MouseLeave += (s, e) =>
        {
            TreeNodeHoverChanged?.Invoke(null);
        };

        if (domNode.Children.Count > 0)
        {
            foreach (var child in domNode.Children)
            {
                var childItem = CreateTreeItem(child);
                item.Items.Add(childItem);
            }
            // Expand nodes that have the selected element
            item.IsExpanded = domNode.IsSelected || HasSelectedDescendant(domNode);
        }

        return item;
    }

    private bool HasSelectedDescendant(DomNode node)
    {
        if (node.IsSelected) return true;
        foreach (var child in node.Children)
        {
            if (HasSelectedDescendant(child)) return true;
        }
        return false;
    }

    public void ClearSelectors()
    {
        CssSelectorTextBox.Text = string.Empty;
        XPathSelectorTextBox.Text = string.Empty;
        StatusTextBlock.Text = "No element selected.";
        ClearElementBrowserInfo();
    }

    public void ClearElementBrowserInfo()
    {
        HtmlSourceTextBox.Text = string.Empty;
        AttributesTextBox.Text = string.Empty;
        ComputedStylesTextBox.Text = string.Empty;
        DomTreeView.Items.Clear();
    }

    private void CopyCss_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CssSelectorTextBox.Text))
        {
            Clipboard.SetText(CssSelectorTextBox.Text);
        }
    }

    private void CopyXPath_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(XPathSelectorTextBox.Text))
        {
            Clipboard.SetText(XPathSelectorTextBox.Text);
        }
    }
}
