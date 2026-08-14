const int MaxTodoLength = 140;

string port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Keep stdout quiet; this app has nothing it needs to log routinely.
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Permissive CORS: in the intended setup, todo-app and todo-backend sit
// behind the same Ingress on different paths, so browser requests are
// same-origin and this never comes into play. Left on defensively for any
// setup where they end up on different origins (e.g. local dev without an
// Ingress in front).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

// In-memory store only for now - resets on restart, a database comes later.
// Seeded with the same items that used to be hardcoded in todo-app's page.
List<Todo> todos =
[
    new Todo(1, "Learn Docker"),
    new Todo(2, "Learn Kubernetes"),
    new Todo(3, "Water the plants"),
];
int nextId = 4;
object todosLock = new();

app.MapGet("/todos", () =>
{
    lock (todosLock)
    {
        return Results.Ok(todos.ToArray());
    }
});

app.MapPost("/todos", (TodoInput input) =>
{
    string text = input.Text?.Trim() ?? string.Empty;
    if (string.IsNullOrEmpty(text) || text.Length > MaxTodoLength)
    {
        return Results.BadRequest(new { error = $"text must be 1-{MaxTodoLength} characters" });
    }

    Todo created;
    lock (todosLock)
    {
        created = new Todo(nextId++, text);
        todos.Add(created);
    }

    return Results.Created($"/todos/{created.Id}", created);
});

app.Run();

record Todo(int Id, string Text);

record TodoInput(string? Text);
