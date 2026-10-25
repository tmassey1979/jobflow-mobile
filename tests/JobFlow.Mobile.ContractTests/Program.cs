using JobFlow.Mobile.Services;

var tests = new (string Name, Action Run)[]
{
    ("parses a valid feed and preserves source metadata", ParsesValidFeed),
    ("removes duplicate stable job IDs without case sensitivity", RemovesDuplicateIds),
    ("rejects unsupported schema versions", RejectsUnsupportedSchema),
    ("rejects missing required job fields", RejectsInvalidJobs),
    ("selects the newest strictly dated feed file", SelectsLatestFeed),
    ("returns null when no dated feed file exists", HandlesNoDatedFeed),
    ("rejects malformed JSON", RejectsMalformedJson)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} contract tests passed.");
return failures == 0 ? 0 : 1;

static void ParsesValidFeed()
{
    var feed = DailyFeedParser.Parse(FeedJson());
    Assert(feed.SchemaVersion == 1, "schema_version should be 1");
    Assert(feed.Jobs.Count == 1, "one job should be parsed");
    Assert(feed.Jobs[0].Title == "Senior Software Engineer", "job title should be preserved");
    Assert(feed.Criteria?.MinimumAnnualCompensationUsd == 110000m, "criteria should be preserved");
}

static void RemovesDuplicateIds()
{
    var json = FeedJson().Replace(
        "\"jobs\":[{",
        "\"jobs\":[{")
        .Replace(
            "\"application_tracking_path\":\"applications/demo.json\"}]",
            "\"application_tracking_path\":\"applications/demo.json\"},{\"job_id\":\"DEMO-1\",\"title\":\"Duplicate\",\"company\":\"Other\"}]");
    var feed = DailyFeedParser.Parse(json);
    Assert(feed.Jobs.Count == 1, "duplicate IDs should collapse to one job");
    Assert(feed.Jobs[0].Title == "Senior Software Engineer", "the first occurrence should win");
}

static void RejectsUnsupportedSchema()
{
    ExpectThrows<NotSupportedException>(() => DailyFeedParser.Parse(FeedJson().Replace("\"schema_version\":1", "\"schema_version\":2")));
}

static void RejectsInvalidJobs()
{
    var json = FeedJson().Replace("\"job_id\":\"demo-1\",", "\"job_id\":\"\",");
    ExpectThrows<InvalidOperationException>(() => DailyFeedParser.Parse(json));
}

static void SelectsLatestFeed()
{
    var latest = DailyFeedParser.SelectLatestFeedFile(new[]
    {
        "README.md", "2026-10-22.json", "2026-10-24.json", "2026-10-23.json",
        "2026-10-25.json.tmp", "2026-1-2.json"
    });
    Assert(latest == "2026-10-24.json", "latest exact YYYY-MM-DD filename should be selected");
}

static void HandlesNoDatedFeed()
{
    Assert(DailyFeedParser.SelectLatestFeedFile(new[] { "README.md", "not-a-date.json" }) is null,
        "non-dated files should be ignored");
}

static void RejectsMalformedJson()
{
    ExpectThrows<System.Text.Json.JsonException>(() => DailyFeedParser.Parse("{not json"));
}

static string FeedJson() => """
{
  "schema_version": 1,
  "generated_at": "2026-10-24T20:34:22-05:00",
  "criteria": { "remote_only": true, "minimum_annual_compensation_usd": 110000 },
  "verification_summary": { "qualifying_jobs": 1 },
  "jobs": [{
    "job_id": "demo-1",
    "title": "Senior Software Engineer",
    "company": "Example Co",
    "salary_min_usd": 150000,
    "salary_max_usd": 180000,
    "salary_pay_type": "base_salary",
    "remote_scope": "Remote — United States",
    "verified_at": "2026-10-25T01:34:22Z",
    "application_url": "https://example.com/careers/demo",
    "match_score": 95,
    "metadata_path": "jobs/demo-1/metadata.json",
    "resume_path": "jobs/demo-1/resume.md",
    "cover_letter_path": "jobs/demo-1/cover-letter.md",
    "application_tracking_path": "applications/demo.json"
  }]
}
""";

static void ExpectThrows<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
