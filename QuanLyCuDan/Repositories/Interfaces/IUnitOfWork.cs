using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace QuanLyCuDan.Repositories.Interfaces;

// Lưu thay đổi, mở transaction và kiểm tra RowVersion (tránh ghi đè dữ liệu cũ).
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted);
    void SetRowVersion(object entity, string version);
}
