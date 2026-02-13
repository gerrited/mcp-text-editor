using System.Text;

namespace McpTextEditor;

/// <summary>
/// Main editor form with a TextBox for editing documents.
/// All public methods that modify UI are thread-safe (auto-invoke to UI thread).
/// </summary>
public class EditorForm : Form
{
    private readonly TextBox _textBox;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _fileLabel;

    private string? _currentFilePath;
    private bool _isDirty;

    public EditorForm()
    {
        Text = "MCP Text Editor";
        Width = 900;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;

        // ── Menu ──
        var menuStrip = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add("&New", null, (_, _) => NewDocument());
        fileMenu.DropDownItems.Add("&Open...", null, (_, _) => OpenDocumentDialog());
        fileMenu.DropDownItems.Add("&Save", null, (_, _) => SaveDocument());
        fileMenu.DropDownItems.Add("Save &As...", null, (_, _) => SaveDocumentAsDialog());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("E&xit", null, (_, _) => Close());
        menuStrip.Items.Add(fileMenu);
        MainMenuStrip = menuStrip;

        // ── TextBox ──
        _textBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            Font = new Font("Consolas", 11f),
            MaxLength = int.MaxValue
        };
        _textBox.TextChanged += (_, _) =>
        {
            _isDirty = true;
            UpdateTitle();
        };

        // ── Status Bar ──
        _statusStrip = new StatusStrip();
        _fileLabel = new ToolStripStatusLabel("No file") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusLabel = new ToolStripStatusLabel("Ready") { TextAlign = ContentAlignment.MiddleRight };
        _statusStrip.Items.AddRange(new ToolStripItem[] { _fileLabel, _statusLabel });

        // ── Layout ──
        Controls.Add(_textBox);
        Controls.Add(menuStrip);
        Controls.Add(_statusStrip);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API  — Called by MCP tools (thread-safe)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Creates a new empty document.</summary>
    public void NewDocument()
    {
        if (InvokeRequired) { Invoke(NewDocument); return; }

        _textBox.Clear();
        _currentFilePath = null;
        _isDirty = false;
        UpdateTitle();
        SetStatus("New document created");
    }

    /// <summary>Opens a file from disk.</summary>
    public string OpenDocument(string filePath)
    {
        if (InvokeRequired) return (string)Invoke(() => OpenDocument(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        _textBox.Text = content;
        _currentFilePath = Path.GetFullPath(filePath);
        _isDirty = false;
        UpdateTitle();
        SetStatus($"Opened: {_currentFilePath}");
        return _currentFilePath;
    }

    /// <summary>Opens a file dialog and loads the selected file.</summary>
    public void OpenDocumentDialog()
    {
        if (InvokeRequired) { Invoke(OpenDocumentDialog); return; }

        using var dlg = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Open Text File"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            OpenDocument(dlg.FileName);
    }

    /// <summary>Saves the current document. Returns the saved file path.</summary>
    public string SaveDocument(string? filePath = null)
    {
        if (InvokeRequired) return (string)Invoke(() => SaveDocument(filePath));

        var path = filePath ?? _currentFilePath;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("No file path specified. Use save_as or provide a path.");

        File.WriteAllText(path, _textBox.Text, Encoding.UTF8);
        _currentFilePath = Path.GetFullPath(path);
        _isDirty = false;
        UpdateTitle();
        SetStatus($"Saved: {_currentFilePath}");
        return _currentFilePath;
    }

    /// <summary>Shows a Save-As dialog.</summary>
    public void SaveDocumentAsDialog()
    {
        if (InvokeRequired) { Invoke(SaveDocumentAsDialog); return; }

        using var dlg = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Save Text File"
        };
        if (!string.IsNullOrEmpty(_currentFilePath))
            dlg.FileName = Path.GetFileName(_currentFilePath);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            SaveDocument(dlg.FileName);
    }

    /// <summary>Returns the entire text content.</summary>
    public string GetText()
    {
        if (InvokeRequired) return (string)Invoke(GetText);
        return _textBox.Text;
    }

    /// <summary>Replaces the entire text content.</summary>
    public void SetText(string text)
    {
        if (InvokeRequired) { Invoke(() => SetText(text)); return; }
        _textBox.Text = text;
        SetStatus("Text replaced via MCP");
    }

    /// <summary>Inserts text at a given character position.</summary>
    public void InsertText(string text, int position)
    {
        if (InvokeRequired) { Invoke(() => InsertText(text, position)); return; }

        if (position < 0 || position > _textBox.TextLength)
            throw new ArgumentOutOfRangeException(nameof(position),
                $"Position {position} is out of range (0..{_textBox.TextLength}).");

        _textBox.SelectionStart = position;
        _textBox.SelectionLength = 0;
        _textBox.SelectedText = text;
        SetStatus($"Inserted {text.Length} chars at position {position}");
    }

    /// <summary>Returns the currently selected text (may be empty).</summary>
    public (string text, int start, int length) GetSelection()
    {
        if (InvokeRequired) return ((string, int, int))Invoke(GetSelection);
        return (_textBox.SelectedText, _textBox.SelectionStart, _textBox.SelectionLength);
    }

    /// <summary>Replaces the current selection with new text.</summary>
    public void ReplaceSelection(string newText)
    {
        if (InvokeRequired) { Invoke(() => ReplaceSelection(newText)); return; }

        if (_textBox.SelectionLength == 0)
            throw new InvalidOperationException("No text is currently selected.");

        _textBox.SelectedText = newText;
        SetStatus("Selection replaced via MCP");
    }

    /// <summary>Returns current file path, or null if untitled.</summary>
    public string? GetCurrentFilePath()
    {
        if (InvokeRequired) return (string?)Invoke(GetCurrentFilePath);
        return _currentFilePath;
    }

    /// <summary>Returns whether the document has unsaved changes.</summary>
    public bool GetIsDirty()
    {
        if (InvokeRequired) return (bool)Invoke(GetIsDirty);
        return _isDirty;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private void UpdateTitle()
    {
        var name = string.IsNullOrEmpty(_currentFilePath)
            ? "Untitled"
            : Path.GetFileName(_currentFilePath);
        var dirty = _isDirty ? " •" : "";
        Text = $"{name}{dirty} — MCP Text Editor";
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }
}
