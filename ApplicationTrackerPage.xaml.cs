using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class ApplicationTrackerPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private IReadOnlyList<ApplicationRecord> _allRecords = Array.Empty<ApplicationRecord>();

    public ApplicationTrackerPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _allRecords = await _tracker.GetAllAsync();
            ApplySearch(SearchEntry.Text);
        }
        catch
        {
            await DisplayAlertAsync("Tracker unavailable", "JobFlow could not read the local application tracker.", "OK");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplySearch(e.NewTextValue);

    private void ApplySearch(string? query)
    {
        var normalized = query?.Trim();
        var filtered = string.IsNullOrEmpty(normalized)
            ? _allRecords
            : _allRecords.Where(record =>
                    record.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || record.Company.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || (record.Notes?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false)
                    || record.Status.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

        ApplicationsCollection.ItemsSource = filtered;
        var applied = _allRecords.Count(record => record.Status == ApplicationStages.Applied);
        SummaryLabel.Text = string.IsNullOrEmpty(normalized)
            ? $"{_allRecords.Count} tracked · {applied} marked applied · saved locally"
            : $"{filtered.Count} of {_allRecords.Count} tracked roles match “{normalized}”";

        var noRecords = _allRecords.Count == 0;
        EmptyTitleLabel.Text = noRecords ? "Your tracker starts here" : "No matching roles";
        EmptyDescriptionLabel.Text = noRecords
            ? "Use Track application on any job card to add it to your pipeline."
            : "Try another title, company, stage, or note.";
    }

    private async void OnChooseStageClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        var current = _allRecords.FirstOrDefault(record => record.JobId == jobId);
        if (current is null) return;

        var selected = await DisplayActionSheetAsync(
            "Choose application stage",
            "Cancel",
            null,
            ApplicationStages.All.ToArray());
        if (string.IsNullOrWhiteSpace(selected)
            || string.Equals(selected, "Cancel", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _tracker.SetStatusAsync(jobId, selected);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't update stage", "The selected stage could not be saved locally. Please try again.", "OK");
        }
    }

    private async void OnEditNotesClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        var current = _allRecords.FirstOrDefault(record => record.JobId == jobId);
        if (current is null) return;

        var updated = await DisplayPromptAsync(
            "Application notes",
            "Notes are stored only on this device.",
            "Save",
            "Cancel",
            "Add a reminder, follow-up date, or interview detail",
            maxLength: 2000,
            initialValue: current.Notes ?? string.Empty);
        if (updated is null) return;

        try
        {
            await _tracker.UpdateNotesAsync(jobId, updated);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't save notes", "Your notes could not be saved locally. Please try again.", "OK");
        }
    }

    private async void OnRemoveClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        var confirm = await DisplayAlertAsync(
            "Remove from tracker?",
            "This removes only the local tracking record. It does not delete the job feed entry or contact the employer.",
            "Remove",
            "Cancel");
        if (!confirm) return;

        try
        {
            await _tracker.RemoveAsync(jobId);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't remove record", "The local application record could not be removed. Please try again.", "OK");
        }
    }

    private async void OnOpenApplicationClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url }
            && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps)
        {
            await Launcher.Default.OpenAsync(parsed);
        }
        else
        {
            await DisplayAlertAsync("Application link unavailable", "This tracked job does not have a valid HTTPS application URL.", "OK");
        }
    }
}
