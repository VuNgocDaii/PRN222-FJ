using System.Security.Claims;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services.Interfaces;

public interface IOrganizationService
{
    Task<OrganizationPage> GetAsync(ClaimsPrincipal actor, long? id);
    Task CreateAsync(ClaimsPrincipal actor, OrganizationInput input);
    Task SetMemberAsync(ClaimsPrincipal actor, long id, long residentId, bool active, string? rowVersion);
}
