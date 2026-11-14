using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

public sealed class LocalAttachmentStorage(IWebHostEnvironment environment) : IAttachmentStorage
{
    private string PathFor(string key) => Path.Combine(environment.ContentRootPath, ".local", "attachments", key);

    public async Task SaveAsync(string key, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFor(key))!);
        await File.WriteAllBytesAsync(PathFor(key), content);
    }

    public async Task<byte[]?> ReadAsync(string key) => File.Exists(PathFor(key)) ? await File.ReadAllBytesAsync(PathFor(key)) : null;

    public void Delete(string key)
    {
        if (File.Exists(PathFor(key)))
            File.Delete(PathFor(key));
    }
}
