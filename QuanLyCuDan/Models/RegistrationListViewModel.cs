namespace QuanLyCuDan.Models;

public class RegistrationListViewModel
{
    public List<AspNetUser> Users { get; set; } = new();
    public List<Resident> Residents { get; set; } = new();
    public Resident? MatchResident(string? citizenId) => string.IsNullOrWhiteSpace(citizenId)
        ? null : Residents.SingleOrDefault(r => r.CitizenId == citizenId.Trim());
    public Dictionary<long, string> HeadNames { get; set; } = new();
    public string Status { get; set; } = "PENDING";
    public List<ResidentialUnit> Groups { get; set; } = new();
    public List<Household> Households { get; set; } = new();
}
