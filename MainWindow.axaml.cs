using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace McpTextEditor;

/// <summary>
/// Main editor window with a TextBox for editing documents.
/// All public methods that modify UI are thread-safe (auto-dispatch to UI thread).
/// </summary>
public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _suppressDirty;

    public MainWindow()
    {
        InitializeComponent();

        Editor.TextChanged += (_, _) =>
        {
            if (_suppressDirty) return;
            _isDirty = true;
            UpdateTitle();
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  MENU EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════

    private void OnNew(object? sender, RoutedEventArgs e) => NewDocument();
    private async void OnOpen(object? sender, RoutedEventArgs e) => await OpenDocumentDialog();
    private void OnSave(object? sender, RoutedEventArgs e) => SaveDocument();
    private async void OnSaveAs(object? sender, RoutedEventArgs e) => await SaveDocumentAsDialog();
    private void OnExit(object? sender, RoutedEventArgs e) => Close();

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC API  — Called by MCP tools (thread-safe)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Creates a new empty document.</summary>
    public void NewDocument()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(NewDocument);
            return;
        }

        _suppressDirty = true;
        Editor.Text = string.Empty;
        _suppressDirty = false;
        _currentFilePath = null;
        _isDirty = false;
        UpdateTitle();
        SetStatus("New document created");
    }

    /// <summary>Opens a file from disk.</summary>
    public string OpenDocument(string filePath)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(() => OpenDocument(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        _suppressDirty = true;
        Editor.Text = content;
        _suppressDirty = false;
        _currentFilePath = Path.GetFullPath(filePath);
        _isDirty = false;
        UpdateTitle();
        SetStatus($"Opened: {_currentFilePath}");
        return _currentFilePath;
    }

    /// <summary>Opens a file dialog and loads the selected file.</summary>
    public async Task OpenDocumentDialog()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Text File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].TryGetLocalPath();
            if (path != null)
                OpenDocument(path);
        }
    }

    /// <summary>Saves the current document. Returns the saved file path.</summary>
    public string SaveDocument(string? filePath = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(() => SaveDocument(filePath));

        var path = filePath ?? _currentFilePath;
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("No file path specified. Use save_as or provide a path.");

        File.WriteAllText(path, Editor.Text, Encoding.UTF8);
        _currentFilePath = Path.GetFullPath(path);
        _isDirty = false;
        UpdateTitle();
        SetStatus($"Saved: {_currentFilePath}");
        return _currentFilePath;
    }

    /// <summary>Shows a Save-As dialog.</summary>
    public async Task SaveDocumentAsDialog()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Text File",
            SuggestedFileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "untitled.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            var path = file.TryGetLocalPath();
            if (path != null)
                SaveDocument(path);
        }
    }

    /// <summary>Returns the entire text content.</summary>
    public string GetText()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(GetText);
        return Editor.Text ?? string.Empty;
    }

    /// <summary>Replaces the entire text content.</summary>
    public void SetText(string text)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => SetText(text));
            return;
        }

        Editor.Text = text;
        SetStatus("Text replaced via MCP");
    }

    /// <summary>Inserts text at a given character position.</summary>
    public void InsertText(string text, int position)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => InsertText(text, position));
            return;
        }

        var current = Editor.Text ?? string.Empty;
        if (position < 0 || position > current.Length)
            throw new ArgumentOutOfRangeException(nameof(position),
                $"Position {position} is out of range (0..{current.Length}).");

        Editor.Text = current.Insert(position, text);
        Editor.CaretIndex = position + text.Length;
        SetStatus($"Inserted {text.Length} chars at position {position}");
    }

    /// <summary>Returns the currently selected text (may be empty).</summary>
    public (string text, int start, int length) GetSelection()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(GetSelection);

        var selected = Editor.SelectedText ?? string.Empty;
        var start = Editor.SelectionStart;
        var length = Editor.SelectionEnd - Editor.SelectionStart;
        return (selected, start, length);
    }

    /// <summary>Replaces the current selection with new text.</summary>
    public void ReplaceSelection(string newText)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => ReplaceSelection(newText));
            return;
        }

        var selStart = Editor.SelectionStart;
        var selEnd = Editor.SelectionEnd;
        if (selStart == selEnd)
            throw new InvalidOperationException("No text is currently selected.");

        var current = Editor.Text ?? string.Empty;
        Editor.Text = current[..selStart] + newText + current[selEnd..];
        Editor.SelectionStart = selStart;
        Editor.SelectionEnd = selStart + newText.Length;
        SetStatus("Selection replaced via MCP");
    }

    /// <summary>Returns current file path, or null if untitled.</summary>
    public string? GetCurrentFilePath()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(GetCurrentFilePath);
        return _currentFilePath;
    }

    /// <summary>Returns whether the document has unsaved changes.</summary>
    public bool GetIsDirty()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.Invoke(GetIsDirty);
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
        var dirty = _isDirty ? " \u2022" : "";
        Title = $"{name}{dirty} \u2014 MCP Text Editor";
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
    }
}
