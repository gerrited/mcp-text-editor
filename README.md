# MCP Text Editor

A cross-platform text editor that can be remote-controlled via the **Model Context Protocol (MCP)** over stdio. Built with [Avalonia UI](https://avaloniaui.net/) and designed to work with Claude Desktop.

## Architecture

```
┌──────────────┐     stdio (stdin/stdout)     ┌─────────────────────┐
│ Claude       │ ◄──────────────────────────► │  McpTextEditor      │
│ Desktop      │    MCP JSON-RPC messages      │                     │
│              │                               │  ┌───────────────┐  │
│  (MCP Client)│                               │  │ MCP Server    │  │
│              │                               │  │ (background)  │  │
│              │                               │  └──────┬────────┘  │
│              │                               │         │ Dispatch  │
│              │                               │  ┌──────▼────────┐  │
│              │                               │  │ Avalonia UI   │  │
│              │                               │  │ (main thread) │  │
│              │                               │  └───────────────┘  │
└──────────────┘                               └─────────────────────┘
```

- **One process** runs both the Avalonia UI (main thread) and the MCP stdio server (background thread).
- Tool calls are dispatched to the UI thread via `Dispatcher.UIThread` for thread safety.
- All logging goes to **stderr** — stdout is exclusively for MCP protocol messages.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude Desktop](https://claude.ai/download) (for MCP integration)

## Build & Run

```bash
dotnet build -c Release
```

The built executable will be at:
```
bin/Release/net8.0/McpTextEditor
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
      "command": "/path/to/McpTextEditor/bin/Release/net8.0/McpTextEditor"
    }
  }
}
```

> **Important:** Replace the path with your actual build output path.

After editing the config, restart Claude Desktop. You should see "text-editor" in the MCP tools list (hammer icon).

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
├── Program.cs                # Entry point: starts UI + MCP server
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
