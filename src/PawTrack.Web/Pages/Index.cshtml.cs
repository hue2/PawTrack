using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PawTrack.Web.Infrastructure;
using PawTrack.Web.Queries;

namespace PawTrack.Web.Pages;

public class IndexModel(ICacheStore cache, VetVisitQueries queries) : PageModel
{
    public const int PageSize = 500;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? Species { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    public PagedResult<VisitRow> Visits { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        PageNumber = Math.Max(PageNumber, 1);
        Visits = await cache.GetOrSetAsync(
            $"visits:{PageNumber}:{PageSize}:{Species}:{City}",
            TimeSpan.FromMinutes(1),
            () => queries.GetVisitsAsync(PageNumber, PageSize, Species, City, HttpContext.RequestAborted));
    }
}
