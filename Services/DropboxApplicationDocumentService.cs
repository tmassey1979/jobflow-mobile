using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Maui.Authentication;
using UglyToad.PdfPig;

namespace JobFlow.Mobile.Services;

/// <summary>
/// User-authorized Dropbox access for application documents. Tokens are stored in Android
/// SecureStorage; only the public Dropbox app key is kept in Preferences.
/// </summary>
public sealed class DropboxApplicationDocumentService
{
    private const string RedirectUri = "com.tmassey1979.jobflowmobile://oauth2redirect";
    private const string AppKeyPreference = "jobflow.dropbox.app_key";
    private const string AccessTokenKey = "jobflow.dropbox.access_token";
    private const string RefreshTokenKey = "jobflow.dropbox.refresh_token";
    private const string ExpiresAtKey = "jobflow.dropbox.expires_at";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public bool HasAppKey => !string.IsNullOrWhiteSpace(Preferences.Default.Get(AppKeyPreference, string.Empty));

    public async Task<bool> IsConnectedAsync()
    {
        try { return !string.IsNullOrWhiteSpace(await GetAccessTokenAsync()); }
        catch { return false; }
    }

    public async Task ConnectAsync(string appKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
            throw new ArgumentException("A Dropbox app key is required.", nameof(appKey));

        Preferences.Default.Set(AppKeyPreference, appKey.Trim());
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(24));
        var query = new Dictionary<string, string>
        {
            ["client_id"] = appKey.Trim(),
            ["response_type"] = "code",
            ["token_access_type"] = "offline",
            ["scope"] = "files.content.read",
            ["redirect_uri"] = RedirectUri,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        var authUrl = "https://www.dropbox.com/oauth2/authorize?" +
            string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        var result = await WebAuthenticator.Default.AuthenticateAsync(
            new Uri(authUrl), new Uri(RedirectUri));
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Properties.TryGetValue("state", out var returnedState)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(returnedState)))
            throw new InvalidOperationException("Dropbox sign-in could not be verified. Please try connecting again.");
        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Dropbox did not return an authorization code.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["client_id"] = appKey.Trim(),
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier
        });
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dropbox authorization failed ({(int)response.StatusCode}): {SafeError(body)}");
        await SaveTokensAsync(body);
    }

    public async Task<ExportedDocument> GetApplicationDocumentAsync(
        string dropboxPath, string suggestedName, CancellationToken cancellationToken = default)
    {
        if (!dropboxPath.StartsWith("/JobFlow/Applications/", StringComparison.OrdinalIgnoreCase)
            || dropboxPath.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("The application document path is outside /JobFlow/Applications.");

        var token = await GetAccessTokenAsync()
            ?? throw new InvalidOperationException("Connect Dropbox before loading application documents.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            dropboxPath.EndsWith(".paper", StringComparison.OrdinalIgnoreCase)
                ? "https://content.dropboxapi.com/2/files/export"
                : "https://content.dropboxapi.com/2/files/download");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Dropbox-API-Arg",
            JsonSerializer.Serialize(new { path = dropboxPath }));
        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Dropbox authorization expired. Tap Connect Dropbox and sign in again.");
            throw new InvalidOperationException($"Dropbox could not export '{suggestedName}' ({(int)response.StatusCode}): {SafeError(error)}");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0) throw new InvalidOperationException($"Dropbox returned an empty file for {suggestedName}.");

        var exportedName = suggestedName;
        if (response.Headers.TryGetValues("Dropbox-API-Result", out var values))
        {
            try
            {
                using var result = JsonDocument.Parse(values.First());
                if (result.RootElement.TryGetProperty("export_metadata", out var export)
                    && export.TryGetProperty("name", out var name))
                    exportedName = name.GetString() ?? suggestedName;
                else if (result.RootElement.TryGetProperty("name", out name))
                    exportedName = name.GetString() ?? suggestedName;
            }
            catch (JsonException) { }
        }
        if (Path.GetExtension(exportedName).Length == 0)
            exportedName = Path.ChangeExtension(exportedName, ".pdf");

        var safeFileName = Path.GetFileName(exportedName);
        var localPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}_{safeFileName}");
        await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);
        return new ExportedDocument(localPath, safeFileName, ExtractText(localPath));
    }

    private static string ExtractText(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".pdf")
        {
            using var pdf = PdfDocument.Open(path);
            return string.Join(Environment.NewLine + Environment.NewLine,
                pdf.GetPages().Select(page => page.Text)).Trim();
        }
        if (extension == ".docx")
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidOperationException("The exported Word document has no document body.");
            using var stream = entry.Open();
            var xml = XDocument.Load(stream);
            return string.Join(Environment.NewLine,
                xml.Descendants().Where(element => element.Name.LocalName == "p")
                    .Select(paragraph => string.Concat(paragraph.Descendants()
                        .Where(element => element.Name.LocalName == "t")
                        .Select(element => element.Value))))
                .Trim();
        }
        if (extension is ".md" or ".txt")
            return File.ReadAllText(path).Trim();
        throw new InvalidOperationException($"Dropbox exported an unsupported document format ({extension}). Expected PDF, DOCX, Markdown, or text.");
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        var expiryText = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
        if (!string.IsNullOrWhiteSpace(accessToken)
            && long.TryParse(expiryText, out var expiresAt)
            && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expiresAt - 60)
            return accessToken;
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var appKey = Preferences.Default.Get(AppKeyPreference, string.Empty);
        if (string.IsNullOrWhiteSpace(appKey)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = appKey
        });
        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return null;
        await SaveTokensAsync(body, preserveRefreshToken: true);
        return await SecureStorage.Default.GetAsync(AccessTokenKey);
    }

    private static async Task SaveTokensAsync(string body, bool preserveRefreshToken = false)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var access = root.GetProperty("access_token").GetString();
        if (string.IsNullOrWhiteSpace(access)) throw new InvalidOperationException("Dropbox did not return an access token.");
        await SecureStorage.Default.SetAsync(AccessTokenKey, access);
        if (root.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrWhiteSpace(refresh.GetString()))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refresh.GetString()!);
        else if (!preserveRefreshToken)
            throw new InvalidOperationException("Dropbox did not issue an offline refresh token. Reconnect and approve offline access.");
        var expiresIn = root.TryGetProperty("expires_in", out var expires) ? expires.GetInt64() : 14400;
        await SecureStorage.Default.SetAsync(ExpiresAtKey,
            (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn).ToString());
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string SafeError(string body) =>
        body.Length > 500 ? body[..500] : body;

    public sealed record ExportedDocument(string LocalPath, string FileName, string Text);
}
