using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IPasswordService
{
    string Hash(AspNetUser user, string password);
    bool Verify(AspNetUser user, string password);
}
