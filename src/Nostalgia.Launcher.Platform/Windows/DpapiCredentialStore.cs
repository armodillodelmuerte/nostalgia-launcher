using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Core.Platform;

namespace Nostalgia.Launcher.Platform.Windows;

/// <summary>
/// Login encrypted with DPAPI (scope CurrentUser) in <c>%LOCALAPPDATA%\Nostalgia\login.dat</c>: only this Windows
/// user on this PC can decrypt it. A copied file is useless elsewhere.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore(string path) : ICredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Nostalgia.Launcher.Login.v1");

    public string Description => "Windows DPAPI (CurrentUser)";

    public LoginCredential? Load()
    {
        try
        {
            if (!File.Exists(path)) return null;
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            var dto = JsonSerializer.Deserialize<Dto>(plain);
            Array.Clear(plain);
            return dto is { Account.Length: > 0, Password.Length: > 0 } ? new LoginCredential(dto.Account, dto.Password) : null;
        }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public void Save(LoginCredential login)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new Dto(login.Account, login.Password));
        byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        Array.Clear(plain);
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, cipher);
        File.Move(tmp, path, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private sealed record Dto(string Account, string Password);
}
