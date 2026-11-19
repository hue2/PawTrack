using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Domain;

namespace PawTrack.Web.Data;

public class PawTrackDbContext(DbContextOptions<PawTrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Veterinarian> Veterinarians => Set<Veterinarian>();
    public DbSet<VetVisit> VetVisits => Set<VetVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.City);
        });

        modelBuilder.Entity<Pet>(e =>
        {
            e.HasOne(p => p.Owner).WithMany(u => u.Pets).HasForeignKey(p => p.UserId);
            e.HasIndex(p => p.Species);
        });

        modelBuilder.Entity<VetVisit>(e =>
        {
            e.HasOne(v => v.Pet).WithMany(p => p.Visits).HasForeignKey(v => v.PetId);
            e.HasOne(v => v.Veterinarian).WithMany(v => v.Visits).HasForeignKey(v => v.VeterinarianId);
            e.Property(v => v.Cost).HasPrecision(10, 2);
            e.HasIndex(v => v.VisitDate);
        });
    }
}
