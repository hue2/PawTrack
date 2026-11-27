using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Data;

namespace PawTrack.Web.Queries;

// Each query creates and disposes its own DbContext: these run inside HybridCache factories, which can outlive the request
// that started them, and a request-scoped context would be disposed mid-query, leaking its connection from the pool.
public class VetVisitQueries(IDbContextFactory<PawTrackDbContext> dbFactory)
{
    public async Task<PagedResult<VisitRow>> GetVisitsAsync(
        int page, int pageSize, string? species, string? city, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query =
            from visit in db.VetVisits
            join pet in db.Pets on visit.PetId equals pet.Id
            join owner in db.Users on pet.UserId equals owner.Id
            join vet in db.Veterinarians on visit.VeterinarianId equals vet.Id
            where (species == null || pet.Species == species)
               && (city == null || owner.City == city)
            select new { visit, pet, owner, vet };

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.visit.VisitDate)
            .ThenBy(x => x.visit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VisitRow(
                x.visit.Id,
                x.visit.VisitDate,
                x.owner.FirstName + " " + x.owner.LastName,
                x.owner.Email,
                x.owner.City,
                x.pet.Name,
                x.pet.Species,
                x.pet.Breed,
                x.vet.Name,
                x.vet.Clinic,
                x.visit.Reason,
                x.visit.Diagnosis,
                x.visit.Cost))
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResult<VisitRow>(items, page, pageSize, total);
    }

    public async Task<PagedResult<OwnerSummaryRow>> GetOwnerSummariesAsync(
        int page, int pageSize, string? city, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var owners = db.Users.Where(u => city == null || u.City == city);
        var total = await owners.CountAsync(ct);

        var query =
            from owner in owners
            join pet in db.Pets on owner.Id equals pet.UserId
            join visit in db.VetVisits on pet.Id equals visit.PetId into petVisits
            from visit in petVisits.DefaultIfEmpty()
            group new { pet, visit } by new { owner.Id, owner.FirstName, owner.LastName, owner.City } into g
            orderby g.Key.LastName, g.Key.FirstName, g.Key.Id
            select new OwnerSummaryRow(
                g.Key.Id,
                g.Key.FirstName + " " + g.Key.LastName,
                g.Key.City,
                g.Select(x => x.pet.Id).Distinct().Count(),
                g.Count(x => x.visit != null),
                g.Sum(x => x.visit == null ? 0m : x.visit.Cost),
                g.Max(x => x.visit == null ? (DateTime?)null : x.visit.VisitDate));

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResult<OwnerSummaryRow>(items, page, pageSize, total);
    }
}
