using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Data;
using PawTrack.Web.Domain;
using PawTrack.Web.Infrastructure;
using PawTrack.Web.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<PawTrackDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PawTrack")));
builder.Services.AddScoped<VetVisitQueries>();
builder.Services.AddScoped<AppointmentQueries>();
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

// status=upcoming (default) lists confirmed appointments; status=pending lists requests awaiting confirmation.
api.MapGet("/appointments", async (string? status, int? page, int? pageSize, ICacheStore cache, AppointmentQueries queries, CancellationToken ct) =>
{
    AppointmentStatus? apptStatus = NullIfEmpty(status)?.ToLowerInvariant() switch
    {
        null or "upcoming" => AppointmentStatus.Confirmed,
        "pending" => AppointmentStatus.Pending,
        _ => null,
    };
    if (apptStatus is not { } s)
    {
        return Results.BadRequest(new { error = "status must be 'upcoming' or 'pending'." });
    }

    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 250, 1, 1000);
    return Results.Ok(await cache.GetOrSetAsync($"appointments:{s}:{p}:{size}", TimeSpan.FromMinutes(1),
        () => queries.GetFutureAsync(s, p, size, ct)));
});

api.MapFallback(() => Results.NotFound());

// Any other path is a client-side route in the React app.
app.MapFallbackToFile("index.html");

app.Run();

// An empty query value (e.g. ?species=) means "no filter", not "match an empty string".
static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
