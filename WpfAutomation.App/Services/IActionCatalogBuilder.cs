using System.Reflection;
using WpfAutomation.App.Models;

namespace WpfAutomation.App.Services;

public interface IActionCatalogBuilder
{
    IReadOnlyList<UiActionCategory> Build(IEnumerable<Assembly> assemblies);
}