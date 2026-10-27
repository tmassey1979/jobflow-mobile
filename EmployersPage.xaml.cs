using System.Windows.Input;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class EmployersPage : ContentPage
{
    private readonly SupabaseDataService _supabase = new();
    private bool _isRefreshing;

    public EmployersPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadEmployersAsync());
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadEmployersAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadEmployersAsync();

    private async Task LoadEmployersAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            var employers = await _supabase.GetEmployersAsync();
            EmployersCollection.ItemsSource = employers;
            var blocked = employers.Count(e => e.DoNotPursue);
            StatusLabel.Text = $"{employers.Count} employers · {blocked} blocked";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Could not load employers.";
            await DisplayAlertAsync("Employer directory unavailable", ex.Message, "OK");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async void OnBlockActionClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: EmployerRecord employer }) return;

        if (employer.DoNotPursue)
        {
            var confirm = await DisplayAlertAsync(
                "Unblock employer?",
                $"Future discovery may include {employer.Name} again. Existing jobs are not deleted.",
                "Unblock",
                "Keep blocked");
            if (!confirm) return;
            employer.DoNotPursue = false;
            employer.BlockReason = null;
            employer.PursuitStatus = "undecided";
        }
        else
        {
            var reason = await DisplayPromptAsync(
                "Block employer",
                $"Why should JobFlow skip {employer.Name} in future discovery runs?",
                "Block",
                "Cancel",
                "Enter a reason (required)",
                maxLength: 500);
            if (string.IsNullOrWhiteSpace(reason)) return;
            employer.DoNotPursue = true;
            employer.BlockReason = reason.Trim();
            employer.PursuitStatus = "do_not_pursue";
        }

        await SaveAndRefreshAsync(employer);
    }

    private async void OnEditNotesClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: EmployerRecord employer }) return;
        var notes = await DisplayPromptAsync(
            "Employer notes",
            $"Private decision notes for {employer.Name}",
            "Save",
            "Cancel",
            "Add notes about this employer",
            maxLength: 2000,
            initialValue: employer.Notes ?? "");
        if (notes is null) return;
        employer.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await SaveAndRefreshAsync(employer);
    }

    private async Task SaveAndRefreshAsync(EmployerRecord employer)
    {
        try
        {
            await _supabase.SaveEmployerAsync(employer);
            await LoadEmployersAsync();
            StatusLabel.Text = employer.DoNotPursue
                ? $"{employer.Name} blocked for future discovery."
                : $"{employer.Name} saved.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't save employer", ex.Message, "OK");
        }
    }
}
