namespace JobFlow.Mobile;

public partial class DocumentViewerPage : ContentPage
{
    private string _body;
    private bool _isEditing;

    public DocumentViewerPage(string title, string subtitle, string? sourcePath, string body)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.Text = string.IsNullOrWhiteSpace(sourcePath) ? "Source path not available" : sourcePath;
        _body = body ?? string.Empty;
        BodyLabel.Text = _body;
        BodyEditor.Text = _body;
        SetTab(view: true);
    }

    public DocumentViewerPage(string title, string subtitle, string statusMessage)
    {
        InitializeComponent();
        Title = title;
        SubtitleLabel.Text = subtitle;
        PathLabel.IsVisible = false;
        StatusLabel.IsVisible = true;
        StatusLabel.Text = statusMessage;
        _body = string.Empty;
        BodyLabel.Text = string.Empty;
        BodyEditor.Text = string.Empty;
        ViewTabButton.IsEnabled = false;
        EditTabButton.IsEnabled = false;
        SetTab(view: true);
    }

    private void OnViewTabClicked(object sender, EventArgs e)
    {
        if (_isEditing)
        {
            // Capture edits when switching back to view.
            _body = BodyEditor.Text ?? string.Empty;
            BodyLabel.Text = _body;
        }
        SetTab(view: true);
    }

    private void OnEditTabClicked(object sender, EventArgs e)
    {
        BodyEditor.Text = _body;
        SetTab(view: false);
    }

    private void SetTab(bool view)
    {
        _isEditing = !view;
        ViewScroll.IsVisible = view;
        BodyEditor.IsVisible = !view;

        if (view)
        {
            ViewTabButton.BackgroundColor = Color.FromArgb("#2563EB");
            ViewTabButton.TextColor = Colors.White;
            EditTabButton.BackgroundColor = Color.FromArgb("#E9EDF4");
            EditTabButton.TextColor = Color.FromArgb("#1A1A1A");
        }
        else
        {
            EditTabButton.BackgroundColor = Color.FromArgb("#2563EB");
            EditTabButton.TextColor = Colors.White;
            ViewTabButton.BackgroundColor = Color.FromArgb("#E9EDF4");
            ViewTabButton.TextColor = Color.FromArgb("#1A1A1A");
        }
    }
}
