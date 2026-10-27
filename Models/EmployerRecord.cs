using System.Text.Json.Serialization;

namespace JobFlow.Mobile.Models;

/// <summary>
/// Shared employer profile and household pursuit decision. A block is intentionally employer-wide.
/// </summary>
public sealed class EmployerRecord
{
    [JsonPropertyName("normalized_name")]
    public string NormalizedName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("pursuit_status")]
    public string PursuitStatus { get; set; } = "undecided";

    [JsonPropertyName("do_not_pursue")]
    public bool DoNotPursue { get; set; }

    [JsonPropertyName("block_reason")]
    public string? BlockReason { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonIgnore]
    public string DecisionLabel => DoNotPursue ? "Blocked from future discovery" : $"Decision: {PursuitStatus.Replace('_', ' ')}";

    [JsonIgnore]
    public string BlockActionLabel => DoNotPursue ? "Unblock" : "Block employer";
}
