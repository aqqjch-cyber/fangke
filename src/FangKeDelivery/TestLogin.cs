using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery;

/// <summary>测试专用：绕过登录窗口直接建立已认证会话。</summary>
internal static class TestLogin
{
    public static AuthService AsAdmin(Repository repo)
    {
        var auth = new AuthService(repo);
        auth.EnsureDefaultAdmin();
        var r = auth.Login("admin", AuthService.DefaultAdminPassword);
        if (!r.Ok) throw new InvalidOperationException("测试登录失败：" + r.Message);
        return auth;
    }

    public static AuthService AsStaff(Repository repo)
    {
        var auth = new AuthService(repo);
        auth.EnsureDefaultAdmin();
        var r = auth.Login("staff", "staff123");
        if (!r.Ok) throw new InvalidOperationException("测试登录失败：" + r.Message);
        return auth;
    }
}
