using FangKeDelivery.Data;

namespace FangKeDelivery.Core;

public sealed record LoginResult(bool Ok, string Message, User? User);

/// <summary>登录鉴权与当前会话。连续失败 5 次锁 5 分钟，防止暴力猜密码。</summary>
public sealed class AuthService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(5);

    private readonly Repository _repo;
    private readonly Dictionary<string, (int Count, DateTime Until)> _failures = new(StringComparer.OrdinalIgnoreCase);

    public AuthService(Repository repo) => _repo = repo;

    /// <summary>当前登录用户；未登录为 null。</summary>
    public User? Current { get; private set; }

    public bool IsAdmin => Current?.IsAdmin == true;

    public static string DefaultAdminPassword => "admin123";

    /// <summary>登录窗口底部提示：首次使用时告知默认账号。</summary>
    public string HasAnyUserHint() =>
        _repo.HasAnyUser()
            ? ""
            : $"首次使用：管理员 admin / {DefaultAdminPassword}，员工 staff / staff123";

    /// <summary>首次运行没有账号时创建默认管理员。</summary>
    public void EnsureDefaultAdmin()
    {
        if (_repo.HasAnyUser()) return;

        _repo.InsertUser(new User
        {
            UserName = "admin",
            DisplayName = "系统管理员",
            Role = UserRole.管理员,
            IsEnabled = true
        }, DefaultAdminPassword);

        _repo.InsertUser(new User
        {
            UserName = "staff",
            DisplayName = "普通员工",
            Role = UserRole.员工,
            IsEnabled = true
        }, "staff123");
    }

    public LoginResult Login(string userName, string password)
    {
        userName = (userName ?? "").Trim();
        if (userName.Length == 0) return new LoginResult(false, "请输入用户名", null);
        if (string.IsNullOrEmpty(password)) return new LoginResult(false, "请输入密码", null);

        if (_failures.TryGetValue(userName, out var st) && st.Count >= MaxAttempts)
        {
            if (DateTime.Now < st.Until)
            {
                var left = (int)Math.Ceiling((st.Until - DateTime.Now).TotalSeconds);
                return new LoginResult(false, $"密码错误次数过多，请 {left} 秒后再试", null);
            }
            _failures.Remove(userName);
        }

        var cred = _repo.GetCredentials(userName);
        // 用户不存在时也要走一次散列运算，避免通过响应速度探测账号是否存在
        if (cred is null)
        {
            PasswordHasher.Verify(password, Convert.ToBase64String(new byte[32]), Convert.ToBase64String(new byte[16]));
            RecordFailure(userName);
            return new LoginResult(false, "用户名或密码错误", null);
        }

        if (!PasswordHasher.Verify(password, cred.Value.Hash, cred.Value.Salt))
        {
            RecordFailure(userName);
            return new LoginResult(false, "用户名或密码错误", null);
        }

        var user = _repo.FindUser(userName);
        if (user is null) return new LoginResult(false, "用户名或密码错误", null);

        if (!user.IsEnabled)
            return new LoginResult(false, "该账号已停用，请联系管理员", null);

        _failures.Remove(userName);
        _repo.TouchLastLogin(user.Id);
        Current = user;
        return new LoginResult(true, "登录成功", user);
    }

    public void Logout() => Current = null;

    /// <summary>修改自己的密码，需要校验原密码。</summary>
    public (bool Ok, string Message) ChangeOwnPassword(string oldPassword, string newPassword)
    {
        if (Current is null) return (false, "尚未登录");

        var cred = _repo.GetCredentials(Current.UserName);
        if (cred is null) return (false, "账号不存在");
        if (!PasswordHasher.Verify(oldPassword, cred.Value.Hash, cred.Value.Salt))
            return (false, "原密码不正确");

        var weak = PasswordHasher.Validate(newPassword);
        if (weak is not null) return (false, weak);
        if (oldPassword == newPassword) return (false, "新密码不能与原密码相同");

        _repo.SetPassword(Current.Id, newPassword);
        return (true, "密码已修改");
    }

    private void RecordFailure(string userName)
    {
        _failures.TryGetValue(userName, out var st);
        var count = st.Count + 1;
        _failures[userName] = (count, DateTime.Now + Lockout);
    }

    // ---------- 用户管理（仅管理员） ----------

    public (bool Ok, string Message) CreateUser(string userName, string displayName,
        UserRole role, string password)
    {
        if (!IsAdmin) return (false, "只有管理员可以新增用户");

        userName = (userName ?? "").Trim();
        if (userName.Length < 2) return (false, "用户名至少 2 个字符");
        if (userName.Length > 20) return (false, "用户名不能超过 20 个字符");
        if (userName.Any(char.IsWhiteSpace)) return (false, "用户名不能包含空格");
        if (_repo.UserExists(userName)) return (false, "该用户名已存在");

        var weak = PasswordHasher.Validate(password);
        if (weak is not null) return (false, weak);

        _repo.InsertUser(new User
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim(),
            Role = role,
            IsEnabled = true
        }, password);

        return (true, $"已新增用户 {userName}");
    }

    public (bool Ok, string Message) UpdateUser(User user, string displayName, UserRole role, bool enabled)
    {
        if (!IsAdmin) return (false, "只有管理员可以修改用户");

        // 不允许把最后一个可用管理员降级或停用，否则没人能管理系统了
        if (user.IsAdmin && (!enabled || role != UserRole.管理员))
        {
            if (_repo.CountAdmins() <= 1)
                return (false, "至少要保留一个启用的管理员账号");
        }

        _repo.UpdateUserProfile(user.Id, displayName, role, enabled);
        return (true, "已保存");
    }

    public (bool Ok, string Message) ResetPassword(User user, string newPassword)
    {
        if (!IsAdmin) return (false, "只有管理员可以重置密码");

        var weak = PasswordHasher.Validate(newPassword);
        if (weak is not null) return (false, weak);

        _repo.SetPassword(user.Id, newPassword);
        return (true, $"已重置 {user.UserName} 的密码");
    }

    public (bool Ok, string Message) DeleteUser(User user)
    {
        if (!IsAdmin) return (false, "只有管理员可以删除用户");
        if (user.Id == Current?.Id) return (false, "不能删除当前登录的账号");
        if (user.IsAdmin && _repo.CountAdmins() <= 1)
            return (false, "至少要保留一个启用的管理员账号");

        _repo.DeleteUser(user.Id);
        return (true, $"已删除用户 {user.UserName}");
    }
}
