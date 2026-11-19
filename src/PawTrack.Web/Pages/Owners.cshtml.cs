using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PawTrack.Web.Infrastructure;
using PawTrack.Web.Queries;

namespace PawTrack.Web.Pages;

public class OwnersModel(ICacheStore cache, VetVisitQueries queries) : PageModel
{
    public const int PageSize = 500;

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    public PagedResult<OwnerSummaryRow> Owners { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        PageNumber = Math.Max(PageNumber, 1);
        Owners = await cache.GetOrSetAsync(
            $"owners:{PageNumber}:{PageSize}:{City}",
            TimeSpan.FromMinutes(1),
            () => queries.GetOwnerSummariesAsync(PageNumber, PageSize, City, HttpContext.RequestAborted));
    }
}
