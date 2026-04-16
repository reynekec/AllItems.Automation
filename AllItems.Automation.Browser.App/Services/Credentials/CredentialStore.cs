using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Services.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Credentials;

/// <summary>
/// AES-256-CBC encrypted JSON file credential store.
///
/// File format  (binary):
///   [32 bytes — PBKDF2-SHA256 salt]
///   [16 bytes — AES-CBC IV]
///   [N  bytes — AES-256-CBC(key, IV, UTF-8 JSON of CredentialDatabase)]
///
/// Key derivation: PBKDF2-SHA256, 100 000 iterations, 256-bit output.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialStore : ICredentialStore, IDisposable
{
    private const int SaltSize          = 32;
    private const int IvSize            = 16;
    private const int KeySize           = 32;   // 256 bits
    private const int Pbkdf2Iterations  = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented            = false,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private byte[]? _key;    // 32-byte AES key — zeroed on Lock()
    private byte[]? _salt;   // 32-byte PBKDF2 salt — zeroed on Lock()
    private Dictionary<Guid, CredentialEntry>? _cache;

    // ---------------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------------

    public CredentialStore()
        : this(GetDefaultFilePath()) { }

    /// <param name="filePath">Full path to the encrypted database file. The directory must exist or be creatable.</param>
    public CredentialStore(string filePath)
    {
        _filePath = filePath;
    }

    // ---------------------------------------------------------------------------
    // ICredentialStore
    // ---------------------------------------------------------------------------

    public bool IsUnlocked => _key is not null;

    public void Unlock(SecureString masterPassword)
    {
        if (IsUnlocked)
        {
            throw new InvalidOperationException("The credential store is already unlocked.");
        }

        var passwordBytes = SecureStringToUtf8Bytes(masterPassword);
        try
        {
            byte[] salt;
            byte[] key;
            var cache = new Dictionary<Guid, CredentialEntry>();

            if (File.Exists(_filePath))
            {
                // Read the persisted salt so we can re-derive the same key.
                salt = ReadSaltFromFile(_filePath);
                key  = DeriveKey(passwordBytes, salt);

                // Verify by decrypting — throws on wrong password.
                var entries = DecryptAndDeserialize(_filePath, key);
                foreach (var entry in entries)
                {
                    cache[entry.Id] = entry;
                }
            }
            else
            {
                // First use — generate a fresh salt; no file to verify against.
                salt = RandomNumberGenerator.GetBytes(SaltSize);
                key  = DeriveKey(passwordBytes, salt);
            }

            _key   = key;
            _salt  = salt;
            _cache = cache;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }

        if (_salt is not null)
        {
            CryptographicOperations.ZeroMemory(_salt);
            _salt = null;
        }

        _cache = null;
    }

    public Task<IReadOnlyList<CredentialEntry>> LoadAllAsync()
    {
        RequireUnlocked();
        IReadOnlyList<CredentialEntry> result = _cache!.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<CredentialEntry?> GetByIdAsync(Guid id)
    {
        RequireUnlocked();
        _cache!.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }

    public async Task SaveAsync(CredentialEntry entry)
    {
        RequireUnlocked();

        AppCrashLogger.Info($"CredentialStore.SaveAsync waiting for lock. Id={entry.Id}");
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            AppCrashLogger.Info($"CredentialStore.SaveAsync acquired lock. Id={entry.Id}");
            _cache![entry.Id] = entry;
            await PersistAsync().ConfigureAwait(false);
            AppCrashLogger.Info($"CredentialStore.SaveAsync completed. Id={entry.Id}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        RequireUnlocked();

        AppCrashLogger.Info($"CredentialStore.DeleteAsync waiting for lock. Id={id}");
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            AppCrashLogger.Info($"CredentialStore.DeleteAsync acquired lock. Id={id}");
            _cache!.Remove(id);
            await PersistAsync().ConfigureAwait(false);
            AppCrashLogger.Info($"CredentialStore.DeleteAsync completed. Id={id}");
        }
        finally
        {
            _lock.Release();
        }
    }

    // ---------------------------------------------------------------------------
    // IDisposable
    // ---------------------------------------------------------------------------

    public void Dispose()
    {
        Lock();
        _lock.Dispose();
    }

    // ---------------------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------------------

    private void RequireUnlocked()
    {
        if (!IsUnlocked)
        {
            throw new InvalidOperationException("The credential store is locked. Call Unlock() first.");
        }
    }

    private async Task PersistAsync()
    {
        // Caller holds _lock.
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var db   = new CredentialDatabase { Entries = _cache!.Values.OfType<WebCredentialEntry>().ToList() };
        var json = JsonSerializer.Serialize(db, JsonOptions);
        var plaintext = Encoding.UTF8.GetBytes(json);

        var iv         = RandomNumberGenerator.GetBytes(IvSize);
        var ciphertext = AesEncrypt(_key!, iv, plaintext);

        // Overwrite atomically via a temp file.
        var tempPath = _filePath + ".tmp";
        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fs.WriteAsync(_salt!).ConfigureAwait(false);
            await fs.WriteAsync(iv).ConfigureAwait(false);
            await fs.WriteAsync(ciphertext).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    // ---------------------------------------------------------------------------
    // Cryptographic helpers
    // ---------------------------------------------------------------------------

    private static byte[] DeriveKey(byte[] passwordBytes, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes,
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    private static byte[] ReadSaltFromFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var salt = new byte[SaltSize];
        var read = fs.Read(salt, 0, SaltSize);
        if (read < SaltSize)
        {
            throw new InvalidDataException("Credential database file is corrupt (salt too short).");
        }

        return salt;
    }

    private static IReadOnlyList<CredentialEntry> DecryptAndDeserialize(string path, byte[] key)
    {
        byte[] fileBytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fileBytes = new byte[fs.Length];
            _ = fs.Read(fileBytes, 0, fileBytes.Length);
        }

        if (fileBytes.Length < SaltSize + IvSize + 1)
        {
            throw new InvalidDataException("Credential database file is corrupt (too short).");
        }

        var iv         = fileBytes.AsSpan(SaltSize, IvSize).ToArray();
        var ciphertext = fileBytes.AsSpan(SaltSize + IvSize).ToArray();
        var plaintext  = AesDecrypt(key, iv, ciphertext);

        var json = Encoding.UTF8.GetString(plaintext);
        var db   = JsonSerializer.Deserialize<CredentialDatabase>(json, JsonOptions);

        if (db is null)
        {
            throw new InvalidDataException("Credential database file is corrupt (invalid JSON).");
        }

        return db.Entries.Cast<CredentialEntry>().ToList();
    }

    private static byte[] AesEncrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key  = key;
        aes.IV   = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }

    private static byte[] AesDecrypt(byte[] key, byte[] iv, byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key     = key;
        aes.IV      = iv;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private static byte[] SecureStringToUtf8Bytes(SecureString ss)
    {
        var bstr = Marshal.SecureStringToBSTR(ss);
        try
        {
            var chars = new char[ss.Length];
            Marshal.Copy(bstr, chars, 0, ss.Length);
            var bytes = Encoding.UTF8.GetBytes(chars);
            Array.Clear(chars, 0, chars.Length);
            return bytes;
        }
        finally
        {
            Marshal.ZeroFreeBSTR(bstr);
        }
    }

    private static string GetDefaultFilePath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfAutomation");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "credentials.db");
    }

    // ---------------------------------------------------------------------------
    // Internal DTO — only used for serialization, never exposed publicly
    // ---------------------------------------------------------------------------

    private sealed class CredentialDatabase
    {
        public List<WebCredentialEntry> Entries { get; set; } = [];
    }
}
