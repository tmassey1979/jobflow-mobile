using System.Globalization;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class ApplicationTrackerPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private IReadOnlyList<ApplicationRecord> _allRecords = Array.Empty<ApplicationRecord>();
    private FollowUpFilter _followUpFilter = FollowUpFilter.All;
    private string? _stageFilter;

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

    private void OnFilterAllClicked(object sender, EventArgs e)
    {
        _followUpFilter = FollowUpFilter.All;
        _stageFilter = null;
        UpdateFilterButtons();
        ApplySearch(SearchEntry.Text);
    }

    private void OnFilterOverdueClicked(object sender, EventArgs e)
    {
        _followUpFilter = FollowUpFilter.Overdue;
        _stageFilter = null;
        UpdateFilterButtons();
        ApplySearch(SearchEntry.Text);
    }

    private void OnFilterDueSoonClicked(object sender, EventArgs e)
    {
        _followUpFilter = FollowUpFilter.DueSoon;
        _stageFilter = null;
        UpdateFilterButtons();
        ApplySearch(SearchEntry.Text);
    }

    private async void OnFilterStageClicked(object sender, EventArgs e)
    {
        var options = ApplicationStages.All.ToList();
        options.Insert(0, "Any stage");
        var selected = await DisplayActionSheetAsync("Filter by stage", "Cancel", null, options.ToArray());
        if (string.IsNullOrWhiteSpace(selected)
            || string.Equals(selected, "Cancel", StringComparison.OrdinalIgnoreCase))
            return;

        _followUpFilter = FollowUpFilter.All;
        _stageFilter = string.Equals(selected, "Any stage", StringComparison.OrdinalIgnoreCase)
            ? null
            : selected;
        UpdateFilterButtons();
        ApplySearch(SearchEntry.Text);
    }

    private void UpdateFilterButtons()
    {
        StyleFilterButton(FilterAllButton, _followUpFilter == FollowUpFilter.All && _stageFilter is null);
        StyleFilterButton(FilterOverdueButton, _followUpFilter == FollowUpFilter.Overdue);
        StyleFilterButton(FilterDueSoonButton, _followUpFilter == FollowUpFilter.DueSoon);
        StyleFilterButton(FilterStageButton, _stageFilter is not null);
        FilterStageButton.Text = _stageFilter is null ? "Stage" : _stageFilter;
    }

    private static void StyleFilterButton(Button button, bool selected)
    {
        if (selected)
        {
            button.BackgroundColor = Color.FromArgb("#2563EB");
            button.TextColor = Colors.White;
        }
        else
        {
            button.BackgroundColor = Color.FromArgb("#E9EDF4");
            button.TextColor = Color.FromArgb("#1A1A1A");
        }
    }

    private void ApplySearch(string? query)
    {
        var normalized = query?.Trim();
        IEnumerable<ApplicationRecord> source = _allRecords;

        source = _followUpFilter switch
        {
            FollowUpFilter.Overdue => source.Where(r => r.IsFollowUpOverdue),
            FollowUpFilter.DueSoon => source.Where(r => r.IsFollowUpDueSoon),
            _ => source
        };

        if (!string.IsNullOrEmpty(_stageFilter))
        {
            source = source.Where(r =>
                string.Equals(r.Status, _stageFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(normalized))
        {
            source = source.Where(record =>
                record.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || record.Company.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || (record.Notes?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false)
                || record.Status.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source
            .OrderByDescending(r => r.IsFollowUpOverdue)
            .ThenByDescending(r => r.IsFollowUpDueSoon)
            .ThenBy(r => r.FollowUpAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(r => r.UpdatedAtUtc)
            .ToList();

        ApplicationsCollection.ItemsSource = filtered;
        var applied = _allRecords.Count(record => record.Status == ApplicationStages.Applied);
        var overdue = _allRecords.Count(r => r.IsFollowUpOverdue);
        var dueSoon = _allRecords.Count(r => r.IsFollowUpDueSoon);

        SummaryLabel.Text = string.IsNullOrEmpty(normalized)
            ? $"{_allRecords.Count} tracked · {overdue} overdue · {dueSoon} due soon · {applied} applied"
            : $"{filtered.Count} of {_allRecords.Count} match “{normalized}”";

        PipelineLabel.Text = BuildPipelineSummary(_allRecords);

        var noRecords = _allRecords.Count == 0;
        EmptyTitleLabel.Text = noRecords ? "Your tracker starts here" : "No matching roles";
        EmptyDescriptionLabel.Text = noRecords
            ? "Use Track application on any job card to add it to your pipeline."
            : _followUpFilter == FollowUpFilter.Overdue
                ? "No overdue follow-ups right now."
                : _followUpFilter == FollowUpFilter.DueSoon
                    ? "Nothing due in the next 3 days."
                    : _stageFilter is not null
                        ? $"No roles in stage {_stageFilter}."
                        : "Try another title, company, stage, or note.";
    }

    private static string BuildPipelineSummary(IReadOnlyList<ApplicationRecord> records)
    {
        if (records.Count == 0)
            return "Pipeline: empty";

        var parts = ApplicationStages.All
            .Select(stage =>
            {
                var count = records.Count(r => string.Equals(r.Status, stage, StringComparison.OrdinalIgnoreCase));
                return count > 0 ? $"{stage} {count}" : null;
            })
            .Where(part => part is not null);

        return "Pipeline: " + string.Join(" · ", parts);
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

    private async void OnFollowUpClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        var current = _allRecords.FirstOrDefault(record => record.JobId == jobId);
        if (current is null) return;

        var action = await DisplayActionSheetAsync(
            "Follow-up date",
            "Cancel",
            null,
            "Set date",
            "Clear date");
        if (string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(action))
            return;

        DateTime? dueAtUtc = null;
        if (string.Equals(action, "Set date", StringComparison.Ordinal))
        {
            var currentDate = current.FollowUpAtUtc?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var entered = await DisplayPromptAsync(
                "Set follow-up date",
                "Enter a date as YYYY-MM-DD. Saved on this device only; does not create a system notification yet.",
                "Save",
                "Cancel",
                "YYYY-MM-DD",
                maxLength: 10,
                initialValue: currentDate ?? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            if (entered is null) return;
            if (!DateTime.TryParseExact(
                    entered.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                await DisplayAlertAsync("Date not recognized", "Please enter a valid date in YYYY-MM-DD format.", "OK");
                return;
            }

            dueAtUtc = DateTime.SpecifyKind(parsedDate.Date.AddHours(17), DateTimeKind.Local).ToUniversalTime();
        }
        else if (!string.Equals(action, "Clear date", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await _tracker.SetFollowUpAsync(jobId, dueAtUtc);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't save follow-up", "The follow-up date could not be saved locally. Please try again.", "OK");
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

    private enum FollowUpFilter
    {
        All,
        Overdue,
        DueSoon
    }
}
