using McpTextEditor;
using McpTextEditor.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Threading;

// ─── IMPORTANT ───────────────────────────────────────────────────────────
// This app uses stdio for MCP communication.
// stdout is RESERVED for the MCP protocol – all logging goes to stderr.
// Claude Desktop launches this as:  "command": "McpTextEditor.exe"
// ──────────────────────────────────────────────────────────────────────────

// WinForms requires STA thread; we start the MCP server on a background thread.
var uiReady = new ManualResetEventSlim(false);
EditorForm? editorForm = null;

// 1) Start MCP server on a background thread
var mcpThread = new Thread(() =>
{
    // Wait until the UI is ready so we can pass the form reference to tools
    uiReady.Wait();

    var builder = Host.CreateApplicationBuilder();

    // Redirect all logging to stderr (stdout is for MCP protocol)
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);

    // Register the editor form as a singleton so MCP tools can access it
    builder.Services.AddSingleton(editorForm!);

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
    if (editorForm != null && !editorForm.IsDisposed)
    {
        editorForm.Invoke(() => editorForm.Close());
    }
})
{
    IsBackground = true,
    Name = "MCP-Server"
};
mcpThread.Start();

// 2) Start WinForms on the main (STA) thread
ApplicationConfiguration.Initialize();
editorForm = new EditorForm();

// Signal MCP thread that the form is ready
uiReady.Set();

Application.Run(editorForm);
