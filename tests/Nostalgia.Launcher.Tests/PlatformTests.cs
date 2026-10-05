using Nostalgia.Launcher.Core.Accounts;
using Nostalgia.Launcher.Platform.Linux;
using Nostalgia.Launcher.Platform.Windows;
using Xunit;

namespace Nostalgia.Launcher.Tests;

public class CredentialStoreTests
{
    [Fact]
    public void Dpapi_round_trip_overwrite_and_delete()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "login.dat");
        var store = new DpapiCredentialStore(path);

        Assert.Null(store.Load());
        store.Save(new LoginCredential("Bob1", "Xk7pQ2mZr9TvBn4c"));
        Assert.Equal(new LoginCredential("Bob1", "Xk7pQ2mZr9TvBn4c"), new DpapiCredentialStore(path).Load());

        // Not stored in clear text.
        string raw = File.ReadAllText(path);
        Assert.DoesNotContain("Xk7pQ2mZr9TvBn4c", raw);
        Assert.DoesNotContain("Bob1", raw);

        // After /reset: replace the password.
        store.Save(new LoginCredential("Bob1", "NewPw9"));
        Assert.Equal("NewPw9", store.Load()!.Password);

        store.Delete();
        Assert.False(File.Exists(path));
        Assert.Null(store.Load());
        store.Delete(); // idempotent
    }

    [Fact]
    public void Dpapi_ignores_a_corrupt_file()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var tmp = new TempDir();
        string path = Path.Combine(tmp.Path, "login.dat");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        Assert.Null(new DpapiCredentialStore(path).Load());
    }

    [Fact]
    public void Linux_stub_keeps_the_login_for_the_session_only()
    {
        var s = new SessionOnlyCredentialStore();
        s.Save(new LoginCredential("a", "b"));
        Assert.Equal("a", s.Load()!.Account);
        Assert.Null(new SessionOnlyCredentialStore().Load());
        Assert.False(new LinuxPlatformServices().IsSupported);
    }
}
