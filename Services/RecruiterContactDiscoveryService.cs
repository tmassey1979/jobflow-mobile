using System.Net;
using System.Text.RegularExpressions;
using JobFlow.Mobile.Models;
using SQLite;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Finds only email addresses actually published on a job's public HTTPS source pages.
/// It never guesses employer email formats. Results remain in the local application database.
/// </summary>
public sealed class RecruiterContactDiscoveryService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(18)
    };

    private static readonly Regex EmailPattern = new(
        @"(?i)(?<![a-z0-9.!#$%&'*+/=?^`{|}~-])[a-z0-9.!#$%&'*+/=?^`{|}~-]+@[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly LocalDatabaseService _local = LocalDatabaseService.Shared;
    private readonly SQLiteAsyncConnection _db;

    public RecruiterContactDiscoveryService() => _db = _local.Connection;

    public async Task<IReadOnlyList<RecruiterContactRecord>> GetForJobAsync(string jobId)
    {
        await _local.InitializeAsync();
        return await _db.Table<RecruiterContactRecord>()
            .Where(contact => contact.JobId == jobId)
            .OrderByDescending(contact => contact.VerifiedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RecruiterContactRecord>> DiscoverAsync(
        JobFeedItem job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(job.JobId))
            throw new ArgumentException("A stable job ID is required.", nameof(job));

        await _local.InitializeAsync(cancellationToken);
        var sources = job.SourceUrls
            .Append(job.ApplicationUrl ?? "")
            .Where(IsSafeHttps)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, source);
                request.Headers.UserAgent.ParseAdd("JobFlowMobile/1.0 (public recruiter contact discovery)");
                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is not null
                    && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                    && !mediaType.Contains("text", StringComparison.OrdinalIgnoreCase))
                    continue;

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                if (html.Length > 2_000_000) html = html[..2_000_000];

                var candidates = ExtractEmails(html);
                foreach (var email in candidates)
                {
                    var id = $"{job.JobId}|{email.ToLowerInvariant()}";
                    var existing = await _db.FindAsync<RecruiterContactRecord>(id);
                    var record = existing ?? new RecruiterContactRecord
                    {
                        Id = id,
                        JobId = job.JobId,
                        Email = email,
                        Company = job.Company,
                        ProfessionalRole = "Public contact; role not stated",
                    };
                    record.SourceUrl = response.RequestMessage?.RequestUri?.ToString() ?? source;
                    record.VerifiedAtUtc = DateTime.UtcNow;
                    record.VerificationStatus = "Email published on fetched public page";
                    await _db.InsertOrReplaceAsync(record);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // One inaccessible job-board page must not block discovery from other sources.
            }
        }

        return await GetForJobAsync(job.JobId);
    }

    private static IReadOnlyList<string> ExtractEmails(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        decoded = Regex.Replace(decoded, @"(?is)<(script|style|noscript)[^>]*>.*?</\\1>", " ");
        decoded = Regex.Replace(decoded, @"(?s)<[^>]+>", " ");
        var roleContext = new Regex(@"(?i)(recruit(?:er|ing|ment)?|talent|human\\s+resources|\\bHR\\b|career|hiring|jobs?)");
        return EmailPattern.Matches(decoded)
            .Where(match =>
            {
                var start = Math.Max(0, match.Index - 140);
                var length = Math.Min(decoded.Length - start, match.Length + 280);
                var context = decoded.Substring(start, length);
                var localPart = match.Value.Split('@')[0];
                return roleContext.IsMatch(context)
                    || Regex.IsMatch(localPart, @"(?i)(recruit|talent|career|hiring|jobs?|hr)");
            })
            .Select(match => match.Value.Trim('.', ',', ';', ':', ')', ']', '}', '"', '\''))
            .Where(IsValidEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();
    }

    private static bool IsValidEmail(string value) =>
        value.Length <= 254
        && value.Count(character => character == '@') == 1
        && !value.StartsWith('.') && !value.EndsWith('.')
        && value.Contains('.', StringComparison.Ordinal)
        && !value.Contains("..", StringComparison.Ordinal);

    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host);
}
