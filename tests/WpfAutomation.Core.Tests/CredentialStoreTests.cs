using System.Runtime.Versioning;
using System.Security;
using FluentAssertions;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Services.Credentials;

namespace WpfAutomation.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _dbPath;

    public CredentialStoreTests()
    {
        // Each test gets its own temp file so tests are fully isolated.
        _dbPath = Path.Combine(Path.GetTempPath(), $"wpa-test-{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        var tempFile = _dbPath + ".tmp";
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    // -------------------------------------------------------------------------
    // Round-trip tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RoundTrip_SingleEntry_SurvivesEncryptDecrypt()
    {
        var password = CreateSecureString("correct-horse-battery-staple");
        var entry    = CreateWebEntry("My Login", WebAuthKind.UsernamePassword, ("Username", "alice"), ("Password", "s3cr3t!"));

        // Write
        using (var store = new CredentialStore(_dbPath))
        {
            store.Unlock(password);
            await store.SaveAsync(entry);
        }

        // Read with a fresh instance
        using var readStore = new CredentialStore(_dbPath);
        readStore.Unlock(password);
        var loaded = await readStore.LoadAllAsync();

        loaded.Should().HaveCount(1);
        var loaded0 = loaded[0].Should().BeOfType<WebCredentialEntry>().Subject;
        loaded0.Id.Should().Be(entry.Id);
        loaded0.Name.Should().Be("My Login");
        loaded0.Fields["Username"].Should().Be("alice");
        loaded0.Fields["Password"].Should().Be("s3cr3t!");
    }

    [Fact]
    public async Task RoundTrip_MultipleEntries_AllPresent()
    {
        var password = CreateSecureString("pass123");
        var e1 = CreateWebEntry("Login A", WebAuthKind.UsernamePassword, ("Username", "userA"), ("Password", "passA"));
        var e2 = CreateWebEntry("Login B", WebAuthKind.ApiKeyBearer, ("TokenName", "My API"), ("Token", "tok-abc"));
        var e3 = CreateWebEntry("Login C", WebAuthKind.HttpBasicAuth, ("Username", "svc"), ("Password", "basic!"));

        using (var store = new CredentialStore(_dbPath))
        {
            store.Unlock(password);
            await store.SaveAsync(e1);
            await store.SaveAsync(e2);
            await store.SaveAsync(e3);
        }

        using var readStore = new CredentialStore(_dbPath);
        readStore.Unlock(password);
        var loaded = await readStore.LoadAllAsync();

        loaded.Should().HaveCount(3);
        loaded.Select(x => x.Id).Should().BeEquivalentTo([e1.Id, e2.Id, e3.Id]);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingEntry()
    {
        var password = CreateSecureString("pass123");
        var entry = CreateWebEntry("My Login", WebAuthKind.UsernamePassword, ("Username", "alice"), ("Password", "old"));

        using (var store = new CredentialStore(_dbPath))
        {
            store.Unlock(password);
            await store.SaveAsync(entry);

            var updated = entry with { Fields = new Dictionary<string, string> { ["Username"] = "alice", ["Password"] = "new-pass" } };
            await store.SaveAsync(updated);
        }

        using var readStore = new CredentialStore(_dbPath);
        readStore.Unlock(password);
        var loaded = await readStore.LoadAllAsync();

        loaded.Should().HaveCount(1);
        loaded[0].Should().BeOfType<WebCredentialEntry>()
            .Which.Fields["Password"].Should().Be("new-pass");
    }

    // -------------------------------------------------------------------------
    // Delete tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var password = CreateSecureString("pass123");
        var e1 = CreateWebEntry("Keep",   WebAuthKind.UsernamePassword, ("Username", "keep"));
        var e2 = CreateWebEntry("Delete", WebAuthKind.UsernamePassword, ("Username", "gone"));

        using (var store = new CredentialStore(_dbPath))
        {
            store.Unlock(password);
            await store.SaveAsync(e1);
            await store.SaveAsync(e2);
            await store.DeleteAsync(e2.Id);
        }

        using var readStore = new CredentialStore(_dbPath);
        readStore.Unlock(password);
        var loaded = await readStore.LoadAllAsync();

        loaded.Should().HaveCount(1);
        loaded[0].Id.Should().Be(e1.Id);
    }

    [Fact]
    public async Task DeleteAsync_NonexistentId_IsNoOp()
    {
        var password = CreateSecureString("pass123");
        var entry = CreateWebEntry("Keep", WebAuthKind.UsernamePassword, ("Username", "keep"));

        using var store = new CredentialStore(_dbPath);
        store.Unlock(password);
        await store.SaveAsync(entry);

        await store.Invoking(s => s.DeleteAsync(Guid.NewGuid())).Should().NotThrowAsync();

        var loaded = await store.LoadAllAsync();
        loaded.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // Wrong password tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Unlock_WrongPassword_Throws()
    {
        var password = CreateSecureString("correct");
        var entry    = CreateWebEntry("e", WebAuthKind.UsernamePassword, ("Username", "u"));

        using (var store = new CredentialStore(_dbPath))
        {
            store.Unlock(password);
            await store.SaveAsync(entry);
        }

        var wrong = CreateSecureString("wrong-password");
        var badStore = new CredentialStore(_dbPath);
        badStore.Invoking(s => s.Unlock(wrong))
            .Should().Throw<Exception>("wrong password should cause decryption to fail");
        badStore.Dispose();
    }

    // -------------------------------------------------------------------------
    // State guard tests
    // -------------------------------------------------------------------------

    [Fact]
    public void IsUnlocked_InitiallyFalse()
    {
        using var store = new CredentialStore(_dbPath);
        store.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsUnlocked_TrueAfterUnlock_FalseAfterLock()
    {
        var password = CreateSecureString("pw");
        var store = new CredentialStore(_dbPath);

        store.Unlock(password);
        store.IsUnlocked.Should().BeTrue();

        store.Lock();
        store.IsUnlocked.Should().BeFalse();

        store.Dispose();
    }

    [Fact]
    public async Task LoadAllAsync_WhenLocked_Throws()
    {
        var store = new CredentialStore(_dbPath);
        await store.Invoking(s => s.LoadAllAsync()).Should().ThrowAsync<InvalidOperationException>();
        store.Dispose();
    }

    [Fact]
    public async Task SaveAsync_WhenLocked_Throws()
    {
        var entry = CreateWebEntry("e", WebAuthKind.UsernamePassword);
        var store = new CredentialStore(_dbPath);
        await store.Invoking(s => s.SaveAsync(entry)).Should().ThrowAsync<InvalidOperationException>();
        store.Dispose();
    }

    [Fact]
    public async Task LoadAllAsync_NewStore_NoFile_ReturnsEmpty()
    {
        var password = CreateSecureString("pw");
        using var store = new CredentialStore(_dbPath);

        store.Unlock(password);
        var loaded = await store.LoadAllAsync();

        loaded.Should().BeEmpty();
    }

    [Fact]
    public void Unlock_AlreadyUnlocked_Throws()
    {
        var password = CreateSecureString("pw");
        var store = new CredentialStore(_dbPath);

        store.Unlock(password);
        store.Invoking(s => s.Unlock(password)).Should().Throw<InvalidOperationException>();
        store.Dispose();
    }

    // -------------------------------------------------------------------------
    // Sensitive field metadata tests
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(WebAuthKind.UsernamePassword, "Password")]
    [InlineData(WebAuthKind.UsernameEmailOtp, "Password")]
    [InlineData(WebAuthKind.UsernameEmailOtp, "ImapPassword")]
    [InlineData(WebAuthKind.Totp,             "TotpSecret")]
    [InlineData(WebAuthKind.ApiKeyBearer,     "Token")]
    [InlineData(WebAuthKind.CertificateMtls,  "CertificatePassword")]
    public void SensitiveFieldNames_ContainsExpectedKey(WebAuthKind kind, string fieldKey)
    {
        WebCredentialEntry.SensitiveFieldNames(kind).Should().Contain(fieldKey);
    }

    [Theory]
    [InlineData(WebAuthKind.UsernamePassword, "Username")]
    [InlineData(WebAuthKind.ApiKeyBearer,     "TokenName")]
    public void SensitiveFieldNames_DoesNotContainNonSensitiveKey(WebAuthKind kind, string fieldKey)
    {
        WebCredentialEntry.SensitiveFieldNames(kind).Should().NotContain(fieldKey);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SecureString CreateSecureString(string value)
    {
        var ss = new SecureString();
        foreach (var c in value)
        {
            ss.AppendChar(c);
        }

        ss.MakeReadOnly();
        return ss;
    }

    private static WebCredentialEntry CreateWebEntry(
        string name,
        WebAuthKind kind,
        params (string Key, string Value)[] fields)
    {
        return new WebCredentialEntry(
            Guid.NewGuid(),
            name,
            kind,
            fields.ToDictionary(f => f.Key, f => f.Value));
    }
}
