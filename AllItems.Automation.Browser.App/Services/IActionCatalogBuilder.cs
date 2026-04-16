using System.Reflection;
using AllItems.Automation.Browser.App.Models;

namespace AllItems.Automation.Browser.App.Services;

public interface IActionCatalogBuilder
{
    IReadOnlyList<UiActionCategory> Build(IEnumerable<Assembly> assemblies);
}