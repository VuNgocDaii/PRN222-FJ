namespace QuanLyCuDan.Services.Interfaces;

// Lưu tệp đính kèm của hồ sơ ngoài cơ sở dữ liệu.
public interface IAttachmentStorage
{
    Task SaveAsync(string key, byte[] content);
    Task<byte[]?> ReadAsync(string key);
    void Delete(string key);
}
