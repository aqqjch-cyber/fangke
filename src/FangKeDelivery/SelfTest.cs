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
            Contact = "王联系人",
            DeliveryMethod = "送货上门",
            Transporter = "张司机",
            VehicleNo = "鲁A12345",
            SettleMethod = "月结",
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
        Check("note contact", saved?.Contact ?? "", "王联系人");
        Check("note delivery method", saved?.DeliveryMethod ?? "", "送货上门");
        Check("note transporter", saved?.Transporter ?? "", "张司机");
        Check("note vehicle no", saved?.VehicleNo ?? "", "鲁A12345");
        Check("note settle method", saved?.SettleMethod ?? "", "月结");

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

        // ---- 三联分页 ----
        failures += RunPrintCopyChecks(repo, id, outDir, log);

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

    /// <summary>验证三联分页：张数、联次标注、续页、空行撑满。</summary>
    private static int RunPrintCopyChecks(Repository repo, long noteId, string outDir, System.Text.StringBuilder log)
    {
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
        }

        var note = repo.GetNote(noteId)!;

        // 少量商品：三联应恰好 3 页
        using (var doc = new NotePrintDocument(note, repo))
        {
            doc.PrintTimeOverride = new DateTime(2026, 1, 1, 9, 0, 0);
            var pages = doc.RenderAllPages(1f);
            try
            {
                Check("三联共 3 页", pages.Count == 3, $"{pages.Count} 页");
                Check("每页尺寸一致",
                    pages.Select(p => $"{p.Width}x{p.Height}").Distinct().Count() == 1,
                    pages.Count > 0 ? $"{pages[0].Width}x{pages[0].Height}" : "无页面");

                for (var i = 0; i < pages.Count; i++)
                {
                    pages[i].Save(Path.Combine(outDir, $"print-copy{i + 1}.png"),
                        System.Drawing.Imaging.ImageFormat.Png);
                }

                var hashes = pages.Select(HashBitmap).Distinct().Count();
                Check("三页内容各不相同（联次标注生效）", hashes == pages.Count, $"{hashes} 种");

                // 三联差异应仅限于右上角的联次标注，正文必须一模一样
                if (pages.Count == 3)
                {
                    var diff = DiffBounds(pages[0], pages[1]);
                    var diff23 = DiffBounds(pages[1], pages[2]);
                    var hasDiff = diff is not null && diff23 is not null;
                    Check("三联之间存在差异", hasDiff);

                    if (hasDiff)
                    {
                        var box = Rectangle.Union(diff!.Value, diff23!.Value);
                        var w = pages[0].Width;
                        Check("差异只在右上角（联次标注处）",
                            box.Right > w / 2 && box.Bottom < 80,
                            $"差异范围 {box}");
                        Check("差异不侵入正文区", box.Top < 80, $"起始 y={box.Top}");
                    }
                }
            }
            finally
            {
                foreach (var p in pages) p.Dispose();
            }
        }

        // 打印版式开关：预印单模式右侧留白，铺满模式占满可打印区
        using (var doc = new NotePrintDocument(repo.GetNote(noteId)!, repo))
        {
            doc.AlignToPrePrinted = true;
            var pages = doc.RenderAllPages(1f);
            try
            {
                var w = pages[0].Width;
                var margin = RightmostInk(pages[0]);
                Check("预印单模式右侧留白", margin < w * 0.85,
                    $"最右墨迹 {margin} / 宽 {w}");
            }
            finally { foreach (var p in pages) p.Dispose(); }
        }

        using (var doc = new NotePrintDocument(repo.GetNote(noteId)!, repo))
        {
            doc.AlignToPrePrinted = false;
            var pages = doc.RenderAllPages(1f);
            try
            {
                var w = pages[0].Width;
                var margin = RightmostInk(pages[0]);
                Check("铺满模式用到右侧区域", margin > w * 0.85,
                    $"最右墨迹 {margin} / 宽 {w}");
            }
            finally { foreach (var p in pages) p.Dispose(); }
        }

        // 商品过多：应跨页续打，总页数是 3 的倍数
        var many = repo.GetNote(noteId)!;
        many.Items.Clear();
        for (var i = 1; i <= 40; i++)
        {
            many.Items.Add(new NoteItem
            {
                ProductName = $"长清单测试商品{i:00}",
                Spec = "标准",
                Unit = "件",
                Qty = 1,
                Price = 10m
            });
        }

        using (var doc = new NotePrintDocument(many, repo))
        {
            var pages = doc.RenderAllPages(1f);
            try
            {
                Check("40 行商品会跨页", pages.Count > 3, $"{pages.Count} 页");
                Check("跨页后每联页数相同", pages.Count % 3 == 0, $"{pages.Count} 页");
            }
            finally
            {
                foreach (var p in pages) p.Dispose();
            }
        }

        return failures;
    }

    /// <summary>返回两张图不相同像素的包围盒；完全相同时返回 null。</summary>
    private static Rectangle? DiffBounds(Bitmap a, Bitmap b)
    {
        var minX = int.MaxValue; var minY = int.MaxValue;
        var maxX = int.MinValue; var maxY = int.MinValue;
        var w = Math.Min(a.Width, b.Width);
        var h = Math.Min(a.Height, b.Height);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (a.GetPixel(x, y) == b.GetPixel(x, y)) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (minX > maxX) return null;
        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>最右侧有墨迹的列号，用于判断版式横向铺开程度。</summary>
    private static int RightmostInk(Bitmap bmp)
    {
        for (var x = bmp.Width - 1; x >= 0; x--)
            for (var y = 0; y < bmp.Height; y++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.R < 200 || c.G < 200 || c.B < 200) return x;
            }
        return 0;
    }

    private static string HashBitmap(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return Convert.ToBase64String(System.Security.Cryptography.MD5.HashData(ms.ToArray()));
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
