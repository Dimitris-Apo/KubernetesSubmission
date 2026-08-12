// Port is configurable via the PORT environment variable, defaulting to 3000.
string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

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

app.Run();
