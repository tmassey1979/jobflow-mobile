namespace JobFlow.Mobile.Services;

/// <summary>
/// Device-local GitHub session: OAuth access token, selected data repository, and public OAuth client id.
/// No secrets other than the access token are stored; the client id is not confidential.
/// </summary>
public static class GitHubAccountStore
{
    public const string TokenKey = "jobflow.github.access.token";
    public const string OwnerKey = "jobflow.github.repo.owner";
    public const string RepoKey = "jobflow.github.repo.name";
    public const string ClientIdKey = "jobflow.github.oauth.client_id";
    public const string DefaultOwner = "tmassey1979";
    public const string DefaultRepo = "Jobflow-data";

    /// <summary>
    /// Public OAuth App client ID (not a secret). Used when the user has not overridden it on device.
    /// </summary>
    public const string DefaultClientId = "Ov23liEzPPsOeMKTOQHn";

    // Legacy key from the temporary PAT paste flow — migrated on read, then removed.
    private const string LegacyTokenKey = "jobflow.github.readonly.token";

    public static async Task<string?> GetAccessTokenAsync()
    {
        // Do not hide SecureStorage failures as a normal signed-out state. The UI needs
        // to distinguish a missing token from storage that cannot be read.
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

        // Verify persistence immediately so a successful OAuth screen cannot be mistaken
        // for a saved session when Android SecureStorage did not retain the value.
        var saved = await SecureStorage.Default.GetAsync(TokenKey);
        if (!string.Equals(saved, normalized, StringComparison.Ordinal))
            throw new InvalidOperationException("Android Secure Storage did not retain the GitHub session. Please restart the app and try signing in again.");
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
        try { SecureStorage.Default.Remove(LegacyTokenKey); } catch { }
        try { SecureStorage.Default.Remove(OwnerKey); } catch { }
        try { SecureStorage.Default.Remove(RepoKey); } catch { }
        await Task.CompletedTask;
    }

    private static async Task<string?> TryGetAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch { return null; }
    }
}
