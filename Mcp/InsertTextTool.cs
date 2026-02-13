using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class InsertTextTool
{
    [McpServerTool("editor_insert_text"),
     Description("Insert text at a specific character position in the editor. Position 0 inserts at the beginning.")]
    public static string InsertText(
        EditorForm editor,
        [Description("The text to insert.")] string text,
        [Description("The character position (0-based) where text should be inserted.")] int position)
    {
        try
        {
            editor.InsertText(text, position);
            return $"Inserted {text.Length} chars at position {position}.";
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
