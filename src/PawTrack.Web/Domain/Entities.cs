namespace PawTrack.Web.Domain;

public class User
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string City { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Pet> Pets { get; set; } = [];
}

public class Pet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Name { get; set; }
    public required string Species { get; set; }
    public required string Breed { get; set; }
    public DateOnly DateOfBirth { get; set; }

    public User Owner { get; set; } = null!;
    public List<VetVisit> Visits { get; set; } = [];
}

public class Veterinarian
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Clinic { get; set; }
    public required string Specialty { get; set; }

    public List<VetVisit> Visits { get; set; } = [];
}

public class VetVisit
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public int VeterinarianId { get; set; }
    public DateTime VisitDate { get; set; }
    public required string Reason { get; set; }
    public required string Diagnosis { get; set; }
    public decimal Cost { get; set; }

    public Pet Pet { get; set; } = null!;
    public Veterinarian Veterinarian { get; set; } = null!;
}

public enum AppointmentStatus
{
    Pending,
    Confirmed,
}

public class Appointment
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public int VeterinarianId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime RequestedAt { get; set; }
    public required string Reason { get; set; }
    public AppointmentStatus Status { get; set; }

    public Pet Pet { get; set; } = null!;
    public Veterinarian Veterinarian { get; set; } = null!;
}
