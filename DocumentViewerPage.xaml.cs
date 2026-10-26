namespace JobFlow.Mobile;

public partial class DocumentViewerPage : ContentPage
{
    public DocumentViewerPage(string title, string subtitle, string? sourcePath, string body)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.Text = string.IsNullOrWhiteSpace(sourcePath) ? "Source path not available" : sourcePath;
        BodyLabel.Text = body;
    }

    public DocumentViewerPage(string title, string subtitle, string statusMessage)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.IsVisible = false;
        StatusLabel.IsVisible = true;
        StatusLabel.Text = statusMessage;
        BodyLabel.Text = string.Empty;
    }
}
