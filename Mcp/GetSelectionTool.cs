using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class GetSelectionTool
{
    [McpServerTool("editor_get_selection"),
     Description("Get the currently selected text in the editor, along with its start position and length.")]
    public static string GetSelection(EditorForm editor)
    {
        var (text, start, length) = editor.GetSelection();

        if (length == 0)
            return "No text is currently selected.";

        return $"Selection start: {start}\nSelection length: {length}\n---\n{text}";
    }
}
