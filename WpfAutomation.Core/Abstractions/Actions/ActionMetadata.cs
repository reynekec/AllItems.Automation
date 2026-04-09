namespace WpfAutomation.Core.Abstractions.Actions;

public sealed record ActionMetadata(
    string ActionId,
    string DisplayName,
    string CategoryId,
    string CategoryName,
    string IconKeyOrPath,
    IReadOnlyList<string> Keywords,
    int SortOrder = 0,
    bool? IsContainer = null);