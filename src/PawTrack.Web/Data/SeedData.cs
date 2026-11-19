using Microsoft.EntityFrameworkCore;
using PawTrack.Web.Domain;

namespace PawTrack.Web.Data;

public static class SeedData
{
    private static readonly string[] FirstNames = ["Ava", "Liam", "Mia", "Noah", "Zoe", "Ethan", "Lily", "Mason", "Chloe", "Lucas", "Nora", "Owen", "Ivy", "Leo", "Ruby", "Kai", "Hana", "Omar", "Priya", "Diego"];
    private static readonly string[] LastNames = ["Nguyen", "Smith", "Garcia", "Patel", "Kim", "Johnson", "Lopez", "Chen", "Brown", "Martin", "Silva", "Okafor", "Walsh", "Novak", "Haddad", "Tanaka"];
    private static readonly string[] Cities = ["Austin", "Denver", "Seattle", "Portland", "Chicago", "Boston", "Atlanta", "Phoenix", "San Diego", "Miami"];
    private static readonly string[] PetNames = ["Bella", "Max", "Luna", "Charlie", "Milo", "Daisy", "Rocky", "Coco", "Bear", "Pepper", "Ziggy", "Olive", "Mochi", "Biscuit", "Nala", "Toby"];

    private static readonly Dictionary<string, string[]> BreedsBySpecies = new()
    {
        ["Dog"] = ["Labrador", "Beagle", "Poodle", "Bulldog", "German Shepherd", "Corgi"],
        ["Cat"] = ["Siamese", "Maine Coon", "Persian", "Bengal", "Domestic Shorthair"],
        ["Rabbit"] = ["Holland Lop", "Rex", "Lionhead"],
        ["Bird"] = ["Cockatiel", "Budgie", "Parrot"],
        ["Reptile"] = ["Leopard Gecko", "Bearded Dragon", "Ball Python"],
    };

    private static readonly string[] Clinics = ["Northside Animal Hospital", "Paws & Claws Clinic", "Riverbend Vet Center", "Oak Street Veterinary"];
    private static readonly string[] Specialties = ["General Practice", "Surgery", "Dermatology", "Dentistry", "Exotics", "Internal Medicine"];
    private static readonly (string Reason, string[] Diagnoses)[] Reasons =
    [
        ("Annual checkup", ["Healthy", "Overweight", "Mild tartar"]),
        ("Vaccination", ["Vaccines administered"]),
        ("Limping", ["Sprain", "Torn ligament", "Arthritis"]),
        ("Skin irritation", ["Allergy", "Flea dermatitis", "Hot spot"]),
        ("Vomiting", ["Gastritis", "Foreign body", "Dietary indiscretion"]),
        ("Dental cleaning", ["Cleaning completed", "Extraction required"]),
        ("Ear infection", ["Otitis externa", "Ear mites"]),
    ];

    public static async Task EnsureSeededAsync(PawTrackDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Seeding PawTrack database...");
        var rng = new Random(42);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var vets = Enumerable.Range(1, 40).Select(i => new Veterinarian
        {
            Name = $"Dr. {Pick(rng, FirstNames)} {Pick(rng, LastNames)}",
            Clinic = Pick(rng, Clinics),
            Specialty = Pick(rng, Specialties),
        }).ToList();
        db.Veterinarians.AddRange(vets);
        await db.SaveChangesAsync(ct);

        const int userCount = 5_000;
        for (var batchStart = 0; batchStart < userCount; batchStart += 500)
        {
            var users = new List<User>();
            for (var i = batchStart; i < batchStart + 500; i++)
            {
                var first = Pick(rng, FirstNames);
                var last = Pick(rng, LastNames);
                var user = new User
                {
                    FirstName = first,
                    LastName = last,
                    Email = $"{first}.{last}.{i}@example.com".ToLowerInvariant(),
                    City = Pick(rng, Cities),
                    CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(30, 3_000)),
                };

                var petCount = rng.Next(1, 5);
                for (var p = 0; p < petCount; p++)
                {
                    var species = Pick(rng, BreedsBySpecies.Keys.ToArray());
                    var pet = new Pet
                    {
                        Name = Pick(rng, PetNames),
                        Species = species,
                        Breed = Pick(rng, BreedsBySpecies[species]),
                        DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-rng.Next(90, 5_500))),
                    };

                    var visitCount = rng.Next(1, 9);
                    for (var v = 0; v < visitCount; v++)
                    {
                        var (reason, diagnoses) = Reasons[rng.Next(Reasons.Length)];
                        pet.Visits.Add(new VetVisit
                        {
                            VeterinarianId = vets[rng.Next(vets.Count)].Id,
                            VisitDate = DateTime.UtcNow.AddDays(-rng.Next(1, 1_800)).Date,
                            Reason = reason,
                            Diagnosis = Pick(rng, diagnoses),
                            Cost = Math.Round((decimal)(rng.NextDouble() * 600 + 45), 2),
                        });
                    }

                    user.Pets.Add(pet);
                }

                users.Add(user);
            }

            db.Users.AddRange(users);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        logger.LogInformation("Seeded {Users} users, {Pets} pets, {Visits} visits",
            await db.Users.CountAsync(ct), await db.Pets.CountAsync(ct), await db.VetVisits.CountAsync(ct));
    }

    private static T Pick<T>(Random rng, T[] items) => items[rng.Next(items.Length)];
}
