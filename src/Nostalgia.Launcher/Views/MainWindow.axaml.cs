using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Nostalgia.Launcher.Services;
using Nostalgia.Launcher.ViewModels;

namespace Nostalgia.Launcher.Views;

public partial class MainWindow : Window, IUiShell
{
    private Bitmap? _bgSmall, _bgLarge;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AttachShell(this);
                vm.PasswordFocusRequested += () => this.FindControl<TextBox>("PasswordBox")?.Focus();
            }
        };
        ScalingChanged += (_, _) => UpdateBackground();
        Opened += (_, _) => { FitToScreen(); UpdateBackground(); };
        SizeChanged += (_, _) => UpdateBackground();
    }

    /// <summary>1280×720 by default; smaller (16:9) if the work area can't hold it, e.g. 150 % on a 1080p laptop.</summary>
    private void FitToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        double scale = screen.Scaling;
        double maxW = screen.WorkingArea.Width / scale * 0.94;
        double maxH = screen.WorkingArea.Height / scale * 0.90 - 32; // title bar
        double f = Math.Min(1.0, Math.Min(maxW / 1280.0, maxH / 720.0));
        if (f < 1.0)
        {
            Width = Math.Round(1280 * f);
            Height = Math.Round(720 * f);
            var wa = screen.WorkingArea;
            Position = new PixelPoint(wa.X + (int)((wa.Width - Width * scale) / 2), wa.Y + (int)((wa.Height - Height * scale) / 2));
        }
    }

    /// <summary>1280 background at ≈100 %, the 2560 one once the window has more physical pixels.</summary>
    private void UpdateBackground()
    {
        var bg = this.FindControl<Image>("BgImage");
        if (bg is null) return;
        double physicalWidth = Bounds.Width * RenderScaling;
        bool large = physicalWidth > 1400 || Bounds.Height * RenderScaling > 800;
        if (large)
            bg.Source = _bgLarge ??= Load("nostalgia-bg-2560x1440.png");
        else
            bg.Source = _bgSmall ??= Load("nostalgia-bg-1280x720.png");
    }

    private static Bitmap Load(string file) =>
        new(AssetLoader.Open(new Uri("avares://Nostalgia/Assets/Brand/" + file)));

    // ── IUiShell ─────────────────────────────────────────────────────────────────────────────

    public async Task OpenUrlAsync(string url) => await Launcher.LaunchUriAsync(new Uri(url));

    public async Task OpenFolderAsync(string folder)
    {
        var f = await StorageProvider.TryGetFolderFromPathAsync(folder);
        if (f is not null) await Launcher.LaunchFileAsync(f);
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task CopyTextAsync(string text)
    {
        if (Clipboard is { } cb) await cb.SetTextAsync(text);
    }

    public void RestartInto(string exePath, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(exePath) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Process.Start(psi);
        Close();
    }
}
