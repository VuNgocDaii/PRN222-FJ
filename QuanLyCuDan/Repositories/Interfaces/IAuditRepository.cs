namespace QuanLyCuDan.Repositories.Interfaces;

public interface IAuditRepository
{
    // Chỉ thêm vào context; người gọi chịu trách nhiệm SaveChanges.
    void Add(string actorUserId, string eventCode, string targetType, string targetId, string? safeMetadata = null);
}
