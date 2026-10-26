namespace JobFlow.Mobile.Services;

/// <summary>
/// Device-local GitHub session: OAuth access token, selected data repository, and public OAuth client id.
/// No secrets other than the access token are stored; the client id is not confidential.
/// </summary>
public static class GitHubAccountStore
{
    public const string TokenKey = "jobflow.github.access.token";
    public const string LoginKey = "jobflow.github.user.login";
    public const string OwnerKey = "jobflow.github.repo.owner";
    public const string RepoKey = "jobflow.github.repo.name";
    public const string ClientIdKey = "jobflow.github.oauth.client_id";
    public const string DefaultOwner = "tmassey1979";
    public const string DefaultRepo = "Jobflow-data";

    /// <summary>
    /// Public OAuth App client ID (not a secret). Used when the user has not overridden it on device.
    /// </summary>
    public const string DefaultClientId = "Ov23liEzPPsOeMKTOQHn";

    private const string LegacyTokenKey = "jobflow.github.readonly.token";
    private const string PendingDeviceCodeKey = "jobflow.github.pending.device_code";
    private const string PendingUserCodeKey = "jobflow.github.pending.user_code";
    private const string PendingVerificationUriKey = "jobflow.github.pending.verification_uri";
    private const string PendingIntervalKey = "jobflow.github.pending.interval";
    private const string PendingExpiresAtKey = "jobflow.github.pending.expires_at";

    public static async Task<string?> GetAccessTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        var legacy = await SecureStorage.Default.GetAsync(LegacyTokenKey);
        if (string.IsNullOrWhiteSpace(legacy))
            return null;

        await SetAccessTokenAsync(legacy);
        try { SecureStorage.Default.Remove(LegacyTokenKey); } catch { /* best effort */ }
        return legacy;
    }

    public static async Task SetAccessTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("GitHub returned an empty access token.", nameof(token));

        var normalized = token.Trim();
        await SecureStorage.Default.SetAsync(TokenKey, normalized);

        var saved = await SecureStorage.Default.GetAsync(TokenKey);
        if (!string.Equals(saved, normalized, StringComparison.Ordinal))
            throw new InvalidOperationException("Android Secure Storage did not retain the GitHub session. Please restart the app and try signing in again.");
    }

    public static async Task SetLoginAsync(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
            return;
        await SecureStorage.Default.SetAsync(LoginKey, login.Trim());
    }

    public static async Task<string?> GetLoginAsync()
    {
        try { return await SecureStorage.Default.GetAsync(LoginKey); }
        catch { return null; }
    }

    public sealed record PendingDeviceLogin(string DeviceCode, string UserCode, string VerificationUri, int IntervalSeconds, DateTimeOffset ExpiresAtUtc);

    public static async Task SavePendingDeviceLoginAsync(PendingDeviceLogin pending)
    {
        await SecureStorage.Default.SetAsync(PendingDeviceCodeKey, pending.DeviceCode);
        await SecureStorage.Default.SetAsync(PendingUserCodeKey, pending.UserCode);
        await SecureStorage.Default.SetAsync(PendingVerificationUriKey, pending.VerificationUri);
        await SecureStorage.Default.SetAsync(PendingIntervalKey, pending.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await SecureStorage.Default.SetAsync(PendingExpiresAtKey, pending.ExpiresAtUtc.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static async Task<PendingDeviceLogin?> GetPendingDeviceLoginAsync()
    {
        var code = await SecureStorage.Default.GetAsync(PendingDeviceCodeKey);
        if (string.IsNullOrWhiteSpace(code)) return null;
        var userCode = await SecureStorage.Default.GetAsync(PendingUserCodeKey) ?? "";
        var uri = await SecureStorage.Default.GetAsync(PendingVerificationUriKey) ?? "https://github.com/login/device";
        var intervalText = await SecureStorage.Default.GetAsync(PendingIntervalKey);
        var expiresText = await SecureStorage.Default.GetAsync(PendingExpiresAtKey);
        if (!int.TryParse(intervalText, out var interval)) interval = 5;
        if (!long.TryParse(expiresText, out var expiresUnix)) return null;
        return new PendingDeviceLogin(code, userCode, uri, interval, DateTimeOffset.FromUnixTimeSeconds(expiresUnix));
    }

    public static void ClearPendingDeviceLogin()
    {
        try { SecureStorage.Default.Remove(PendingDeviceCodeKey); } catch { }
        try { SecureStorage.Default.Remove(PendingUserCodeKey); } catch { }
        try { SecureStorage.Default.Remove(PendingVerificationUriKey); } catch { }
        try { SecureStorage.Default.Remove(PendingIntervalKey); } catch { }
        try { SecureStorage.Default.Remove(PendingExpiresAtKey); } catch { }
    }

    public static async Task<(string Owner, string Name)> GetSelectedRepositoryAsync()
    {
        var owner = await TryGetAsync(OwnerKey);
        var name = await TryGetAsync(RepoKey);
        return (
            string.IsNullOrWhiteSpace(owner) ? DefaultOwner : owner.Trim(),
            string.IsNullOrWhiteSpace(name) ? DefaultRepo : name.Trim());
    }

    public static async Task SetSelectedRepositoryAsync(string owner, string name)
    {
        await SecureStorage.Default.SetAsync(OwnerKey, owner.Trim());
        await SecureStorage.Default.SetAsync(RepoKey, name.Trim());
    }

    public static async Task<string> GetClientIdAsync()
    {
        var stored = await TryGetAsync(ClientIdKey);
        return string.IsNullOrWhiteSpace(stored) ? DefaultClientId : stored.Trim();
    }

    public static Task SetClientIdAsync(string clientId) =>
        SecureStorage.Default.SetAsync(ClientIdKey, clientId.Trim());

    public static async Task ClearSessionAsync()
    {
        try { SecureStorage.Default.Remove(TokenKey); } catch { }
        try { SecureStorage.Default.Remove(LoginKey); } catch { }
        try { SecureStorage.Default.Remove(LegacyTokenKey); } catch { }
        try { SecureStorage.Default.Remove(OwnerKey); } catch { }
        try { SecureStorage.Default.Remove(RepoKey); } catch { }
        ClearPendingDeviceLogin();
        await Task.CompletedTask;
    }

    private static async Task<string?> TryGetAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch { return null; }
    }
}
