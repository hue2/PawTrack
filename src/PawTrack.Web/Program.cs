using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Data;
using PawTrack.Web.Infrastructure;
using PawTrack.Web.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<PawTrackDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PawTrack")));
builder.Services.AddScoped<VetVisitQueries>();
builder.Services.AddRedisCache(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

api.MapGet("/visits", (int? page, int? pageSize, string? species, string? city, ICacheStore cache, VetVisitQueries queries, CancellationToken ct) =>
{
    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 250, 1, 1000);
    species = NullIfEmpty(species);
    city = NullIfEmpty(city);
    return cache.GetOrSetAsync($"visits:{p}:{size}:{species}:{city}", TimeSpan.FromMinutes(1),
        () => queries.GetVisitsAsync(p, size, species, city, ct));
});

api.MapGet("/owners", (int? page, int? pageSize, string? city, ICacheStore cache, VetVisitQueries queries, CancellationToken ct) =>
{
    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 250, 1, 1000);
    city = NullIfEmpty(city);
    return cache.GetOrSetAsync($"owners:{p}:{size}:{city}", TimeSpan.FromMinutes(1),
        () => queries.GetOwnerSummariesAsync(p, size, city, ct));
});

api.MapFallback(() => Results.NotFound());

// Any other path is a client-side route in the React app.
app.MapFallbackToFile("index.html");

app.Run();

// An empty query value (e.g. ?species=) means "no filter", not "match an empty string".
static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
