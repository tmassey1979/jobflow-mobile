using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace JobFlow.Mobile;

public partial class DocumentViewerPage : ContentPage
{
    private string _source;
    private bool _isEditing;

    public DocumentViewerPage(string title, string subtitle, string? sourcePath, string body)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.Text = string.IsNullOrWhiteSpace(sourcePath) ? "Source path not available" : sourcePath;
        _source = body ?? string.Empty;
        SourceEditor.Text = _source;
        ApplyPreview();
        StyleModeButtons();
    }

    public DocumentViewerPage(string title, string subtitle, string statusMessage)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.IsVisible = false;
        StatusLabel.IsVisible = true;
        StatusLabel.Text = statusMessage;
        _source = string.Empty;
        SourceEditor.Text = string.Empty;
        PreviewLabel.Text = string.Empty;
        ViewButton.IsEnabled = false;
        EditButton.IsEnabled = false;
        CopyButton.IsEnabled = false;
    }

    private void OnViewClicked(object? sender, EventArgs e)
    {
        _isEditing = false;
        _source = SourceEditor.Text ?? string.Empty;
        ApplyPreview();
        PreviewScroll.IsVisible = true;
        SourceEditor.IsVisible = false;
        StyleModeButtons();
    }

    private void OnEditClicked(object? sender, EventArgs e)
    {
        _isEditing = true;
        SourceEditor.Text = _source;
        PreviewScroll.IsVisible = false;
        SourceEditor.IsVisible = true;
        StyleModeButtons();
    }

    private void OnSourceChanged(object? sender, TextChangedEventArgs e)
    {
        _source = e.NewTextValue ?? string.Empty;
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        var text = _isEditing ? (SourceEditor.Text ?? string.Empty) : _source;
        if (string.IsNullOrEmpty(text))
        {
            await DisplayAlertAsync("Nothing to copy", "This document is empty.", "OK");
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(text);
            await DisplayAlertAsync("Copied", "Document text copied to the clipboard.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Copy failed", "Clipboard was unavailable.", "OK");
        }
    }

    private void ApplyPreview()
    {
        // Lightweight markdown-ish preview: keep structure readable without a full renderer.
        // Headers, lists, and code fences are lightly normalized for mobile reading.
        var text = _source;
        if (string.IsNullOrWhiteSpace(text))
        {
            PreviewLabel.Text = "(empty document)";
            return;
        }

        // Preserve newlines; strip excessive fence noise for readability.
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var rendered = new System.Text.StringBuilder(text.Length + 64);
        var inFence = false;
        foreach (var raw in lines)
        {
            var line = raw;
            if (line.StartsWith("```"))
            {
                inFence = !inFence;
                rendered.AppendLine(inFence ? "—— code ——" : "——————");
                continue;
            }

            if (!inFence)
            {
                if (line.StartsWith("# "))
                    line = line[2..].ToUpperInvariant();
                else if (line.StartsWith("## "))
                    line = line[3..];
                else if (line.StartsWith("### "))
                    line = line[4..];
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                    line = "• " + line[2..];
            }

            rendered.AppendLine(line);
        }

        PreviewLabel.Text = rendered.ToString().TrimEnd();
    }

    private void StyleModeButtons()
    {
        if (_isEditing)
        {
            EditButton.BackgroundColor = Color.FromArgb("#2563EB");
            EditButton.TextColor = Colors.White;
            ViewButton.BackgroundColor = Color.FromArgb("#E9F0FF");
            ViewButton.TextColor = Color.FromArgb("#2563EB");
        }
        else
        {
            ViewButton.BackgroundColor = Color.FromArgb("#2563EB");
            ViewButton.TextColor = Colors.White;
            EditButton.BackgroundColor = Color.FromArgb("#E9F0FF");
            EditButton.TextColor = Color.FromArgb("#2563EB");
        }
    }
}
