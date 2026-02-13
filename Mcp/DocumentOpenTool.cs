using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class DocumentOpenTool
{
    [McpServerTool("editor_document_open"),
     Description("Open an existing text file in the editor. Provide the full file path.")]
    public static string DocumentOpen(
        EditorForm editor,
        [Description("Full path to the text file to open.")] string filePath)
    {
        try
        {
            var fullPath = editor.OpenDocument(filePath);
            return $"Opened: {fullPath}";
        }
        catch (FileNotFoundException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
