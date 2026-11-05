using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// A publicly published professional contact discovered for one job.
/// The source URL and verification timestamp are retained; contacts are device-local.
/// </summary>
[Table("recruiter_contacts")]
public sealed class RecruiterContactRecord
{
    [PrimaryKey]
    public string Id { get; set; } = "";

    [Indexed]
    public string JobId { get; set; } = "";

    public string Email { get; set; } = "";

    public string? ContactName { get; set; }

    public string? ProfessionalRole { get; set; }

    public string Company { get; set; } = "";

    public string SourceUrl { get; set; } = "";

    public DateTime VerifiedAtUtc { get; set; } = DateTime.UtcNow;

    public string VerificationStatus { get; set; } = "Published on source page";
}
