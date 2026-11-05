using System.Text;
using Microsoft.Maui.ApplicationModel.Communication;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class ApplicationEmailPage : ContentPage
{
    private readonly JobFeedItem _job;
    private readonly SupabaseDataService _supabase = new();
    private readonly RecruiterContactDiscoveryService _contacts = new();
    private string? _resumeText;
    private RecruiterContactRecord? _selectedContact;

    public ApplicationEmailPage(JobFeedItem job)
    {
        InitializeComponent();
        _job = job ?? throw new ArgumentNullException(nameof(job));
        RoleLabel.Text = $"{job.Title} · {job.Company}";
        SubjectEntry.Text = $"Application: {job.Title}";
        ContactStatusLabel.Text = "No recruiter/HR email confirmed yet. Search the job's public source pages.";
        ResumeStatusLabel.Text = "Loading your job-specific resume and cover letter…";
        LoadDocumentsAsync();
        LoadContactsAsync();
    }

    private async void LoadDocumentsAsync()
    {
        try
        {
            if (!await _supabase.IsSignedInAsync())
            {
                ResumeStatusLabel.Text = "Sign in to JobFlow Cloud to load the private job-specific application kit.";
                DraftButton.IsEnabled = false;
                return;
            }

            _resumeText = await _supabase.GetJobDocumentAsync(_job.JobId, "resume");
            var cover = await _supabase.GetJobDocumentAsync(_job.JobId, "cover");
            if (string.IsNullOrWhiteSpace(_resumeText))
            {
                ResumeStatusLabel.Text = "No resume content is available in the private JobFlow record. Email draft is disabled so the wrong attachment is never sent.";
                DraftButton.IsEnabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(cover))
            {
                ResumeStatusLabel.Text = "Job-specific resume loaded; no cover letter found. Add the cover letter text before opening the email draft.";
                DraftButton.IsEnabled = false;
                return;
            }

            CoverLetterEditor.Text = cover;
            ResumeStatusLabel.Text = "Job-specific resume loaded and ready to attach.";
            DraftButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ResumeStatusLabel.Text = $"Could not load the private application kit: {ex.Message}";
            DraftButton.IsEnabled = false;
        }
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
        if (string.IsNullOrWhiteSpace(_resumeText) || string.IsNullOrWhiteSpace(CoverLetterEditor.Text))
        {
            await DisplayAlertAsync("Application kit incomplete", "The correct job-specific resume and cover letter must both be loaded before preparing an email.", "OK");
            return;
        }

        var approved = await DisplayAlertAsync("Review application email",
            $"Open your email app with this recipient?\n\nTo: {recipient}\nJob: {_job.Title} at {_job.Company}\nAttachment: job-specific resume\n\nNothing will be sent by JobFlow.",
            "Open draft", "Cancel");
        if (!approved) return;

        try
        {
            var safeName = string.Concat(_job.Company.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            var filePath = Path.Combine(FileSystem.CacheDirectory, $"JobFlow_{safeName}_{DateTime.UtcNow:yyyyMMddHHmmss}_Resume.txt");
            await File.WriteAllTextAsync(filePath, _resumeText, new UTF8Encoding(false));
            var message = new EmailMessage
            {
                Subject = string.IsNullOrWhiteSpace(SubjectEntry.Text) ? $"Application: {_job.Title}" : SubjectEntry.Text.Trim(),
                Body = CoverLetterEditor.Text.Trim(),
                To = { recipient },
                Attachments = { new EmailAttachment(filePath) }
            };
            await Email.Default.ComposeAsync(message);
            ResumeStatusLabel.Text = "Email draft opened. JobFlow has not sent it; finish review and send from your email app.";
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
