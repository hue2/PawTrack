using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Data;
using PawTrack.Web.Domain;

namespace PawTrack.Web.Queries;

public class AppointmentQueries(PawTrackDbContext db)
{
    // Appointments with the given status that haven't happened yet, soonest first.
    public async Task<PagedResult<AppointmentRow>> GetFutureAsync(
        AppointmentStatus status, int page, int pageSize, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var query =
            from appt in db.Appointments
            join pet in db.Pets on appt.PetId equals pet.Id
            join owner in db.Users on pet.UserId equals owner.Id
            join vet in db.Veterinarians on appt.VeterinarianId equals vet.Id
            where appt.Status == status && appt.ScheduledAt >= now
            select new { appt, pet, owner, vet };

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.appt.ScheduledAt)
            .ThenBy(x => x.appt.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AppointmentRow(
                x.appt.Id,
                x.appt.ScheduledAt,
                x.appt.RequestedAt,
                x.owner.FirstName + " " + x.owner.LastName,
                x.owner.Email,
                x.owner.City,
                x.pet.Name,
                x.pet.Species,
                x.pet.Breed,
                x.vet.Name,
                x.vet.Clinic,
                x.appt.Reason))
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResult<AppointmentRow>(items, page, pageSize, total);
    }
}
