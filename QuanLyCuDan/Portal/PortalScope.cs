using Microsoft.EntityFrameworkCore;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Portal;

public sealed record PortalLocality(long Id, string Code, string Name, string LocalityType)
{
    public string TypeLabel => LocalityType == "VILLAGE" ? "Thôn" : "Tổ dân phố";
}

// Only the public/resident controllers use this scope. Management queries and the shared EF model are unchanged.
// IConfiguration already includes appsettings, local settings, user secrets and environment variables.
public sealed class PortalScope(MyStockContext db, IConfiguration configuration)
{
    public async Task<PortalLocality> GetAsync()
    {
        var code = configuration["PortalScope:GroupCode"]?.Trim();
        var type = configuration["PortalScope:LocalityType"]?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 30 || type is not ("NEIGHBORHOOD" or "VILLAGE"))
            throw Unavailable();

        // GROUP is the existing database type for the served unit, including a village.
        // The display name always comes from the database; no arbitrary name or numeric ID is generated.
        var group = await db.ResidentialUnits.AsNoTracking().SingleOrDefaultAsync(g => g.Code == code &&
            g.UnitType == "GROUP" && g.IsActive && g.ParentUnit != null &&
            g.ParentUnit.UnitType == "AREA" && g.ParentUnit.IsActive);
        if (group == null) throw Unavailable();
        return new PortalLocality(group.Id, group.Code, group.Name, type);
    }

    private static AppFault Unavailable() => new(503,
        "Cổng công khai và cư dân chưa có địa bàn phục vụ hợp lệ hoặc địa bàn đã ngừng hoạt động. Vui lòng liên hệ ban quản lý.");
}
