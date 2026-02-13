# MCP Text Editor

A cross-platform text editor that can be remote-controlled via the **Model Context Protocol (MCP)**. Supports **stdio** and **Streamable HTTP** transports, configurable via `appsettings.json`. Built with [Avalonia UI](https://avaloniaui.net/) and designed to work with Claude Desktop and other MCP clients.

## Architecture

```
┌──────────────┐  stdio or Streamable HTTP     ┌─────────────────────┐
│ MCP Client   │ ◄──────────────────────────►  │  McpTextEditor      │
│ (Claude      │    MCP JSON-RPC messages      │                     │
│  Desktop,    │                               │  ┌───────────────┐  │
│  etc.)       │                               │  │ MCP Server    │  │
│              │                               │  │ (background)  │  │
│              │                               │  └──────┬────────┘  │
│              │                               │         │ Dispatch  │
│              │                               │  ┌──────▼────────┐  │
│              │                               │  │ Avalonia UI   │  │
│              │                               │  │ (main thread) │  │
│              │                               │  └───────────────┘  │
└──────────────┘                               └─────────────────────┘
```

- **One process** runs both the Avalonia UI (main thread) and the MCP server (background thread).
- **Transport mode** is configured in `appsettings.json`: `Stdio` (default) or `Http`.
- Tool calls are dispatched to the UI thread via `Dispatcher.UIThread` for thread safety.
- In stdio mode, logging goes to **stderr** — stdout is exclusively for MCP protocol messages.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Claude Desktop](https://claude.ai/download) (for MCP integration)

## Build & Run

```bash
dotnet build -c Release
```

The built executable will be at:
```
bin/Release/net10.0/McpTextEditor
```

### Standalone run (without Claude)

```bash
dotnet run
```

The editor window opens. You can use it as a normal text editor. The MCP server runs in the background on stdio but won't receive any messages without a client.

## Claude Desktop Configuration

Add this to your `claude_desktop_config.json`:

| OS | Config location |
|----|-----------------|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| Linux | `~/.config/Claude/claude_desktop_config.json` |

```json
{
  "mcpServers": {
    "text-editor": {
      "command": "/path/to/McpTextEditor/bin/Release/net10.0/McpTextEditor"
    }
  }
}
```

> **Important:** Replace the path with your actual build output path.

After editing the config, restart Claude Desktop. You should see "text-editor" in the MCP tools list (hammer icon).

## Transport Configuration

Transport mode is configured in `appsettings.json`:

```json
{
  "Transport": {
    "Mode": "Stdio",
    "Http": {
      "Url": "https://localhost:5000",
      "McpPath": "/mcp",
      "ApiKey": "",
      "Certificate": {
        "Path": "",
        "Password": ""
      }
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `Mode` | `"Stdio"` or `"Http"` | `"Stdio"` |
| `Http.Url` | Listen URL (scheme + host + port) | `https://localhost:5000` |
| `Http.McpPath` | MCP endpoint path | `/mcp` |
| `Http.ApiKey` | Bearer token for authentication (empty = no auth) | `""` |
| `Http.Certificate.Path` | Path to PFX certificate file (empty = dev cert) | `""` |
| `Http.Certificate.Password` | PFX certificate password | `""` |

### Stdio Mode (default)

No additional configuration needed. Works with Claude Desktop out of the box.

### HTTP Mode

1. Set `"Mode": "Http"` in `appsettings.json`
2. Set an API key: `"ApiKey": "your-secret-key"`
3. For HTTPS, either:
   - Use the .NET dev cert: `dotnet dev-certs https --trust` (no certificate config needed)
   - Or provide a PFX certificate via `Certificate.Path` and `Certificate.Password`

> **Security:** Store the API key and certificate password outside of source control. Use `appsettings.Development.json`, environment variables, or [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

Clients connect with a Bearer token:

```bash
# Test the endpoint
curl -k -H "Authorization: Bearer your-secret-key" \
  -X POST https://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}},"id":1}'
```

### Connecting Claude Desktop to HTTP Mode

Claude Desktop doesn't natively support bearer token authentication over Streamable HTTP. Use the [`mcp-remote`](https://www.npmjs.com/package/mcp-remote) npm package as a bridge:

```json
{
  "mcpServers": {
    "text-editor": {
      "command": "npx",
      "args": [
        "mcp-remote",
        "https://localhost:5000/mcp",
        "--header",
        "Authorization: Bearer your-secret-key"
      ],
      "env": {
        "NODE_EXTRA_CA_CERTS": "/path/to/dev-cert.pem"
      }
    }
  }
}
```

> **HTTPS with dev certs:** Node.js doesn't trust the .NET dev certificate by default. Either export it as PEM (`dotnet dev-certs https --export-path ./dev-cert.pem --format Pem --no-password`) and set `NODE_EXTRA_CA_CERTS`, or use `"NODE_OPTIONS": "--use-system-ca"` (Node.js 23.8+), or switch to plain `http://` for localhost.

Requires [Node.js](https://nodejs.org/) (for npx).

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `editor_document_new` | Create a new empty document |
| `editor_document_open` | Open a text file by path |
| `editor_document_save` | Save the document (optionally with a new path) |
| `editor_get_text` | Get the full editor content |
| `editor_set_text` | Replace the full editor content |
| `editor_insert_text` | Insert text at a specific position |
| `editor_get_selection` | Get the currently selected text |
| `editor_replace_selection` | Replace the current selection |

## Example Prompts for Claude

Once configured, you can tell Claude things like:

- *"Open the file ~/notes.txt in the editor"*
- *"Read the current editor content and fix any spelling errors"*
- *"Create a new document and write a summary of [topic]"*
- *"Insert a header at the beginning of the document"*
- *"Save the document as ~/Desktop/output.txt"*

## Troubleshooting

### Editor window doesn't appear
Make sure you're running the built executable directly. The `command` in the config must point to the built binary.

### MCP tools not showing in Claude Desktop
1. Check that `claude_desktop_config.json` is valid JSON
2. Ensure the path to the executable is correct
3. Restart Claude Desktop completely
4. Check Claude Desktop logs for errors

### NuGet restore issues
If the `ModelContextProtocol` package can't be found, it may be in preview. Try:
```bash
dotnet add package ModelContextProtocol --prerelease
```

## Project Structure

```
├── McpTextEditor.csproj      # Project file with NuGet references
├── Program.cs                # Entry point: starts UI + MCP server (transport selection)
├── appsettings.json          # Transport configuration (Stdio/Http)
├── TransportConfig.cs        # Strongly-typed config classes
├── ApiKeyAuthHandler.cs      # Bearer token authentication handler
├── App.axaml                 # Avalonia application definition
├── App.axaml.cs              # Application startup, creates MainWindow
├── MainWindow.axaml          # Editor UI layout (XAML)
├── MainWindow.axaml.cs       # Editor logic with thread-safe API
└── Mcp/
    ├── DocumentNewTool.cs
    ├── DocumentOpenTool.cs
    ├── DocumentSaveTool.cs
    ├── GetTextTool.cs
    ├── SetTextTool.cs
    ├── InsertTextTool.cs
    ├── GetSelectionTool.cs
    └── ReplaceSelectionTool.cs
```

## License

MIT
