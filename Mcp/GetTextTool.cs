using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class GetTextTool
{
    [McpServerTool(Name = "editor_get_text"),
     Description("Get the entire text content currently displayed in the editor.")]
    public static string GetText(MainWindow editor)
    {
        var text = editor.GetText();
        var filePath = editor.GetCurrentFilePath() ?? "(untitled)";
        var isDirty = editor.GetIsDirty();

        return $"File: {filePath}\nModified: {isDirty}\nLength: {text.Length} chars\n---\n{text}";
    }
}
