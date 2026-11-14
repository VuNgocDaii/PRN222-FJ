using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class PermissionService(MyStockContext context) : ApplicationService(context)
{
    public async Task<List<Permission>> ListAsync(ClaimsPrincipal actor)
    {
        await RequirePermission(actor, "permissions.read");
        return await db.Permissions.AsNoTracking().Include(p => p.PermissionAllowedScopes).Include(p => p.Roles).OrderBy(p => p.Module).ThenBy(p => p.Code).ToListAsync();
    }
    public async Task<int> CreateAsync(ClaimsPrincipal actor, PermissionCreateInput input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "permissions.create");

        var scopes = ValidatePermissionScopesCore(input.Scopes);
        if (await db.Permissions.AnyAsync(item => item.Code == input.Code))
        {
            throw new AppFault(409, "Mã Permission đã tồn tại.");
        }

        var permission = new Permission
        {
            Code = input.Code,
            Name = input.Name.Trim(),
            Module = input.Module.ToUpperInvariant(),
            Description = input.Description?.Trim(),
            PermissionAllowedScopes = scopes.Select(scope => new PermissionAllowedScope
            {
                ScopeType = scope
            }).ToList()
        };

        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
        Audit(CurrentUserId(actor), "PERMISSION_CREATED", "Permission", permission.Id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return permission.Id;
    }

    public async Task UpdateAsync(ClaimsPrincipal actor, int id, PermissionUpdateInput input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "permissions.update");

        var permission = await FindPermissionCore(id, input.Version);
        var scopes = ValidatePermissionScopesCore(input.Scopes);
        var existingScopes = permission.PermissionAllowedScopes.Select(scope => scope.ScopeType).ToHashSet();
        if (!existingScopes.SetEquals(scopes))
        {
            if (permission.Roles.Count > 0)
            {
                throw new AppFault(409, "Không thể đổi phạm vi của Permission đang được gán cho Role.");
            }

            db.PermissionAllowedScopes.RemoveRange(permission.PermissionAllowedScopes.Where(scope => !scopes.Contains(scope.ScopeType)));
            foreach (var scope in scopes.Where(scope => !existingScopes.Contains(scope)))
            {
                permission.PermissionAllowedScopes.Add(new PermissionAllowedScope { ScopeType = scope });
            }
        }

        permission.Name = input.Name.Trim();
        permission.Module = input.Module.ToUpperInvariant();
        permission.Description = input.Description?.Trim();
        Audit(CurrentUserId(actor), "PERMISSION_UPDATED", "Permission", id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(ClaimsPrincipal actor, int id, PermissionDeleteInput input)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await RequirePermission(actor, "permissions.delete");

        var permission = await FindPermissionCore(id, input.Version);
        if (permission.Roles.Count > 0)
        {
            throw new AppFault(409, "Permission đang được gán cho Role. Hãy gỡ khỏi các Role trước khi xóa.");
        }

        db.PermissionAllowedScopes.RemoveRange(permission.PermissionAllowedScopes);
        db.Permissions.Remove(permission);
        Audit(CurrentUserId(actor), "PERMISSION_DELETED", "Permission", id.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<Permission> FindPermissionCore(int id, string version)
    {
        var permission = await db.Permissions
            .Include(item => item.PermissionAllowedScopes)
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == id)
            ?? throw new AppFault(404, "Không tìm thấy Permission.");

        if (VersionOf(permission) != version)
        {
            throw new AppFault(409, "Permission đã thay đổi. Hãy tải lại danh sách.");
        }

        return permission;
    }

    private static string[] ValidatePermissionScopesCore(string[] scopes)
    {
        var allowed = new[] { "SYSTEM", "SELF", "AREA", "GROUP" };
        var selected = scopes.Distinct().OrderBy(scope => scope).ToArray();
        if (selected.Length == 0 || selected.Any(scope => !allowed.Contains(scope)))
        {
            throw new AppFault(400, "Chọn ít nhất một phạm vi hợp lệ.");
        }

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
