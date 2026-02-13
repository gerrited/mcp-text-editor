# Streamable HTTP Transport with Authentication

## Summary

Add configurable Streamable HTTP transport to the MCP text editor, alongside the existing stdio transport. The transport mode is selected via `appsettings.json`. HTTP mode includes bearer token (API key) authentication and Kestrel HTTPS support.

## Architecture

Single host, conditional transport. `appsettings.json` determines whether a `Host` (stdio) or `WebApplication` (HTTP) is built. Only one transport runs at a time. MCP tools and the `MainWindow` singleton are registered identically in either case.

```
Program.cs
  │
  ├─ Mode: Stdio → Host + WithStdioServerTransport()
  │
  └─ Mode: Http  → WebApplication + WithHttpTransport()
                    + Bearer Auth + Kestrel HTTPS
                    │
                    Both share:
                      MainWindow (singleton)
                      MCP Tools (from assembly)
```

## Configuration

`appsettings.json`:

```json
{
  "Transport": {
    "Mode": "Stdio",
    "Http": {
      "Url": "https://localhost:5000",
      "McpPath": "/mcp",
      "ApiKey": "your-secret-api-key-here",
      "Certificate": {
        "Path": "./cert.pfx",
        "Password": "cert-password"
      }
    }
  }
}
```

- **Mode**: `"Stdio"` (default) or `"Http"`.
- **Http.Url**: Kestrel listen URL (scheme + host + port).
- **Http.McpPath**: Endpoint path for `MapMcp()`.
- **Http.ApiKey**: Static bearer token for authentication.
- **Http.Certificate**: PFX file path and password for HTTPS.

When Mode is Stdio, the Http section is ignored.

## Authentication (HTTP mode)

1. Client sends `Authorization: Bearer <api-key>` header.
2. Custom `ApiKeyAuthHandler` (extends `AuthenticationHandler<AuthenticationSchemeOptions>`) validates the token against `Transport:Http:ApiKey`.
3. Valid → request proceeds. Missing/invalid → 401 Unauthorized.
4. SDK's `.AddAuthorizationFilters()` is wired up for future per-tool `[Authorize]` attribute support.

## Program.cs changes

The dual-thread architecture is preserved: MCP host on background thread, Avalonia on main thread. The background thread branches on config:

**Stdio mode** — current behavior, unchanged.

**Http mode** — builds `WebApplication` with:
- Custom API key auth handler
- ASP.NET Core authorization middleware
- Kestrel HTTPS with PFX certificate
- `WithHttpTransport()` + `AddAuthorizationFilters()`
- `MapMcp()` at configured path

## New files

| File | Purpose |
|------|---------|
| `ApiKeyAuthHandler.cs` | Custom `AuthenticationHandler` validating bearer token against configured API key |
| `TransportConfig.cs` | Strongly-typed config classes for `appsettings.json` binding |
| `appsettings.json` | Default configuration (Stdio mode) |

## Modified files

| File | Change |
|------|--------|
| `Program.cs` | Read config, branch on transport mode, build appropriate host |
| `McpTextEditor.csproj` | Add `Microsoft.AspNetCore.App` framework reference |

## Unchanged

- All 8 MCP tools
- MainWindow.axaml and MainWindow.axaml.cs
- App.axaml and App.axaml.cs
- Thread-safe dispatch pattern
- Avalonia UI thread startup
