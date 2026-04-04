using Microsoft.UI.Xaml;

namespace PhotoDuplicates;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
        UnhandledException += (_, e) =>
        {
            System.Diagnostics.Trace.WriteLine("[PhotoDuplicates] " + e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
