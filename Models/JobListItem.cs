namespace JobFlow.Mobile.Models;

/// <summary>
/// UI wrapper around a feed job so the Jobs list can show tracked state without mutating source data.
/// </summary>
public sealed class JobListItem
{
    public required JobFeedItem Job { get; init; }

    public bool IsTracked { get; init; }

    public string TrackedLabel => IsTracked ? "Tracked" : string.Empty;

    public string Title => Job.Title;
    public string Company => Job.Company;
    public string SalaryLabel => Job.SalaryLabel;
    public string? RemoteScope => Job.RemoteScope;
    public string? ApplicationUrl => Job.ApplicationUrl;
    public string JobId => Job.JobId;
}
