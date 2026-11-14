using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuanLyCuDan.Auth;
using QuanLyCuDan.Models;
using QuanLyCuDan.Repositories.Interfaces;

namespace QuanLyCuDan.Repositories.Implementations;

public sealed class UnitOfWork(MyStockContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync() => db.SaveChangesAsync();

    public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted) =>
        db.Database.BeginTransactionAsync(level);

    public void SetRowVersion(object entity, string version)
    {
        try
        {
            db.Entry(entity).Property("RowVersion").OriginalValue = Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw new AppFault(400, "RowVersion không hợp lệ.");
        }
    }
}
