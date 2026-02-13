using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace McpTextEditor;

public class App : Application
{
    /// <summary>
    /// Called once the main window has been created externally.
    /// We store a reference so MCP tools can access it.
    /// </summary>
    public MainWindow? EditorWindow { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            EditorWindow = new MainWindow();
            desktop.MainWindow = EditorWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
