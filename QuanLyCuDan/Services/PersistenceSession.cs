using QuanLyCuDan.Models;
namespace QuanLyCuDan.Services;

public sealed class PersistenceSession(MyStockContext db)
{
    public void ClearTracking() => db.ChangeTracker.Clear();
}
