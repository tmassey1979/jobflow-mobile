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

    // Legacy key from the temporary PAT paste flow — migrated on read, then removed.
    private const string LegacyTokenKey = "jobflow.github.readonly.token";

    public static async Task<string?> GetAccessTokenAsync()
    {
        var token = await TryGetAsync(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
            return token;

        var legacy = await TryGetAsync(LegacyTokenKey);
        if (string.IsNullOrWhiteSpace(legacy))
            return null;

        await SetAccessTokenAsync(legacy);
        try { SecureStorage.Default.Remove(LegacyTokenKey); } catch { /* best effort */ }
        return legacy;
    }

    public static Task SetAccessTokenAsync(string token) =>
        SecureStorage.Default.SetAsync((TokenKey, token.Trim());

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

    public static Task<string?> GetClientIdAsync() => TryGetAsync(ClientIdKey);

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
