using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Data;
using PawTrack.Web.Infrastructure;
using PawTrack.Web.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<PawTrackDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PawTrack")));
builder.Services.AddScoped<VetVisitQueries>();
builder.Services.AddRedisCache(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PawTrackDbContext>();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(db.Database.GetDbConnection().DataSource))!);
    await SeedData.EnsureSeededAsync(db, app.Logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

api.MapGet("/visits", (int? page, int? pageSize, string? species, string? city, ICacheStore cache, VetVisitQueries queries) =>
{
    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 250, 1, 1000);
    return cache.GetOrSetAsync($"visits:{p}:{size}:{species}:{city}", TimeSpan.FromMinutes(1),
        () => queries.GetVisitsAsync(p, size, species, city));
});

api.MapGet("/owners", (int? page, int? pageSize, string? city, ICacheStore cache, VetVisitQueries queries) =>
{
    var p = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 250, 1, 1000);
    return cache.GetOrSetAsync($"owners:{p}:{size}:{city}", TimeSpan.FromMinutes(1),
        () => queries.GetOwnerSummariesAsync(p, size, city));
});

app.Run();
