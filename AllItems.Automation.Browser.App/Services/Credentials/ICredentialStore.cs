using System.IO;
using System.Security;
using AllItems.Automation.Browser.App.Credentials.Models;

namespace AllItems.Automation.Browser.App.Services.Credentials;

public interface ICredentialStore
{
    /// <summary>True when the master password has been accepted and the AES key is held in memory.</summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Derives the AES key from <paramref name="masterPassword"/> and verifies it against the
    /// on-disk database (if one exists). Throws <see cref="System.Security.Cryptography.CryptographicException"/>
    /// or <see cref="InvalidDataException"/> when the password is wrong.
    /// </summary>
    void Unlock(SecureString masterPassword);

    /// <summary>Zeros the in-memory key and clears the cached entries.</summary>
    void Lock();

    /// <summary>Returns all stored credential entries. Requires the store to be unlocked.</summary>
    Task<IReadOnlyList<CredentialEntry>> LoadAllAsync();

    /// <summary>Returns a single credential by id, or null when no matching entry exists.</summary>
    Task<CredentialEntry?> GetByIdAsync(Guid id);

    /// <summary>
    /// Adds or updates <paramref name="entry"/> and persists the entire database.
    /// Requires the store to be unlocked.
    /// </summary>
    Task SaveAsync(CredentialEntry entry);

    /// <summary>
    /// Removes the credential with the given <paramref name="id"/> and persists the database.
    /// No-ops silently if the id does not exist. Requires the store to be unlocked.
    /// </summary>
    Task DeleteAsync(Guid id);
}
