using System.Data;
using System.Security.Claims;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class PermissionService(
    IAdminGuard guard,
    IPermissionChecker checker,
    IPermissionCatalogRepository catalog,
    IAuditRepository audit,
    IUnitOfWork unitOfWork) : IPermissionService
{
    public async Task<List<Permission>> ListAsync(ClaimsPrincipal actor)
    {
        await guard.RequireAsync(actor, "permissions.read");
        return await catalog.ListAsync();
    }

    public async Task<int> CreateAsync(ClaimsPrincipal actor, PermissionCreateInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "permissions.create");
        var scopes = ValidateScopes(input.Scopes);
        if (await catalog.CodeExistsAsync(input.Code))
            throw new AppFault(409, "Mã Permission đã tồn tại.");
        var permission = new Permission
        {
            Code = input.Code,
            Name = input.Name.Trim(),
            Module = input.Module.ToUpperInvariant(),
            Description = input.Description?.Trim(),
            PermissionAllowedScopes = scopes.Select(scope => new PermissionAllowedScope { ScopeType = scope }).ToList()
        };
        catalog.Add(permission);
        await unitOfWork.SaveChangesAsync();
        audit.Add(checker.UserId(actor), "PERMISSION_CREATED", "Permission", permission.Id.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return permission.Id;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, int id, PermissionUpdateInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "permissions.update");
        var permission = await FindAsync(id, input.Version);
        var scopes = ValidateScopes(input.Scopes);
        var existing = permission.PermissionAllowedScopes.Select(scope => scope.ScopeType).ToHashSet();
        if (!existing.SetEquals(scopes))
        {
            if (permission.Roles.Count > 0)
                throw new AppFault(409, "Không thể đổi phạm vi của Permission đang được gán cho Role.");
            catalog.RemoveScopes(permission.PermissionAllowedScopes.Where(scope => !scopes.Contains(scope.ScopeType)));
            foreach (var scope in scopes.Where(scope => !existing.Contains(scope)))
                permission.PermissionAllowedScopes.Add(new PermissionAllowedScope { ScopeType = scope });
        }
        permission.Name = input.Name.Trim();
        permission.Module = input.Module.ToUpperInvariant();
        permission.Description = input.Description?.Trim();
        audit.Add(checker.UserId(actor), "PERMISSION_UPDATED", "Permission", id.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(ClaimsPrincipal actor, int id, PermissionDeleteInput input)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
        await guard.RequireAsync(actor, "permissions.delete");
        var permission = await FindAsync(id, input.Version);
        if (permission.Roles.Count > 0)
            throw new AppFault(409, "Permission đang được gán cho Role. Hãy gỡ khỏi các Role trước khi xóa.");
        catalog.RemoveScopes(permission.PermissionAllowedScopes);
        catalog.Remove(permission);
        audit.Add(checker.UserId(actor), "PERMISSION_DELETED", "Permission", id.ToString());
        await unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<Permission> FindAsync(int id, string version)
    {
        var permission = await catalog.FindWithDetailsAsync(id) ?? throw new AppFault(404, "Không tìm thấy Permission.");
        if (VersionOf(permission) != version)
            throw new AppFault(409, "Permission đã thay đổi. Hãy tải lại danh sách.");
        return permission;
    }

    private static string[] ValidateScopes(string[] scopes)
    {
        var allowed = new[] { "SYSTEM", "SELF", "AREA", "GROUP" };
        var selected = scopes.Distinct().OrderBy(scope => scope).ToArray();
        if (selected.Length == 0 || selected.Any(scope => !allowed.Contains(scope)))
            throw new AppFault(400, "Chọn ít nhất một phạm vi hợp lệ.");
        return selected;
    }

    public static string VersionOf(Permission permission)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            permission.Code,
            permission.Name,
            permission.Module,
            permission.Description,
            Scopes = permission.PermissionAllowedScopes.Select(scope => scope.ScopeType).OrderBy(scope => scope).ToArray()
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
    }
}
