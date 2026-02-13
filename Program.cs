using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using McpTextEditor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

// ─── IMPORTANT ───────────────────────────────────────────────────────────
// Transport mode is configured in appsettings.json:
//   "Stdio" (default) — stdout reserved for MCP protocol, logging to stderr.
//   "Http"            — ASP.NET Core Kestrel with bearer token auth.
// ──────────────────────────────────────────────────────────────────────────

// Avalonia requires the main thread; we start the MCP server on a background thread.
var uiReady = new ManualResetEventSlim(false);
MainWindow? editorWindow = null;

// 1) Start MCP server on a background thread
var mcpThread = new Thread(() =>
{
    // Wait until the UI is ready so we can pass the window reference to tools
    uiReady.Wait();

    // Load configuration to determine transport mode
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var transportConfig = new TransportConfig();
    config.GetSection("Transport").Bind(transportConfig);

    if (transportConfig.Mode.Equals("Http", StringComparison.OrdinalIgnoreCase))
    {
        RunHttpTransport(editorWindow!, transportConfig, config);
    }
    else
    {
        if (!transportConfig.Mode.Equals("Stdio", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"[MCP] Unknown transport mode '{transportConfig.Mode}', defaulting to Stdio.");
        }
        RunStdioTransport(editorWindow!);
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

// ─── Transport Runners ──────────────────────────────────────────────────

static void RunStdioTransport(MainWindow editorWindow)
{
    var builder = Host.CreateApplicationBuilder();

    // Redirect all logging to stderr (stdout is for MCP protocol)
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddSingleton<MainWindow>(editorWindow);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(MainWindow).Assembly);

    using var host = builder.Build();

    try
    {
        host.Run();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] Stdio server error: {ex.Message}");
    }
}

static void RunHttpTransport(MainWindow editorWindow, TransportConfig transportConfig, IConfiguration config)
{
    var httpConfig = transportConfig.Http;

    var builder = WebApplication.CreateBuilder();
    builder.Configuration.AddConfiguration(config);

    // Configure Kestrel to listen on the configured URL
    builder.WebHost.UseKestrel(kestrel =>
    {
        var uri = new Uri(httpConfig.Url);
        var isLocalhost = uri.Host is "localhost" or "127.0.0.1" or "::1";

        void ConfigureListener(Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions)
        {
            if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(httpConfig.Certificate.Path))
                {
                    listenOptions.UseHttps(httpConfig.Certificate.Path, httpConfig.Certificate.Password);
                }
                else
                {
                    // Use dev cert if no certificate configured
                    listenOptions.UseHttps();
                }
            }
        }

        if (isLocalhost)
        {
            kestrel.ListenLocalhost(uri.Port, ConfigureListener);
        }
        else
        {
            kestrel.Listen(IPAddress.Parse(uri.Host), uri.Port, ConfigureListener);
        }
    });

    // Register editor window and HTTP config
    builder.Services.AddSingleton<MainWindow>(editorWindow);
    builder.Services.Configure<HttpTransportConfig>(config.GetSection("Transport:Http"));

    // Authentication
    builder.Services.AddAuthentication("ApiKey")
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", null);
    builder.Services.AddAuthorization();

    // MCP server with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(httpOptions =>
        {
            httpOptions.Stateless = false;
            httpOptions.IdleTimeout = TimeSpan.FromMinutes(30);
        })
        .AddAuthorizationFilters()
        .WithToolsFromAssembly(typeof(MainWindow).Assembly);

    using var app = builder.Build();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapMcp(httpConfig.McpPath);

    if (string.IsNullOrEmpty(httpConfig.ApiKey))
    {
        Console.Error.WriteLine("[MCP] WARNING: No API key configured. HTTP endpoint is open to all requests.");
    }

    try
    {
        Console.Error.WriteLine($"[MCP] HTTP server starting on {httpConfig.Url}{httpConfig.McpPath}");
        app.Run();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MCP] HTTP server error: {ex.Message}");
    }
}
