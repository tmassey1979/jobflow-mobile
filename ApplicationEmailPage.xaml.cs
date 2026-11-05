using Microsoft.Maui.ApplicationModel.Communication;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class ApplicationEmailPage : ContentPage
{
    private readonly JobFeedItem _job;
    private readonly RecruiterContactDiscoveryService _contacts = new();
    private readonly DropboxApplicationDocumentService _dropbox = new();
    private string? _resumeFilePath;
    private string? _resumeFileName;
    private RecruiterContactRecord? _selectedContact;
    private bool _documentsLoading;

    public ApplicationEmailPage(JobFeedItem job)
    {
        InitializeComponent();
        _job = job ?? throw new ArgumentNullException(nameof(job));
        DraftButton.IsEnabled = false;
        RoleLabel.Text = $"{job.Title} · {job.Company}";
        SubjectEntry.Text = $"Application: {job.Title}";
        ContactStatusLabel.Text = "No recruiter/HR email confirmed yet. Search the job's public source pages.";
        ResumeStatusLabel.Text = "Connect Dropbox to load the matching resume and cover letter.";
        LoadContactsAsync();
        LoadDocumentsIfConnectedAsync();
    }

    private async void LoadDocumentsIfConnectedAsync()
    {
        if (await _dropbox.IsConnectedAsync())
            await LoadDocumentsAsync();
    }

    private async void OnConnectDropboxClicked(object sender, EventArgs e)
    {
        ConnectDropboxButton.IsEnabled = false;
        try
        {
            var appKey = Preferences.Default.Get("jobflow.dropbox.app_key", string.Empty);
            if (string.IsNullOrWhiteSpace(appKey))
            {
                appKey = await DisplayPromptAsync("Dropbox app key",
                    "Enter the App key from your Dropbox developer app. JobFlow uses read-only file access.",
                    "Continue", "Cancel", keyboard: Keyboard.Text);
                if (string.IsNullOrWhiteSpace(appKey)) return;
            }

            ResumeStatusLabel.Text = "Opening Dropbox sign-in…";
            await _dropbox.ConnectAsync(appKey);
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            ResumeStatusLabel.Text = $"Dropbox connection failed: {ex.Message}";
            DraftButton.IsEnabled = false;
        }
        finally
        {
            ConnectDropboxButton.IsEnabled = true;
        }
    }

    private async Task LoadDocumentsAsync()
    {
        if (_documentsLoading) return;
        _documentsLoading = true;
        DraftButton.IsEnabled = false;
        try
        {
            ResumeStatusLabel.Text = "Downloading the job-specific documents from Dropbox…";
            var resumePath = ResolveDropboxPath(_job.ResumeDropboxFilename, _job.JobId, "Resume");
            var coverPath = ResolveDropboxPath(_job.CoverLetterDropboxFilename, _job.JobId, "Cover Letter");

            var resume = await _dropbox.GetApplicationDocumentAsync(resumePath,
                $"{_job.JobId} - Resume.pdf");
            if (string.IsNullOrWhiteSpace(resume.Text))
                throw new InvalidOperationException("The matching Dropbox resume exported successfully, but its text could not be verified. Email draft remains disabled.");

            var cover = await _dropbox.GetApplicationDocumentAsync(coverPath,
                $"{_job.JobId} - Cover Letter.pdf");
            if (string.IsNullOrWhiteSpace(cover.Text))
                throw new InvalidOperationException("The matching Dropbox cover letter is empty or could not be read. Email draft remains disabled.");

            _resumeFilePath = resume.LocalPath;
            _resumeFileName = resume.FileName;
            CoverLetterEditor.Text = cover.Text;
            ResumeStatusLabel.Text = $"Dropbox documents ready. Resume attachment: {resume.FileName}";
            DraftButton.Text = $"Review email with {Path.GetExtension(resume.FileName).TrimStart('.').ToUpperInvariant()} resume attached";
            DraftButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _resumeFilePath = null;
            _resumeFileName = null;
            CoverLetterEditor.Text = string.Empty;
            ResumeStatusLabel.Text = $"Could not load the matching Dropbox application kit: {ex.Message}";
            DraftButton.IsEnabled = false;
        }
        finally
        {
            _documentsLoading = false;
        }
    }

    private static string ResolveDropboxPath(string? configuredName, string jobId, string documentType)
    {
        var name = string.IsNullOrWhiteSpace(configuredName)
            ? $"{jobId} - {documentType}.paper"
            : configuredName.Trim();
        if (name.StartsWith("/JobFlow/Applications/", StringComparison.OrdinalIgnoreCase))
            return name;
        if (name.Contains('/') || name.Contains('\\') || name.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("The Dropbox document filename is not a safe file name.");
        return $"/JobFlow/Applications/{name}";
    }

    private async void LoadContactsAsync()
    {
        try
        {
            var found = await _contacts.GetForJobAsync(_job.JobId);
            ShowContacts(found, chooseContact: false);
        }
        catch
        {
            ContactStatusLabel.Text = "Saved contact records could not be loaded.";
        }
    }

    private async void OnFindContactClicked(object sender, EventArgs e)
    {
        FindContactButton.IsEnabled = false;
        ContactStatusLabel.Text = "Checking public job-posting and application pages for published email addresses…";
        try
        {
            var found = await _contacts.DiscoverAsync(_job);
            ShowContacts(found, chooseContact: true);
        }
        catch (Exception ex)
        {
            ContactStatusLabel.Text = $"Contact search failed: {ex.Message}";
        }
        finally
        {
            FindContactButton.IsEnabled = true;
        }
    }

    private void ShowContacts(IReadOnlyList<RecruiterContactRecord> contacts, bool chooseContact)
    {
        if (contacts.Count == 0)
        {
            ContactStatusLabel.Text = "No public email address found. JobFlow will not guess an address. You can use the employer's application link instead.";
            SourceLabel.Text = "Source: none";
            _selectedContact = null;
            return;
        }

        var options = contacts.Select(contact => $"{contact.Email} — {contact.VerificationStatus}").ToArray();
        if (!chooseContact)
        {
            ContactStatusLabel.Text = $"{contacts.Count} public recruiting-related contact(s) already saved. Tap Find to select one.";
            return;
        }
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var selected = await DisplayActionSheetAsync("Public contacts found", "Cancel", null, options);
            var index = Array.IndexOf(options, selected);
            if (index < 0) return;
            _selectedContact = contacts[index];
            RecipientEntry.Text = _selectedContact.Email;
            SourceLabel.Text = $"Source: {_selectedContact.SourceUrl}\nChecked: {_selectedContact.VerifiedAtUtc.ToLocalTime():g}";
            ContactStatusLabel.Text = $"{contacts.Count} published email address(es) found. Confirm the recipient is appropriate before opening the draft.";
        });
    }

    private async void OnDraftClicked(object sender, EventArgs e)
    {
        var recipient = RecipientEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(recipient) || !System.Net.Mail.MailAddress.TryCreate(recipient, out var address)
            || !string.Equals(address.Address, recipient, StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Recipient required", "Enter a valid email address from a public source before continuing.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(_resumeFilePath) || !File.Exists(_resumeFilePath)
            || string.IsNullOrWhiteSpace(CoverLetterEditor.Text))
        {
            await DisplayAlertAsync("Application kit incomplete", "The matching Dropbox resume file and cover letter must both be loaded before preparing an email.", "OK");
            return;
        }

        var approved = await DisplayAlertAsync("Review application email",
            $"Open your email app with this recipient?\n\nTo: {recipient}\nJob: {_job.Title} at {_job.Company}\nAttachment: {_resumeFileName}\n\nNothing will be sent by JobFlow.",
            "Open draft", "Cancel");
        if (!approved) return;

        try
        {
            var message = new EmailMessage
            {
                Subject = string.IsNullOrWhiteSpace(SubjectEntry.Text) ? $"Application: {_job.Title}" : SubjectEntry.Text.Trim(),
                Body = CoverLetterEditor.Text.Trim(),
                To = { recipient },
                Attachments = { new EmailAttachment(_resumeFilePath) }
            };
            await Email.Default.ComposeAsync(message);
            ResumeStatusLabel.Text = "Email draft opened with the actual Dropbox-exported resume file attached. JobFlow has not sent it.";
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlertAsync("Email unavailable", "No email composer is available on this device.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open email draft", ex.Message, "OK");
        }
    }
}
