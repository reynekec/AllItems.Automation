using System.Runtime.Versioning;
using FluentAssertions;
using Moq;
using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.ViewModels;
using AllItems.Automation.Browser.App.Services.Credentials;

namespace AllItems.Automation.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class CredentialManagerPhaseTests : IDisposable
{
    private readonly string _dbPath;

    public CredentialManagerPhaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wpa-credential-manager-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        var tempPath = _dbPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task CredentialManagerViewModel_Save_NewCredential_Persists()
    {
        using var store = new CredentialStore(_dbPath);
        store.Unlock(CreateSecureString("phase3"));

        var viewModel = new CredentialManagerViewModel(store);
        await viewModel.InitializeAsync();

        viewModel.SelectedCredential.Should().BeNull();
        viewModel.NewCommand.Execute(null);
        viewModel.SelectedCredential.Should().NotBeNull();
        viewModel.SelectedCredential!.Name = "Portal Login";
        viewModel.SelectedCredential.WebAuthKind = WebAuthKind.UsernamePassword;
        viewModel.SelectedCredential.Username = "alice";
        viewModel.SelectedCredential.Password = "secret";

        viewModel.SaveCommand.Execute(null);

        var loaded = await store.LoadAllAsync();
        loaded.Should().ContainSingle();
        loaded[0].Should().BeOfType<WebCredentialEntry>()
            .Which.Name.Should().Be("Portal Login");
    }

    [Fact]
    public async Task CredentialManagerViewModel_Delete_Removes_Selected_Credential()
    {
        using var store = new CredentialStore(_dbPath);
        store.Unlock(CreateSecureString("phase3"));

        var original = new WebCredentialEntry(
            Guid.NewGuid(),
            "Delete Me",
            WebAuthKind.UsernamePassword,
            new Dictionary<string, string>
            {
                [WebCredentialEntry.FieldKeys.Username] = "u",
                [WebCredentialEntry.FieldKeys.Password] = "p",
            });

        await store.SaveAsync(original);

        var viewModel = new CredentialManagerViewModel(store, original.Id);
        await viewModel.InitializeAsync();

        viewModel.SelectedCredential.Should().NotBeNull();
        viewModel.SelectedCredential!.Id.Should().Be(original.Id);

        viewModel.DeleteCommand.Execute(null);
        await WaitForConditionAsync(() => viewModel.Credentials.All(entry => entry.Id != original.Id));

        var loaded = await store.LoadAllAsync();
        loaded.Should().BeEmpty();
    }

    [Fact]
    public void NavigateInspector_OpenCredentialManager_WritesBack_CredentialId_And_Name()
    {
        NavigateToUrlActionParameters? committed = null;

        var dialogService = new Mock<ICredentialManagerDialogService>(MockBehavior.Strict);
        dialogService
            .Setup(service => service.ShowDialog(It.IsAny<Guid?>(), It.IsAny<bool>()))
            .Returns(new CredentialManagerDialogResult(true, Guid.Parse("3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042"), "Contoso Login"));

        var viewModel = new NavigateToUrlInspectorViewModel(
            new NavigateToUrlActionParameters("https://example.com", 30000, true, null, null, true),
            new NavigateToUrlActionParameters(),
            parameters => committed = (NavigateToUrlActionParameters)parameters,
            dialogService.Object);

        viewModel.OpenCredentialManagerCommand.Execute(null);

        committed.Should().NotBeNull();
        committed!.CredentialName.Should().Be("Contoso Login");
        committed.CredentialId.Should().Be("3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042");
        viewModel.CredentialName.Should().Be("Contoso Login");
    }

    [Fact]
    public void NavigateInspector_Hides_Credential_Fields_From_Generic_Editor()
    {
        var viewModel = new NavigateToUrlInspectorViewModel(
            new NavigateToUrlActionParameters("https://example.com", 30000, true, "3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042", "Contoso Login", true),
            new NavigateToUrlActionParameters(),
            _ => { });

        viewModel.Fields.Should().ContainSingle(field => field.Name == "CredentialId" && !field.IsVisible);
        viewModel.Fields.Should().ContainSingle(field => field.Name == "CredentialName" && !field.IsVisible);
        viewModel.CredentialName.Should().Be("Contoso Login");
    }

    [Fact]
    public void NavigateInspector_MissingCredential_ShowsWarning()
    {
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        credentialStore.SetupGet(store => store.IsUnlocked).Returns(true);
        credentialStore
            .Setup(store => store.GetByIdAsync(Guid.Parse("3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042")))
            .ReturnsAsync((CredentialEntry?)null);

        var viewModel = new NavigateToUrlInspectorViewModel(
            new NavigateToUrlActionParameters("https://example.com", 30000, true, "3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042", "Contoso Login", true),
            new NavigateToUrlActionParameters(),
            _ => { },
            credentialStore: credentialStore.Object);

        viewModel.HasCredentialWarning.Should().BeTrue();
        viewModel.CredentialWarningMessage.Should().Be("Credential not found. Please re-select.");
    }

    [Fact]
    public void NavigateInspector_ClearCredential_ClearsIdAndName()
    {
        NavigateToUrlActionParameters? committed = null;

        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        credentialStore.SetupGet(store => store.IsUnlocked).Returns(false);

        var viewModel = new NavigateToUrlInspectorViewModel(
            new NavigateToUrlActionParameters("https://example.com", 30000, true, "3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042", "Contoso Login", true),
            new NavigateToUrlActionParameters(),
            parameters => committed = (NavigateToUrlActionParameters)parameters,
            credentialStore: credentialStore.Object);

        viewModel.ClearCredentialCommand.Execute(null);

        committed.Should().NotBeNull();
        committed!.CredentialId.Should().BeNullOrWhiteSpace();
        committed.CredentialName.Should().BeNullOrWhiteSpace();
        viewModel.CredentialName.Should().Be("None");
        viewModel.HasCredentialWarning.Should().BeFalse();
    }

    [Fact]
    public void NavigateInspector_DoesNotThrow_WhenCredentialStoreAccessFails()
    {
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        credentialStore
            .SetupGet(store => store.IsUnlocked)
            .Throws(new InvalidOperationException("Store unavailable"));

        var create = () => new NavigateToUrlInspectorViewModel(
            new NavigateToUrlActionParameters(
                "https://example.com",
                30000,
                true,
                "3f5ef90e-f96e-4dc3-9bb8-ef4f4cdb2042",
                "Contoso Login",
                true),
            new NavigateToUrlActionParameters(),
            _ => { },
            credentialStore: credentialStore.Object);

        create.Should().NotThrow();
    }

    private static System.Security.SecureString CreateSecureString(string value)
    {
        var secureString = new System.Security.SecureString();
        foreach (var character in value)
        {
            secureString.AppendChar(character);
        }

        secureString.MakeReadOnly();
        return secureString;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        condition().Should().BeTrue();
    }
}
