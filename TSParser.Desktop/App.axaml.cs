using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using TSParser.Desktop.ViewModels;
using TSParser.Desktop.Views;

namespace TSParser.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += async (_, e) =>
            {
                if (mainWindow.DataContext is IAsyncDisposable disposable)
                {
                    e.Cancel = true;
                    await disposable.DisposeAsync().ConfigureAwait(true);
                    desktop.Shutdown();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}