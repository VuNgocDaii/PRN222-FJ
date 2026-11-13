using QuanLyCuDan.Models;

namespace QuanLyCuDan.Services;

public static class RoleScopeRules
{
    // Personal permissions coexist with the role's assigned business boundary.
    public static bool Supports(IEnumerable<Permission> permissions, string scope)
    {
        var items = permissions.ToArray();
        if (items.Length == 0) return false;
        var business = items.Where(p => !p.PermissionAllowedScopes.Any(s => s.ScopeType == "SELF")).ToArray();
        return business.Length == 0 ? scope == "SELF" :
            business.All(p => p.PermissionAllowedScopes.Any(s => s.ScopeType == scope));
    }
}
