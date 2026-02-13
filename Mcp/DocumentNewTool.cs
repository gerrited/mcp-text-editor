using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpTextEditor.Mcp;

[McpServerToolType]
public static class DocumentNewTool
{
    [McpServerTool(Name = "editor_document_new"),
     Description("Create a new empty document in the editor, discarding the current content.")]
    public static string DocumentNew(MainWindow editor)
    {
        editor.NewDocument();
        return "New document created.";
    }
}
