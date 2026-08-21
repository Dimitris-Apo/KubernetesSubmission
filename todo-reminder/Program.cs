using System.Text;
using System.Text.Json;

// Where to fetch a random article from, and where to post the reminder
// todo to. Both overridable via env vars, both default to the real
// in-cluster values so no manifest overrides are strictly required.
string todoBackendUrl = Environment.GetEnvironmentVariable("TODO_BACKEND_URL") ?? "http://todo-backend-svc:2345/todos";
string wikipediaUrl = Environment.GetEnvironmentVariable("WIKIPEDIA_RANDOM_URL") ?? "https://en.wikipedia.org/api/rest_v1/page/random/summary";

using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
// Wikipedia's API rejects requests with no (or a generic) User-Agent with a
// 403 - their etiquette policy requires a descriptive one identifying the
// client. HttpClient sends none by default.
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("todo-reminder-cronjob/1.0 (KubernetesProject learning app)");

try
{
    // 1. Ask Wikipedia for a genuinely random article - this single call
    // already resolves to one specific article, no redirect-following needed.
    using HttpResponseMessage wikiResponse = await httpClient.GetAsync(wikipediaUrl);
    wikiResponse.EnsureSuccessStatusCode();
    string wikiJson = await wikiResponse.Content.ReadAsStringAsync();

    using JsonDocument doc = JsonDocument.Parse(wikiJson);
    string articleUrl = doc.RootElement
        .GetProperty("content_urls")
        .GetProperty("desktop")
        .GetProperty("page")
        .GetString()
        ?? throw new InvalidOperationException("Wikipedia response had no article URL.");

    // 2. Create the reminder todo with that concrete URL embedded in the text.
    string todoText = $"Read {articleUrl}";
    string payload = JsonSerializer.Serialize(new { text = todoText });
    using StringContent content = new(payload, Encoding.UTF8, "application/json");
    using HttpResponseMessage todoResponse = await httpClient.PostAsync(todoBackendUrl, content);

    if (!todoResponse.IsSuccessStatusCode)
    {
        string body = await todoResponse.Content.ReadAsStringAsync();
        Console.Error.WriteLine($"todo-backend rejected the reminder: {(int)todoResponse.StatusCode} {body}");
        Environment.Exit(1);
    }

    Console.WriteLine($"Created reminder: {todoText}");
}
catch (Exception ex)
{
    // Any failure here (Wikipedia unreachable, todo-backend unreachable, bad
    // response shape, etc.) should make the container exit non-zero, so the
    // Kubernetes Job correctly registers this run as failed instead of
    // silently doing nothing.
    Console.Error.WriteLine($"Failed to create Wikipedia reminder todo: {ex.Message}");
    Environment.Exit(1);
}
