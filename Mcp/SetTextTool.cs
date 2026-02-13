using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class SetTextTool
{
    [McpServerTool(Name = "editor_set_text"),
     Description("Replace the entire text content in the editor with new text.")]
    public static string SetText(
        MainWindow editor,
        [Description("The new text content to set in the editor.")] string text)
    {
        editor.SetText(text);
        return $"Text replaced. New length: {text.Length} chars.";
    }
}
