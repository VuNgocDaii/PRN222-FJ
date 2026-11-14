using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using QuanLyCuDan.Models;
using QuanLyCuDan.Services.Interfaces;

namespace QuanLyCuDan.Services.Implementations;

// Băm và kiểm tra mật khẩu (hỗ trợ cả định dạng SHA256 cũ lẫn Identity).
public sealed class PasswordService : IPasswordService
{
    private static readonly PasswordHasher<AspNetUser> hasher = new();

    public string Hash(AspNetUser user, string password) => hasher.HashPassword(user, password);

    public bool Verify(AspNetUser user, string password)
    {
        if (user.PasswordHash is not { } stored)
            return false;
        if (!stored.StartsWith("SHA256$", StringComparison.Ordinal))
        {
            try { return hasher.VerifyHashedPassword(user, stored, password) != PasswordVerificationResult.Failed; }
            catch (Exception e) when (e is FormatException or ArgumentException) { return false; }
        }
        var parts = stored.Split('$');
        if (parts.Length != 3 || parts[1].Length != 32 || parts[2].Length != 64)
            return false;
        try
        {
            var salt = Convert.FromHexString(parts[1]);
            var bytes = Encoding.Unicode.GetBytes(password);
            var input = salt.Concat(bytes).ToArray();
            try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(parts[2]), SHA256.HashData(input)); }
            finally { CryptographicOperations.ZeroMemory(bytes); CryptographicOperations.ZeroMemory(input); }
        }
        catch (FormatException) { return false; }
    }
}
