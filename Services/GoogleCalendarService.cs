using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Read-only Google Calendar access via OAuth 2.0 + PKCE.
/// Never requests calendar write scopes. Never creates or edits events.
/// Requires a Google Cloud OAuth client ID (see docs/INTEGRATIONS-SETUP.md).
/// </summary>
public sealed class GoogleCalendarService
{
    /// <summary>
    /// Web or installed-app OAuth client ID from Google Cloud Console.
    /// Leave empty until configured; Connect will explain how to set it up.
    /// You can also store a device override under Secure Storage key jobflow.google.client_id.
    /// </summary>
    public const string DefaultClientId = "";

    public const string RedirectUri = "com.tmassey1979.jobflowmobile:/oauth2redirect";
    public const string CalendarReadonlyScope = "https://www.googleapis.com/auth/calendar.readonly";

    private const string AccessTokenKey = "jobflow.google.access_token";
    private const string RefreshTokenKey = "jobflow.google.refresh_token";
    private const string TokenExpiryKey = "jobflow.google.token_expiry_utc";
    private const string ClientIdOverrideKey = "jobflow.google.client_id";
    private const string AccountEmailKey = "jobflow.google.account_email";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<string?> GetClientIdAsync()
    {
        try
        {
            var stored = await SecureStorage.Default.GetAsync(ClientIdOverrideKey);
            if (!string.IsNullOrWhiteSpace(stored)) return stored.Trim();
        }
        catch { /* fall through */ }

        return string.IsNullOrWhiteSpace(DefaultClientId) ? null : DefaultClientId.Trim();
    }

    public Task SetClientIdOverrideAsync(string clientId) =>
        SecureStorage.Default.SetAsync(ClientIdOverrideKey, clientId.Trim());

    public bool IsClientIdConfigured(string? clientId) => !string.IsNullOrWhiteSpace(clientId);

    public async Task<bool> IsConnectedAsync()
    {
        var token = await GetValidAccessTokenAsync();
        return !string.IsNullOrWhiteSpace(token);
    }

