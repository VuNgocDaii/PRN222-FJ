namespace QuanLyCuDan.Models;

public sealed class OrganizationDirectoryViewModel
{
    public bool IsAvailable { get; set; }
    public string Title { get; set; } = "Cơ cấu tổ chức";
    public string? Kind { get; set; }
    public List<OrganizationCard> Items { get; set; } = [];
}
public sealed record OrganizationCard(long Id, string Code, string Name, string Kind, string? Description, int PositionCount);
public sealed class OrganizationDetailViewModel
{
    public OrganizationCard Organization { get; set; } = null!;
    public string? SourceNote { get; set; }
    public bool IsResident { get; set; }
    public List<OrganizationPositionItem> Positions { get; set; } = [];
}
public sealed record OrganizationPositionItem(string Name, int? MinimumCount, int? MaximumCount, bool NeedsClarification,
    string? SourceNote, IReadOnlyList<string> PublishedNames)
{
    public string Quota => MinimumCount == null && MaximumCount == null ? "Chưa xác định" :
        MinimumCount == MaximumCount ? $"{MinimumCount} người" :
        MinimumCount != null && MaximumCount != null ? $"{MinimumCount}–{MaximumCount} người" :
        MinimumCount != null ? $"Từ {MinimumCount} người" : $"Tối đa {MaximumCount} người";
}
public sealed record MyOrganizationItem(long OrganizationId, string Name, string Kind, string? MembershipRole, IReadOnlyList<string> Positions);
public sealed class MyOrganizationsViewModel
{
    public bool IsAvailable { get; set; }
    public List<MyOrganizationItem> Items { get; set; } = [];
}
public static class OrganizationKinds
{
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string> {
        ["PARTY"] = "Khối Đảng", ["LOCAL_GOVERNMENT"] = "Khối chính quyền", ["FRONT"] = "Mặt trận",
        ["ASSOCIATION"] = "Đoàn thể, hội nhóm", ["SECURITY"] = "An ninh cơ sở", ["SOCIAL"] = "Hoạt động xã hội"
    };
    public static string Text(string kind) => Labels.GetValueOrDefault(kind, "Tổ chức cộng đồng");
}
