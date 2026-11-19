namespace PawTrack.Web.Queries;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record VisitRow(
    int VisitId,
    DateTime VisitDate,
    string OwnerName,
    string OwnerEmail,
    string City,
    string PetName,
    string Species,
    string Breed,
    string VetName,
    string Clinic,
    string Reason,
    string Diagnosis,
    decimal Cost);

public record OwnerSummaryRow(
    int UserId,
    string OwnerName,
    string City,
    int PetCount,
    int VisitCount,
    decimal TotalSpend,
    DateTime? LastVisit);
