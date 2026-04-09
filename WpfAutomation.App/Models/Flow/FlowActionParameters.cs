namespace WpfAutomation.App.Models.Flow;

public abstract record ActionParameters;

public abstract record ContainerParameters;

public sealed record UnknownActionParameters : ActionParameters;

public sealed record UnknownContainerParameters : ContainerParameters;

public sealed record ForContainerParameters(
    int Start = 0,
    int End = 10,
    int Step = 1,
    int? MaxIterationsOverride = null) : ContainerParameters;

public sealed record ForEachContainerParameters(
    string ItemsExpression = "",
    string ItemVariable = "item",
    int? MaxIterationsOverride = null) : ContainerParameters;

public sealed record WhileContainerParameters(
    string ConditionExpression = "true",
    int MaxIterations = 1000) : ContainerParameters;

public sealed record OpenBrowserActionParameters(
    string BrowserEngine = "chromium",
    bool Headless = true,
    int TimeoutMs = 5000,
    int RetryCount = 3) : ActionParameters;

public sealed record NewPageActionParameters(
    string InitialUrl = "",
    bool BringToFront = true) : ActionParameters;

public sealed record CloseBrowserActionParameters(
    bool CloseAllPages = true) : ActionParameters;

public sealed record NavigateToUrlActionParameters(
    string Url = "https://example.com",
    int TimeoutMs = 30000,
    bool WaitUntilNetworkIdle = true) : ActionParameters;

public sealed record GoBackActionParameters(
    int TimeoutMs = 10000) : ActionParameters;

public sealed record GoForwardActionParameters(
    int TimeoutMs = 10000) : ActionParameters;

public sealed record ReloadPageActionParameters(
    bool IgnoreCache = false,
    int TimeoutMs = 10000) : ActionParameters;

public sealed record WaitForUrlActionParameters(
    string UrlPattern = "",
    int TimeoutMs = 30000,
    bool IsRegex = false) : ActionParameters;

public sealed record ClickElementActionParameters(
    string Selector = "",
    string? FrameSelector = null,
    bool Force = false,
    int TimeoutMs = 10000) : ActionParameters;

public sealed record FillInputActionParameters(
    string Selector = "",
    string Value = "",
    bool ClearFirst = true,
    int TimeoutMs = 10000) : ActionParameters;

public sealed record HoverElementActionParameters(
    string Selector = "",
    int TimeoutMs = 10000) : ActionParameters;

public sealed record PressKeyActionParameters(
    string Key = "Enter",
    string? Selector = null,
    int TimeoutMs = 10000) : ActionParameters;

public sealed record SelectOptionActionParameters(
    string Selector = "",
    string OptionValue = "",
    int TimeoutMs = 10000) : ActionParameters;

public sealed record ExpectEnabledActionParameters(
    string Selector = "",
    int TimeoutMs = 5000) : ActionParameters;

public sealed record ExpectHiddenActionParameters(
    string Selector = "",
    int TimeoutMs = 5000) : ActionParameters;

public sealed record ExpectTextActionParameters(
    string Selector = "",
    string ExpectedText = "",
    bool IgnoreCase = false,
    int TimeoutMs = 5000) : ActionParameters;

public sealed record ExpectVisibleActionParameters(
    string Selector = "",
    int TimeoutMs = 5000) : ActionParameters;
