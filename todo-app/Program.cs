// Port is configurable via the PORT environment variable, defaulting to 3000.
string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// Where the cached random picture lives. Put this on a volume (the
// manifests' job) so it survives container restarts and isn't re-fetched
// from Lorem Picsum on every single request.
string imageCachePath = Environment.GetEnvironmentVariable("IMAGE_CACHE_PATH") ?? "/image-cache/picture.jpg";

int cacheMinutes = int.TryParse(Environment.GetEnvironmentVariable("IMAGE_CACHE_MINUTES"), out int parsedMinutes)
    ? parsedMinutes
    : 10;
TimeSpan cacheDuration = TimeSpan.FromMinutes(cacheMinutes);

string? imageCacheDir = Path.GetDirectoryName(imageCachePath);
if (!string.IsNullOrEmpty(imageCacheDir))
{
    Directory.CreateDirectory(imageCacheDir);
}

using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

// Guards both the "nothing cached yet" wait and the "only one background
// refresh at a time" case below.
SemaphoreSlim refreshLock = new(1, 1);

async Task RefreshImageAsync()
{
    byte[] bytes = await httpClient.GetByteArrayAsync("https://picsum.photos/1200");
    await File.WriteAllBytesAsync(imageCachePath, bytes);
}

async Task<byte[]> GetCachedImageAsync()
{
    if (!File.Exists(imageCachePath))
    {
        // Nothing cached at all yet (e.g. a fresh volume) — nothing to fall
        // back on, so this request has to actually wait for a real fetch.
        await refreshLock.WaitAsync();
        try
        {
            if (!File.Exists(imageCachePath)) // another request may have won the race
            {
                await RefreshImageAsync();
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }
    else if (DateTime.UtcNow - File.GetLastWriteTimeUtc(imageCachePath) >= cacheDuration)
    {
        // Stale: this request still gets served the current picture (per
        // spec, the old one is fine "one more time"), but kicks off a single
        // background refresh so the *next* request gets a new one.
        if (await refreshLock.WaitAsync(0))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshImageAsync();
                }
                catch
                {
                    // Keep serving the stale image if Lorem Picsum is unreachable;
                    // it'll just retry on the next stale request.
                }
                finally
                {
                    refreshLock.Release();
                }
            });
        }
    }

    return await File.ReadAllBytesAsync(imageCachePath);
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Print once Kestrel is actually listening, not just once we've asked it to start.
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"Server started in port {port}");
});

// Serve wwwroot/index.html for GET / (and any other static assets placed there).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/image", async () =>
{
    try
    {
        byte[] bytes = await GetCachedImageAsync();
        return Results.File(bytes, "image/jpeg");
    }
    catch (HttpRequestException)
    {
        // Nothing cached yet and Lorem Picsum couldn't be reached either.
        return Results.Text("Image not available right now.", statusCode: 503);
    }
});

app.Run();
