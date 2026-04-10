using System.Runtime.Versioning;
using System.Security;
using FluentAssertions;
using Moq;
using WpfAutomation.App.Credentials;
using WpfAutomation.App.Credentials.Models;
using WpfAutomation.App.Services.Credentials;

namespace WpfAutomation.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class MasterPasswordServiceTests : IDisposable
{
    private readonly string _dbPath;

    public MasterPasswordServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"wpa-master-password-{Guid.NewGuid():N}.db");
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
    public void EnsureUnlockedBeforeRun_WhenStoreAlreadyUnlocked_DoesNotPrompt()
    {
        var credentialStore = new Mock<ICredentialStore>(MockBehavior.Strict);
        credentialStore.SetupGet(candidate => candidate.IsUnlocked).Returns(true);

        var dialogService = new Mock<ICredentialUnlockDialogService>(MockBehavior.Strict);
        var service = new MasterPasswordService(credentialStore.Object, dialogService.Object);

        var result = service.EnsureUnlockedBeforeRun();

        result.Should().BeTrue();
        dialogService.Verify(candidate => candidate.ShowDialog(It.IsAny<CredentialUnlockViewModel>()), Times.Never);
    }

    [Fact]
    public async Task CredentialUnlockViewModel_WrongPassword_ReturnsFalse_AndSetsError()
    {
        var correctPassword = CreateSecureString("correct-password");
        var wrongPassword = CreateSecureString("wrong-password");

        using (var writerStore = new CredentialStore(_dbPath))
        {
            writerStore.Unlock(correctPassword);
            await writerStore.SaveAsync(new WebCredentialEntry(
                Guid.NewGuid(),
                "Login",
                WebAuthKind.UsernamePassword,
                new Dictionary<string, string>
                {
                    [WebCredentialEntry.FieldKeys.Username] = "alice",
                    [WebCredentialEntry.FieldKeys.Password] = "secret",
                }));
        }

        using var lockedStore = new CredentialStore(_dbPath);
        var viewModel = new CredentialUnlockViewModel(lockedStore);

        var result = viewModel.TryUnlock(wrongPassword);

        result.Should().BeFalse();
        lockedStore.IsUnlocked.Should().BeFalse();
        viewModel.HasError.Should().BeTrue();
        viewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EnsureUnlockedBeforeRun_CorrectPassword_UnlocksStore_AndReturnsTrue()
    {
        var correctPassword = CreateSecureString("correct-password");

        using (var writerStore = new CredentialStore(_dbPath))
        {
            writerStore.Unlock(correctPassword);
            await writerStore.SaveAsync(new WebCredentialEntry(
                Guid.NewGuid(),
                "Service Account",
                WebAuthKind.HttpBasicAuth,
                new Dictionary<string, string>
                {
                    [WebCredentialEntry.FieldKeys.Username] = "svc",
                    [WebCredentialEntry.FieldKeys.Password] = "secret",
                }));
        }

        using var lockedStore = new CredentialStore(_dbPath);
        var dialogService = new Mock<ICredentialUnlockDialogService>(MockBehavior.Strict);
        dialogService
            .Setup(candidate => candidate.ShowDialog(It.IsAny<CredentialUnlockViewModel>()))
            .Returns<CredentialUnlockViewModel>(viewModel => viewModel.TryUnlock(CreateSecureString("correct-password")));

        var service = new MasterPasswordService(lockedStore, dialogService.Object);

        var result = service.EnsureUnlockedBeforeRun();

        result.Should().BeTrue();
        lockedStore.IsUnlocked.Should().BeTrue();
        dialogService.Verify(candidate => candidate.ShowDialog(It.IsAny<CredentialUnlockViewModel>()), Times.Once);
    }

    private static SecureString CreateSecureString(string value)
    {
        var secureString = new SecureString();
        foreach (var character in value)
        {
            secureString.AppendChar(character);
        }

        secureString.MakeReadOnly();
        return secureString;
    }
}
