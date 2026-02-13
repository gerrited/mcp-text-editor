using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class ReplaceSelectionTool
{
    [McpServerTool(Name = "editor_replace_selection"),
     Description("Replace the currently selected text in the editor with new text. Fails if nothing is selected.")]
    public static string ReplaceSelection(
        MainWindow editor,
        [Description("The new text to replace the selection with.")] string newText)
    {
        try
        {
            editor.ReplaceSelection(newText);
            return $"Selection replaced with {newText.Length} chars.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
