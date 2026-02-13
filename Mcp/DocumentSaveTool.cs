using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class DocumentSaveTool
{
    [McpServerTool("editor_document_save"),
     Description("Save the current document. Optionally provide a file path for 'Save As'. If no path is given, saves to the current file.")]
    public static string DocumentSave(
        EditorForm editor,
        [Description("Optional file path. If omitted, saves to the current file path.")] string? filePath = null)
    {
        try
        {
            var savedPath = editor.SaveDocument(filePath);
            return $"Saved: {savedPath}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
