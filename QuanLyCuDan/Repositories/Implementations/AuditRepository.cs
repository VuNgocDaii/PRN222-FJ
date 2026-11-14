using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class AuditRepository(MyStockContext db) : IAuditRepository
{
    public void Add(string actorUserId, string eventCode, string targetType, string targetId, string? safeMetadata = null) =>
        db.Activities.Add(new Models.Activity
        {
            ActivityType = "AUDIT",
            ActorUserId = actorUserId,
            EventCode = eventCode,
            TargetType = targetType,
            TargetId = targetId,
            SafeMetadata = safeMetadata,
            OccurredAt = DateTime.UtcNow
        });
}
