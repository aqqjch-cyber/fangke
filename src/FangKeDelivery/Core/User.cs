using System.Text;
using System.Security.Cryptography;

namespace FangKeDelivery.Core;

public enum UserRole
{
    员工 = 0,
    管理员 = 1
}

public sealed class User
{
    public long Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.员工;
    public bool IsEnabled { get; set; } = true;
    public string CreatedAt { get; set; } = "";
    public string LastLoginAt { get; set; } = "";

    public bool IsAdmin => Role == UserRole.管理员;
}

/// <summary>PBKDF2-SHA256 口令散列。数据库不存明文，也不存可直接比对的散列。</summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 120_000;

    public static (string hash, string salt) Create(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, Iterations);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt)) return false;
        byte[] saltBytes, expected;
        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, saltBytes, Iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
    {
        // 不使用 Rfc2898DeriveBytes.Pbkdf2：它走系统 bcrypt，Wine 下会抛 0xc1000008。
        // 这里用托管实现，Windows / Wine 行为一致，且结果与标准 PBKDF2-HMAC-SHA256 相同。
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        using var hmac = new HMACSHA256(passwordBytes);

        var block = new byte[hmac.HashSize / 8];
        var u = hmac.ComputeHash(Concat(salt, [0, 0, 0, 1]));
        Buffer.BlockCopy(u, 0, block, 0, block.Length);

        var acc = (byte[])block.Clone();
        for (var i = 1; i < iterations; i++)
        {
            u = hmac.ComputeHash(u);
            for (var j = 0; j < acc.Length; j++) acc[j] ^= u[j];
        }

        return acc.Length == KeySize ? acc : acc[..KeySize];
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    /// <summary>密码强度要求：至少 6 位，且不能是纯数字或纯字母之外的单一重复字符。</summary>
    public static string? Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return "密码不能为空";
        if (password.Length < 6) return "密码至少 6 位";
        if (password.Length > 64) return "密码不能超过 64 位";
        if (password.Distinct().Count() == 1) return "密码不能是同一个字符重复";
        return null;
    }
}
