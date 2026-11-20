using FangKeDelivery.Core;
using FangKeDelivery.Data;
using FangKeDelivery.Printing;

namespace FangKeDelivery;

/// <summary>无人值守自检：校验金额大写、数据存取与打印渲染。</summary>
internal static class SelfTest
{
    public static int Run(Repository repo, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var log = new System.Text.StringBuilder();
        var failures = 0;

        void Check(string name, string actual, string expected)
        {
            var ok = actual == expected;
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}: \"{actual}\"{(ok ? "" : $"  expected \"{expected}\"")}");
        }

        // ---- 金额大写 ----
        Check("0", MoneyToChinese.Convert(0m), "零元整");
        Check("1", MoneyToChinese.Convert(1m), "壹元整");
        Check("10", MoneyToChinese.Convert(10m), "壹拾元整");
        Check("100", MoneyToChinese.Convert(100m), "壹佰元整");
        Check("1000", MoneyToChinese.Convert(1000m), "壹仟元整");
        Check("10000", MoneyToChinese.Convert(10000m), "壹万元整");
        Check("0.05", MoneyToChinese.Convert(0.05m), "伍分");
        Check("0.50", MoneyToChinese.Convert(0.50m), "伍角");
        Check("1.23", MoneyToChinese.Convert(1.23m), "壹元贰角叁分");
        Check("10.05", MoneyToChinese.Convert(10.05m), "壹拾元零伍分");
        Check("105", MoneyToChinese.Convert(105m), "壹佰零伍元整");
        Check("1005", MoneyToChinese.Convert(1005m), "壹仟零伍元整");
        Check("10500", MoneyToChinese.Convert(10500m), "壹万零伍佰元整");
        Check("100000", MoneyToChinese.Convert(100000m), "壹拾万元整");
        Check("1234567.89", MoneyToChinese.Convert(1234567.89m), "壹佰贰拾叁万肆仟伍佰陆拾柒元捌角玖分");
        Check("1000000", MoneyToChinese.Convert(1000000m), "壹佰万元整");
        Check("20000.10", MoneyToChinese.Convert(20000.10m), "贰万元壹角");
        Check("168.80", MoneyToChinese.Convert(168.80m), "壹佰陆拾捌元捌角");

        // ---- 数据存取 ----
        var customer = new Customer { Name = "自检测试客户", Contact = "王测试", Phone = "13700000000", Address = "测试路 1 号" };
        repo.SaveCustomer(customer);
        var reloaded = repo.GetCustomers("自检测试客户").FirstOrDefault();
        Check("customer saved", reloaded?.Name ?? "(null)", "自检测试客户");
        Check("customer phone", reloaded?.Phone ?? "", "13700000000");

        var note = new DeliveryNote
        {
            NoteNo = repo.NextNoteNo(new DateTime(2026, 11, 20)),
            CustomerId = customer.Id,
            CustomerName = "自检测试客户",
            CustomerPhone = "13700000000",
            CustomerAddress = "测试路 1 号",
            NoteDate = new DateTime(2026, 11, 20),
            Salesman = "李送货",
            Receiver = "王收货",
            Maker = "管理员",
            Remark = "自检单据，请勿使用",
            Items =
            {
                new NoteItem { ProductName = "纯棉毛巾", Spec = "34×76cm", Unit = "条", Qty = 12, Price = 8.50m },
                new NoteItem { ProductName = "洗衣液", Spec = "2L", Unit = "瓶", Qty = 3, Price = 19.90m },
                new NoteItem { ProductName = "抽纸", Spec = "3层×6包", Unit = "提", Qty = 2, Price = 15.00m }
            }
        };
        note.TotalAmount = note.Items.Sum(i => i.Amount);
        var id = repo.SaveNote(note);
        var saved = repo.GetNote(id);
        Check("note saved", saved?.NoteNo ?? "(null)", note.NoteNo);
        Check("note items", (saved?.Items.Count ?? 0).ToString(), "3");
        Check("note total", (saved?.TotalAmount ?? 0m).ToString("0.00"), "191.70");
        Check("note amount words", saved?.TotalAmountInWords ?? "", "壹佰玖拾壹元柒角");
        Check("note status", saved?.Status.ToString() ?? "", "未送货");

        // 状态变更
        repo.SetNoteStatus(new[] { id }, NoteStatus.已送货);
        Check("status changed", repo.GetNote(id)?.Status.ToString() ?? "", "已送货");

        // ---- 打印渲染 ----
        try
        {
            var printNote = repo.GetNote(id)!;
            using var doc = new NotePrintDocument(printNote, repo);
            var ps = doc.DefaultPageSettings;
            using var bmp = new Bitmap(ps.PaperSize.Width, ps.PaperSize.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                var margin = ps.Margins;
                var area = new RectangleF(margin.Left, margin.Top,
                    ps.PaperSize.Width - margin.Left - margin.Right,
                    ps.PaperSize.Height - margin.Top - margin.Bottom);
                doc.DrawNote(g, area, printNote);
            }
            var path = Path.Combine(outDir, "print-preview.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            log.AppendLine($"PASS  print rendered: {path} ({bmp.Width}x{bmp.Height})");
        }
        catch (Exception ex)
        {
            failures++;
            log.AppendLine("FAIL  print render: " + ex.Message);
        }

        // 清理自检数据
        repo.DeleteNotes(new[] { id });
        repo.DeleteCustomers(new[] { customer.Id });

        failures += RunAuthChecks(repo, log);

        log.AppendLine();
        log.AppendLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        var reportPath = Path.Combine(outDir, "selftest.txt");
        File.WriteAllText(reportPath, log.ToString(), System.Text.Encoding.UTF8);
        Console.WriteLine(log.ToString());
        return failures;
    }

    /// <summary>账号、密码散列与角色权限的校验。</summary>
    private static int RunAuthChecks(Repository repo, System.Text.StringBuilder log)
    {
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
        }

        // 散列：同一密码两次生成应不同（盐随机），但都能验证通过
        var (h1, s1) = PasswordHasher.Create("abc123");
        var (h2, s2) = PasswordHasher.Create("abc123");
        Check("密码散列加盐随机化", h1 != h2 && s1 != s2);
        Check("正确密码可通过校验", PasswordHasher.Verify("abc123", h1, s1));
        Check("错误密码无法通过校验", !PasswordHasher.Verify("abc124", h1, s1));
        Check("空密码无法通过校验", !PasswordHasher.Verify("", h1, s1));
        Check("数据库不存明文密码", !h1.Contains("abc123") && !s1.Contains("abc123"));

        // 强度校验
        Check("拒绝 5 位密码", PasswordHasher.Validate("abc12") is not null);
        Check("拒绝单一重复字符", PasswordHasher.Validate("aaaaaa") is not null);
        Check("接受 6 位以上密码", PasswordHasher.Validate("abc129") is null);

        // 建临时用户做登录与权限校验
        var auth = new AuthService(repo);
        auth.EnsureDefaultAdmin();

        var tempName = "t_" + Guid.NewGuid().ToString("N")[..8];
        var created = auth.CreateUser(tempName, "测试员工", UserRole.员工, "pass1234");

        // CreateUser 需要管理员身份；此时 auth 尚未登录，应当被拒绝
        Check("未登录不能创建用户", !created.Ok, created.Message);

        var adminAuth = new AuthService(repo);
        adminAuth.EnsureDefaultAdmin();
        var adminLogin = adminAuth.Login("admin", AuthService.DefaultAdminPassword);
        Check("默认管理员可登录", adminLogin.Ok, adminLogin.Message);
        Check("登录后角色为管理员", adminAuth.IsAdmin);

        var created2 = adminAuth.CreateUser(tempName, "测试员工", UserRole.员工, "pass1234");
        Check("管理员可创建员工", created2.Ok, created2.Message);
        Check("用户名重复被拒绝",
            !adminAuth.CreateUser(tempName, "重复", UserRole.员工, "pass1234").Ok);

        var staffAuth = new AuthService(repo);
        var staffLogin = staffAuth.Login(tempName, "pass1234");
        Check("新建员工可登录", staffLogin.Ok, staffLogin.Message);
        Check("员工不是管理员", !staffAuth.IsAdmin);

        var wrong = new AuthService(repo);
        Check("错误密码登录失败", !wrong.Login(tempName, "wrongpass").Ok);
        Check("不存在的用户登录失败", !wrong.Login("no_such_user_xyz", "pass1234").Ok);

        // 员工不能管理用户
        var staff = staffLogin.User!;
        Check("员工不能删除用户", !staffAuth.DeleteUser(staff).Ok);
        Check("员工不能重置他人密码", !staffAuth.ResetPassword(staff, "newpass1").Ok);

        // 连续失败锁定
        var lockAuth = new AuthService(repo);
        for (var i = 0; i < 5; i++) lockAuth.Login(tempName, "badpass");
        var locked = lockAuth.Login(tempName, "pass1234");
        Check("连续 5 次失败后锁定", !locked.Ok && locked.Message.Contains("秒后再试"), locked.Message);

        // 修改密码：原密码错误应失败
        var pw = new AuthService(repo);
        pw.Login(tempName, "pass1234");
        Check("原密码错误时改密失败", !pw.ChangeOwnPassword("badold", "newpass9").Ok);
        Check("原密码正确时改密成功", pw.ChangeOwnPassword("pass1234", "newpass9").Ok);

        var after = new AuthService(repo);
        Check("新密码可登录", after.Login(tempName, "newpass9").Ok);
        var oldPw = new AuthService(repo);
        Check("旧密码已失效", !oldPw.Login(tempName, "pass1234").Ok);

        // 不允许删掉自己 / 最后一个管理员
        Check("不能删除当前登录账号", !adminAuth.DeleteUser(adminAuth.Current!).Ok);
        Check("至少要保留一个管理员", repo.CountAdmins() >= 1);

        // 停用账号无法登录
        var u = repo.FindUser(tempName)!;
        adminAuth.UpdateUser(u, "测试员工", UserRole.员工, false);
        var disabled = new AuthService(repo);
        var dis = disabled.Login(tempName, "newpass9");
        Check("停用账号无法登录", !dis.Ok && dis.Message.Contains("停用"), dis.Message);

        repo.DeleteUser(u.Id);
        Check("清理测试用户", repo.FindUser(tempName) is null);
        return failures;
    }
}
