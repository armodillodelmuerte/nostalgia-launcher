using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Nostalgia.Launcher.ViewModels;
using Nostalgia.Launcher.Views;

namespace Nostalgia.Launcher;

public partial class App : Application
{
    /// <summary>Set by Program before start (and by UI tests).</summary>
    public static Func<MainViewModel>? CreateViewModel { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && CreateViewModel is not null)
        {
            var vm = CreateViewModel();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            window.Opened += async (_, _) => await vm.InitializeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
