using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IRegistrationService
{
    Task EnsureReviewerAsync(ClaimsPrincipal actor);
    Task<RegistrationListViewModel> ListAsync(ClaimsPrincipal actor, string status);
    Task ReviewAsync(ClaimsPrincipal actor, RegistrationReviewInput input);
}
