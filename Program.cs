using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using McpTextEditor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ─── IMPORTANT ───────────────────────────────────────────────────────────
// This app uses stdio for MCP communication.
// stdout is RESERVED for the MCP protocol – all logging goes to stderr.
// Claude Desktop launches this as:  "command": "McpTextEditor"
// ──────────────────────────────────────────────────────────────────────────

// Avalonia requires the main thread; we start the MCP server on a background thread.
var uiReady = new ManualResetEventSlim(false);
MainWindow? editorWindow = null;

// 1) Start MCP server on a background thread
var mcpThread = new Thread(() =>
{
    // Wait until the UI is ready so we can pass the window reference to tools
    uiReady.Wait();

    var builder = Host.CreateApplicationBuilder();

    // Redirect all logging to stderr (stdout is for MCP protocol)
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);

    // Register the editor window as a singleton so MCP tools can access it
    builder.Services.AddSingleton(editorWindow!);

    // Register MCP server with stdio transport
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "mcp-text-editor",
            Version = "1.0.0"
        };
        options.Capabilities = new()
        {
            Tools = new() { }
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

    var host = builder.Build();

    try
    {
        host.Run();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] Server error: {ex.Message}");
    }

    // If MCP server shuts down, close the UI
    if (editorWindow != null)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => editorWindow.Close());
    }
})
{
    IsBackground = true,
    Name = "MCP-Server"
};
mcpThread.Start();

// 2) Start Avalonia on the main thread
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .StartWithClassicDesktopLifetime(args, lifetime =>
    {
        lifetime.Startup += (_, _) =>
        {
            if (Application.Current is App app)
            {
                editorWindow = app.EditorWindow;
                uiReady.Set();
            }
        };
    });
