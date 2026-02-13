# Streamable HTTP Transport Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add configurable Streamable HTTP transport with API key authentication and HTTPS support to the MCP text editor, selectable via `appsettings.json`.

**Architecture:** Single host, conditional transport. `appsettings.json` sets `Transport:Mode` to `"Stdio"` or `"Http"`. Stdio builds a `Host` with `WithStdioServerTransport()`. Http builds a `WebApplication` with `WithHttpTransport()`, bearer token auth, and Kestrel HTTPS. Only one transport runs at a time.

**Tech Stack:** .NET 10, ModelContextProtocol C# SDK 0.8.0-preview.1, ASP.NET Core (Kestrel), Avalonia 11.3.11

**Design doc:** `docs/plans/2026-02-13-streamable-http-transport-design.md`

---

### Task 1: Add ASP.NET Core framework reference

**Files:**
- Modify: `McpTextEditor.csproj:9-17`

**Step 1: Add the framework reference**

The HTTP transport requires `Microsoft.AspNetCore.App`. Add it as a `FrameworkReference` (not a PackageReference — it's a shared framework).

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

Add this as a new `<ItemGroup>` after the existing `<ItemGroup>` block (after line 17, before `</Project>`).

**Step 2: Verify it builds**

Run: `dotnet build`
Expected: Build succeeds with no errors.

**Step 3: Commit**

```bash
git add McpTextEditor.csproj
git commit -m "feat: add ASP.NET Core framework reference for HTTP transport"
```

---

### Task 2: Create TransportConfig.cs

**Files:**
- Create: `TransportConfig.cs`

**Step 1: Create the strongly-typed config classes**

This file defines the config model that maps to the `Transport` section in `appsettings.json`.

```csharp
namespace McpTextEditor;

public class TransportConfig
{
    public string Mode { get; set; } = "Stdio";
    public HttpTransportConfig Http { get; set; } = new();
}

public class HttpTransportConfig
{
    public string Url { get; set; } = "https://localhost:5000";
    public string McpPath { get; set; } = "/mcp";
    public string ApiKey { get; set; } = string.Empty;
    public CertificateConfig Certificate { get; set; } = new();
}

public class CertificateConfig
{
    public string Path { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```

**Step 2: Verify it builds**

Run: `dotnet build`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add TransportConfig.cs
git commit -m "feat: add strongly-typed transport configuration classes"
```

---

### Task 3: Create ApiKeyAuthHandler.cs

**Files:**
- Create: `ApiKeyAuthHandler.cs`

**Step 1: Create the authentication handler**

This handler extracts the `Authorization: Bearer <token>` header and compares it against the configured API key using a constant-time comparison.

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace McpTextEditor;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly HttpTransportConfig _config;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<HttpTransportConfig> config)
        : base(options, logger, encoder)
    {
        _config = config.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            // No API key configured — allow all requests
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Bearer token."));
        }

        var token = authHeader["Bearer ".Length..].Trim();

        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(token),
                System.Text.Encoding.UTF8.GetBytes(_config.ApiKey)))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "api-key-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**Key details:**
- Uses `CryptographicOperations.FixedTimeEquals` for constant-time comparison (prevents timing attacks).
- If `ApiKey` is empty in config, auth is skipped (allows unauthenticated access for dev/testing).
- Creates a `ClaimsPrincipal` on success so ASP.NET Core authorization middleware works.

**Step 2: Verify it builds**

Run: `dotnet build`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add ApiKeyAuthHandler.cs
git commit -m "feat: add API key authentication handler for HTTP transport"
```

---

### Task 4: Create appsettings.json

**Files:**
- Create: `appsettings.json`

**Step 1: Create the default config file**

```json
{
  "Transport": {
    "Mode": "Stdio",
    "Http": {
      "Url": "https://localhost:5000",
      "McpPath": "/mcp",
      "ApiKey": "change-me",
      "Certificate": {
        "Path": "",
        "Password": ""
      }
    }
  }
}
```

**Step 2: Ensure it's copied to output**

Add to `McpTextEditor.csproj` inside the first `<ItemGroup>` (or a new one):

```xml
  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

**Step 3: Verify it builds**

Run: `dotnet build`
Expected: Build succeeds and `appsettings.json` appears in `bin/Debug/net10.0/`.

**Step 4: Commit**

```bash
git add appsettings.json McpTextEditor.csproj
git commit -m "feat: add appsettings.json with default transport configuration"
```

---

### Task 5: Refactor Program.cs — conditional transport

**Files:**
- Modify: `Program.cs` (full rewrite of lines 1-76)

This is the core change. The MCP background thread now reads config and branches between stdio and HTTP host setup.

**Step 1: Rewrite Program.cs**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using McpTextEditor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

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

    var host = builder.Build();

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
        kestrel.ListenAnyIP(uri.Port, listenOptions =>
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
        });
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

    var app = builder.Build();

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapMcp(httpConfig.McpPath);

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
```

**Key changes from the original:**
- Config loading at the top of the background thread.
- `RunStdioTransport()` — extracted from the original inline code, behavior unchanged.
- `RunHttpTransport()` — new, builds a `WebApplication` with Kestrel HTTPS, API key auth, and `WithHttpTransport()`.
- Both methods register the same `MainWindow` singleton and tools from the same assembly.

**Step 2: Verify stdio mode still works (default)**

Run: `dotnet build`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add Program.cs
git commit -m "feat: conditional transport in Program.cs — stdio or HTTP based on appsettings.json"
```

---

### Task 6: Manual smoke test

**Step 1: Verify stdio mode (default config)**

Ensure `appsettings.json` has `"Mode": "Stdio"`. Run the app:

Run: `dotnet run`
Expected: Avalonia window opens. App communicates over stdin/stdout as before.

**Step 2: Generate a dev certificate (if needed)**

Run: `dotnet dev-certs https --trust`
Expected: Dev certificate trusted.

**Step 3: Switch to HTTP mode**

Edit `appsettings.json`: set `"Mode": "Http"`. Leave `Certificate.Path` empty to use the dev cert. Set `"ApiKey": "test-key-123"`.

Run: `dotnet run`
Expected: Avalonia window opens. Stderr shows `[MCP] HTTP server starting on https://localhost:5000/mcp`.

**Step 4: Test authentication — missing token**

Run: `curl -k https://localhost:5000/mcp`
Expected: 401 Unauthorized.

**Step 5: Test authentication — valid token**

Run: `curl -k -H "Authorization: Bearer test-key-123" -X POST https://localhost:5000/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}},"id":1}'`
Expected: 200 OK with MCP initialize response containing server capabilities.

**Step 6: Test authentication — wrong token**

Run: `curl -k -H "Authorization: Bearer wrong-key" -X POST https://localhost:5000/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}},"id":1}'`
Expected: 401 Unauthorized.

**Step 7: Commit (no code changes — just verifying)**

No commit needed. If any fixes were required during testing, commit them here.

---

### Task 7: Update README

**Files:**
- Modify: `README.md`

**Step 1: Add HTTP transport documentation**

Add a section documenting:
- The new `appsettings.json` configuration options
- How to switch between Stdio and Http mode
- How to configure HTTPS certificates
- How to set the API key
- Example `curl` commands for testing
- Updated Claude Desktop config for HTTP mode (using `--url` flag or streamable HTTP client config)

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add HTTP transport configuration to README"
```
