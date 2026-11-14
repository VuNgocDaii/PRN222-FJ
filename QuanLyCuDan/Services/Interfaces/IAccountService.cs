using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IAccountService
{
    Task<AspNetUser> GetApplicantAsync(ClaimsPrincipal actor);
    Task RegisterAsync(RegisterInput input);
    Task<List<ResidentialUnit>> GetGroupsAsync();
    Task<bool> CanSubmitProfileAsync(ClaimsPrincipal actor);
    Task SubmitProfileAsync(ClaimsPrincipal actor, RegistrationProfileInput input, string rowVersion);
}