    public async Task<string?> GetAccountEmailAsync()
    {
        try { return await SecureStorage.Default.GetAsync(AccountEmailKey); }
        catch { return null; }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var clientId = await GetClientIdAsync();
        if (!IsClientIdConfigured(clientId))
        {
            throw new InvalidOperationException(
                "Google OAuth client ID is not set. In Google Cloud Console create an OAuth client, " +
                "enable the Google Calendar API, add redirect URI " + RedirectUri +
                ", then put the client ID in GoogleCalendarService.DefaultClientId (or set it from Integrations). " +
                "See docs/INTEGRATIONS-SETUP.md.");
        }

        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var authUrl =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            "?client_id=" + Uri.EscapeDataString(clientId!) +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&response_type=code" +
            "&scope=" + Uri.EscapeDataString(CalendarReadonlyScope) +
            "&code_challenge=" + Uri.EscapeDataString(challenge) +
            "&code_challenge_method=S256" +
            "&access_type=offline" +
            "&prompt=consent" +
            "&state=" + Uri.EscapeDataString(state);

        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri(authUrl),
                new Uri(RedirectUri));
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Google sign-in was canceled.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not complete Google sign-in in the browser. " + Safe(ex.Message));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (result.Properties.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            result.Properties.TryGetValue("error_description", out var desc);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(desc) ? $"Google error: {oauthError}" : desc);
        }

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Google did not return an authorization code. Check the OAuth client redirect URI matches " + RedirectUri);

        if (result.Properties.TryGetValue("state", out var returnedState)
            && !string.IsNullOrWhiteSpace(returnedState)
            && !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Google sign-in state mismatch. Try connecting again.");
        }

        await ExchangeCodeAsync(clientId!, code, verifier, cancellationToken);
        await RefreshAccountEmailAsync(cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://oauth2.googleapis.com/revoke?token=" + Uri.EscapeDataString(token));
                using var _ = await Http.SendAsync(request);
            }
        }
        catch { /* Local disconnect must succeed offline. */ }

        try { SecureStorage.Default.Remove(AccessTokenKey); } catch { }
        try { SecureStorage.Default.Remove(RefreshTokenKey); } catch { }
        try { SecureStorage.Default.Remove(TokenExpiryKey); } catch { }
        try { SecureStorage.Default.Remove(AccountEmailKey); } catch { }
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CalendarEventSummary>> GetUpcomingEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var token = await GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return Array.Empty<CalendarEventSummary>();

        var url =
            "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
            "?singleEvents=true" +
            "&orderBy=startTime" +
            "&timeMin=" + Uri.EscapeDataString(from.UtcDateTime.ToString("o")) +
            "&timeMax=" + Uri.EscapeDataString(to.UtcDateTime.ToString("o")) +
            "&maxResults=50";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            await DisconnectAsync();
            throw new InvalidOperationException("Google Calendar access expired or was revoked. Connect again from Integrations.");
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Could not load Google Calendar events.");

        var parsed = JsonSerializer.Deserialize<EventsResponse>(body, JsonOptions);
        if (parsed?.Items is null || parsed.Items.Count == 0)
            return Array.Empty<CalendarEventSummary>();

        var list = new List<CalendarEventSummary>();
        foreach (var item in parsed.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id)) continue;
            var (start, allDay) = ParseEventTime(item.Start);
            var (end, _) = ParseEventTime(item.End);
            list.Add(new CalendarEventSummary
            {
                Id = item.Id,
                Title = string.IsNullOrWhiteSpace(item.Summary) ? "(No title)" : item.Summary!,
                Start = start,
                End = end == default ? null : end,
                Location = item.Location,
                IsAllDay = allDay,
                Source = "Google"
            });
        }

        return list;
    }

    private async Task ExchangeCodeAsync(string clientId, string code, string verifier, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri
        });

        using var response = await Http.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Google token exchange failed. Confirm the OAuth client type allows this redirect URI and PKCE. " +
                Safe(ExtractOAuthError(body)));
        }

        using var doc = JsonDocument.Parse(body);
        await SaveTokensAsync(doc.RootElement);
    }

    private async Task SaveTokensAsync(JsonElement root)
    {
        if (!root.TryGetProperty("access_token", out var access) || string.IsNullOrWhiteSpace(access.GetString()))
            throw new InvalidOperationException("Google did not return an access token.");

        await SecureStorage.Default.SetAsync(AccessTokenKey, access.GetString()!);

        if (root.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrWhiteSpace(refresh.GetString()))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refresh.GetString()!);

        var expiresIn = 3600;
        if (root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
            expiresIn = Math.Max(60, seconds);

        var expiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
        await SecureStorage.Default.SetAsync(TokenExpiryKey, expiry.ToUnixTimeSeconds().ToString());
    }

    private async Task<string?> GetValidAccessTokenAsync()
    {
        string? token;
        try { token = await SecureStorage.Default.GetAsync(AccessTokenKey); }
        catch { return null; }

        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var expiryText = await SecureStorage.Default.GetAsync(TokenExpiryKey);
            if (long.TryParse(expiryText, out var unix))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(unix);
                if (expiry > DateTimeOffset.UtcNow.AddMinutes(1))
                    return token;
            }
            else
            {
                return token;
            }
        }
        catch
        {
            return token;
        }

        return await RefreshAccessTokenAsync();
    }

    private async Task<string?> RefreshAccessTokenAsync()
    {
        string? refresh;
        try { refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey); }
        catch { return null; }

        if (string.IsNullOrWhiteSpace(refresh)) return null;

        var clientId = await GetClientIdAsync();
        if (string.IsNullOrWhiteSpace(clientId)) return null;

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["refresh_token"] = refresh,
                ["grant_type"] = "refresh_token"
            });

            using var response = await Http.PostAsync("https://oauth2.googleapis.com/token", content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                await DisconnectAsync();
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            await SaveTokensAsync(doc.RootElement);
            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch
        {
            return null;
        }
    }

    private async Task RefreshAccountEmailAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetValidAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("email", out var email) && !string.IsNullOrWhiteSpace(email.GetString()))
                await SecureStorage.Default.SetAsync(AccountEmailKey, email.GetString()!);
        }
        catch { /* optional */ }
    }

    private static (DateTimeOffset Start, bool AllDay) ParseEventTime(EventTimeDto? dto)
    {
        if (dto is null) return (DateTimeOffset.UtcNow, false);
        if (!string.IsNullOrWhiteSpace(dto.DateTime)
            && DateTimeOffset.TryParse(dto.DateTime, out var timed))
            return (timed, false);

        if (!string.IsNullOrWhiteSpace(dto.Date)
            && DateTime.TryParse(dto.Date, out var day))
            return (new DateTimeOffset(DateTime.SpecifyKind(day.Date, DateTimeKind.Local)), true);

        return (DateTimeOffset.UtcNow, false);
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ExtractOAuthError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var d))
                return d.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("error", out var e))
                return e.GetString() ?? "";
        }
        catch { }
        return body.Length > 120 ? body[..120] : body;
    }

    private static string Safe(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Unknown error.";
        if (message.Contains("ya29.", StringComparison.OrdinalIgnoreCase)) return "Token error.";
        return message.Length > 200 ? message[..197] + "…" : message;
    }

    public sealed class CalendarEventSummary
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public DateTimeOffset Start { get; init; }
        public DateTimeOffset? End { get; init; }
        public string? Location { get; init; }
        public bool IsAllDay { get; init; }
        public string Source { get; init; } = "Google";

        public string WhenDisplay
        {
            get
            {
                if (IsAllDay) return $"All day · {Start.LocalDateTime:d}";
                return Start.LocalDateTime.ToString("g");
            }
        }
    }

    private sealed class EventsResponse
    {
        [JsonPropertyName("items")]
        public List<EventItemDto>? Items { get; set; }
    }

    private sealed class EventItemDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("start")]
        public EventTimeDto? Start { get; set; }

        [JsonPropertyName("end")]
        public EventTimeDto? End { get; set; }
    }

    private sealed class EventTimeDto
    {
        [JsonPropertyName("dateTime")]
        public string? DateTime { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }
}
