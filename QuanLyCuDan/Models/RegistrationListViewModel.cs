namespace QuanLyCuDan.Models;

public class RegistrationListViewModel
{
    public List<AspNetUser> Users { get; set; } = new();
    public List<Resident> Residents { get; set; } = new();
    public string Status { get; set; } = "PENDING";
}
