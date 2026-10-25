namespace JobFlow.Mobile.Models;

public enum IntegrationStatus
{
    NotConfigured,
    Disconnected,
    Connecting,
    Connected,
    NeedsReauthentication,
    Error
}

/// <summary>
/// Non-secret provider status for the Integrations screen. Credentials never belong in this model.
/// </summary>
public sealed class IntegrationConnection
{
    public IntegrationConnection(string providerId, string displayName, IntegrationStatus initialStatus = IntegrationStatus.Disconnected)
    {
        if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("Provider ID is required.", nameof(providerId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Provider display name is required.", nameof(displayName));
        ProviderId = providerId;
        DisplayName = displayName;
        Status = initialStatus;
    }

    public string ProviderId { get; }
    public string DisplayName { get; }
    public IntegrationStatus Status { get; private set; }
    public IReadOnlyList<string> GrantedPermissions { get; private set; } = Array.Empty<string>();
    public DateTimeOffset? LastSuccessfulSync { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool CanDisconnect => Status is IntegrationStatus.Connected or IntegrationStatus.NeedsReauthentication or IntegrationStatus.Error;

    public void BeginConnect()
    {
        if (Status == IntegrationStatus.NotConfigured)
            throw new InvalidOperationException($"{DisplayName} is not configured.");
        Status = IntegrationStatus.Connecting;
        ErrorMessage = null;
    }

    public void MarkConnected(IEnumerable<string> grantedPermissions, DateTimeOffset? lastSuccessfulSync = null)
    {
        ArgumentNullException.ThrowIfNull(grantedPermissions);
        if (Status == IntegrationStatus.NotConfigured)
            throw new InvalidOperationException($"{DisplayName} is not configured.");
        GrantedPermissions = grantedPermissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LastSuccessfulSync = lastSuccessfulSync;
        ErrorMessage = null;
        Status = IntegrationStatus.Connected;
    }

    public void MarkSyncSucceeded(DateTimeOffset completedAt)
    {
        if (Status != IntegrationStatus.Connected)
            throw new InvalidOperationException($"{DisplayName} is not connected.");
        LastSuccessfulSync = completedAt;
        ErrorMessage = null;
    }

    public void MarkNeedsReauthentication(string? message = null)
    {
        if (Status == IntegrationStatus.NotConfigured)
            throw new InvalidOperationException($"{DisplayName} is not configured.");
        Status = IntegrationStatus.NeedsReauthentication;
        ErrorMessage = SanitizeMessage(message);
    }

    public void MarkFailed(string message)
    {
        if (Status == IntegrationStatus.NotConfigured)
            throw new InvalidOperationException($"{DisplayName} is not configured.");
        Status = IntegrationStatus.Error;
        ErrorMessage = SanitizeMessage(message) ?? "Connection failed. Try again.";
    }

    public void Disconnect()
    {
        if (Status == IntegrationStatus.NotConfigured)
            throw new InvalidOperationException($"{DisplayName} is not configured.");
        Status = IntegrationStatus.Disconnected;
        GrantedPermissions = Array.Empty<string>();
        LastSuccessfulSync = null;
        ErrorMessage = null;
    }

    private static string? SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        return message.Trim().Replace("\r", " ").Replace("\n", " ")[..Math.Min(message.Trim().Length, 240)];
    }
}
