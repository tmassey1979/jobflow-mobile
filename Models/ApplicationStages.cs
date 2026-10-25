namespace JobFlow.Mobile.Models;

/// <summary>
/// Defines the local application pipeline and its explicit manual progression.
/// No stage triggers an application submission or external communication.
/// </summary>
public static class ApplicationStages
{
    public const string Interested = "Interested";
    public const string Preparing = "Preparing";
    public const string ReadyToApply = "Ready to apply";
    public const string Applied = "Applied";
    public const string Interviewing = "Interviewing";
    public const string Offer = "Offer";
    public const string Rejected = "Closed";

    public static IReadOnlyList<string> All { get; } =
    [
        Interested, Preparing, ReadyToApply, Applied, Interviewing, Offer, Rejected
    ];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status)
        && All.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static string Next(string? current)
    {
        var index = All.ToList().FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
        return All[(index + 1) % All.Count];
    }
}
