using FangKeDelivery.Data;
using System.Threading;
using FangKeDelivery.UI;

namespace FangKeDelivery;

internal static class Program
{
    private static void Trace(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "startup.log"),
                $"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
        catch { }
    }

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            var db = new Db(Core.AppPaths.DatabasePath);
            db.Initialize();
            var args = Environment.GetCommandLineArgs();
            // 必须在任何可能弹窗的逻辑之前设置，否则无人值守运行时会被模态框卡死
            if (args.Any(a => a is "--dump-layout" or "--selftest" or "--uitest" or "--shot"))
                Msg.AutoAnswer = true;

            var repo = new Repository(db);
            SeedIfEmpty(repo);

            var auth = new Core.AuthService(repo);
            auth.EnsureDefaultAdmin();

            var shotIndex = Array.FindIndex(args, a => a == "--shot");
            if (shotIndex >= 0)
            {
                RunShots(repo, args, shotIndex);
                return;
            }

            var dumpIndex = Array.FindIndex(args, a => a == "--dump-layout");
            if (dumpIndex >= 0)
            {
                RunLayoutSelfTest(repo, args, dumpIndex);
                return;
            }

            var testIndex = Array.FindIndex(args, a => a == "--selftest");
            if (testIndex >= 0)
            {
                var dir = testIndex + 1 < args.Length ? args[testIndex + 1] : Path.Combine(AppContext.BaseDirectory, "selftest");
                Environment.ExitCode = SelfTest.Run(repo, dir);
                return;
            }

            var uiIndex = Array.FindIndex(args, a => a == "--uitest");
            if (uiIndex >= 0)
            {
                var dir = uiIndex + 1 < args.Length ? args[uiIndex + 1] : Path.Combine(AppContext.BaseDirectory, "uitest");
                Environment.ExitCode = UiFlowTest.Run(repo, dir);
                return;
            }

            var demoIndex = Array.FindIndex(args, a => a == "--demo");
            if (demoIndex >= 0)
            {
                RunDemoSeed(repo);
                return;
            }

            // 登录通过后才进入主界面；取消登录直接退出
            using (var login = new LoginForm(auth))
            {
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            Application.Run(new MainForm(repo, auth));
        }
        catch (Exception ex)
        {
            Msg.Show(
                "软件启动失败：\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                "万方商贸管家", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 截图模式：登录为管理员后逐页显示并抓屏。
    /// 用 CopyFromScreen 而不是 DrawToBitmap —— Wine 下后者画不出内容。
    /// </summary>
    private static void RunShots(Repository repo, string[] args, int shotIndex)
    {
        var outDir = shotIndex + 1 < args.Length ? args[shotIndex + 1] : Path.Combine(AppContext.BaseDirectory, "shots");
        Directory.CreateDirectory(outDir);

        var pages = new[]
        {
            "note", "list", "stockin", "stock", "customer", "product", "audit", "report", "settings", "users"
        };

        var form = new MainForm(repo, TestLogin.AsAdmin(repo));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(0, 0);
        form.Show();
        for (var i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(120); }

        foreach (var page in pages)
        {
            form.ShowPage(page);
            for (var i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(120); }

            try
            {
                var bounds = form.Bounds;
                using var bmp = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                bmp.Save(Path.Combine(outDir, $"page-{page}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(outDir, "errors.txt"), $"{page}: {ex.Message}\n");
            }
        }

        form.Close();
    }

    private static void RunLayoutSelfTest(Repository repo, string[] args, int dumpIndex)
    {
        var outDir = dumpIndex + 1 < args.Length ? args[dumpIndex + 1] : Path.Combine(AppContext.BaseDirectory, "layout");
        Directory.CreateDirectory(outDir);

        var report = new System.Text.StringBuilder();

        // 登录窗口：独立窗口，之前没纳入检查，漏掉了输入框与标签错位
        using (var login = new UI.LoginForm(new Core.AuthService(repo)))
        {
            login.Show();
            Application.DoEvents();
            UI.LayoutDump.Write(login, Path.Combine(outDir, "layout-login.txt"));
            var lp = UI.LayoutDump.Validate(login);
            report.AppendLine($"=== login === {lp.Count} problem(s)");
            foreach (var p in lp.Distinct().Take(25)) report.AppendLine("  " + p);
            login.Close();
        }

        // 打印预览窗口：检查主图是否完整落在可视区内
        var anyNote = repo.QueryNotes().FirstOrDefault();
        var previewChecked = anyNote is not null;
        if (anyNote is null)
        {
            // 静默跳过会让报告少一行却仍显示全绿，这里必须显式标出来
            report.AppendLine("=== preview === SKIPPED (库中无送货单，先运行 --demo 生成演示数据)");
        }
        else
        {
            using var pv = new Printing.PreviewForm(repo.GetNote(anyNote.Id)!, repo);
            pv.Show();
            Application.DoEvents();
            Application.DoEvents();
            UI.LayoutDump.Write(pv, Path.Combine(outDir, "layout-preview.txt"));
            var pp = UI.LayoutDump.Validate(pv);
            report.AppendLine($"=== preview === {pp.Count} problem(s)");
            foreach (var p in pp.Distinct().Take(25)) report.AppendLine("  " + p);
            pv.Close();
        }

        var form = new MainForm(repo, TestLogin.AsAdmin(repo));
        form.Show();
        Application.DoEvents();
        var pages = new[]
        {
            "note", "list", "stockin", "stock", "customer", "product", "audit", "report", "settings", "users"
        };

        foreach (var page in pages)
        {
            form.ShowPage(page);
            Application.DoEvents();
            Application.DoEvents();

            UI.LayoutDump.Write(form, Path.Combine(outDir, $"layout-{page}.txt"));

            var problems = UI.LayoutDump.Validate(form);
            report.AppendLine($"=== {page} === {problems.Count} problem(s)");
            foreach (var p in problems.Distinct().Take(25))
                report.AppendLine("  " + p);

            try
            {
                var bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(Path.Combine(outDir, $"page-{page}.png"), System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
            }
            catch (Exception ex)
            {
                report.AppendLine("  SCREENSHOT_FAILED " + ex.Message);
            }
        }

        // 汇总行：窗口数量与跳过项一目了然，避免少查了却仍显示全绿
        var skipped = previewChecked ? 0 : 1;
        report.AppendLine();
        report.AppendLine($"=== 汇总 === 已检查 {12 - skipped} 个窗口，跳过 {skipped} 个"
            + (skipped > 0 ? "（preview：库中无单据）" : ""));

        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString(), System.Text.Encoding.UTF8);
        form.Close();
    }

    /// <summary>造一批演示单据，方便试用和验收；重复执行会跳过已存在的单号。</summary>
    private static void RunDemoSeed(Repository repo)
    {
        try
        {
            var products = repo.GetProducts();
            var customers = repo.GetCustomers();
            if (products.Count == 0 || customers.Count == 0)
            {
                Console.WriteLine("演示数据需要商品和客户，请先正常启动一次程序。");
                return;
            }

            var existing = repo.QueryNotes().Select(n => n.NoteNo).ToHashSet();
            var made = 0;

            (string no, int dayOffset, int qtyA, int qtyB, Core.ApproveStatus status)[] plan =
            {
                ("SD-DEMO-0001", -2, 20, 5, Core.ApproveStatus.待审核),
                ("SD-DEMO-0002", -1, 10, 3, Core.ApproveStatus.待审核),
                ("SD-DEMO-0003", -3, 8, 2, Core.ApproveStatus.已审核)
            };

            foreach (var (no, dayOffset, qtyA, qtyB, status) in plan)
            {
                if (existing.Contains(no)) continue;

                var p1 = products[0];
                var p2 = products.Count > 1 ? products[1] : products[0];
                var c = customers[0];

                var note = new Core.DeliveryNote
                {
                    NoteNo = no,
                    CustomerId = c.Id,
                    CustomerName = c.Name,
                    CustomerPhone = c.Phone,
                    CustomerAddress = c.Address,
                    Contact = c.Contact,
                    NoteDate = DateTime.Today.AddDays(dayOffset),
                    DeliveryMethod = "送货上门",
                    Transporter = "王师傅",
                    VehicleNo = "粤B·12345",
                    SettleMethod = "月结",
                    Salesman = "业务员甲",
                    Receiver = c.Contact,
                    Maker = "管理员",
                    ApproveStatus = status
                };
                note.Items.Add(new Core.NoteItem
                {
                    Seq = 1, ProductId = p1.Id, ProductName = p1.Name,
                    Spec = p1.Spec, Unit = p1.Unit, Qty = qtyA, Price = p1.Price
                });
                note.Items.Add(new Core.NoteItem
                {
                    Seq = 2, ProductId = p2.Id, ProductName = p2.Name,
                    Spec = p2.Spec, Unit = p2.Unit, Qty = qtyB, Price = p2.Price
                });
                note.TotalAmount = note.Items.Sum(i => i.Amount);

                var id = repo.SaveNote(note);
                if (status == Core.ApproveStatus.已审核)
                    repo.ApproveNote(id, "管理员");

                made++;
            }

            Console.WriteLine($"演示数据就绪：新增 {made} 张送货单（2 张待审核 / 1 张已审核）。");
        }
        catch (Exception ex)
        {
            Console.WriteLine("生成演示数据失败：" + ex.Message);
        }
    }

    private static void SeedIfEmpty(Repository repo)
    {
        try
        {
            if (string.IsNullOrEmpty(repo.GetSetting("Seeded")))
            {
                repo.SetSetting("CompanyName", "方可商贸有限公司");
                repo.SetSetting("CompanyPhone", "138-0000-0000");
                repo.SetSetting("CompanyAddress", "示例地址：某某市某某区某某路 1 号");
                repo.SetSetting("Maker", "管理员");
                repo.SetSetting("Salesman", "");
                repo.SetSetting("Seeded", "1");
            }

            if (repo.Count("Product") == 0)
            {
                foreach (var p in new[]
                {
                    new Core.Product { Name = "纯棉毛巾", Spec = "34×76cm", Unit = "条", Price = 8.50m },
                    new Core.Product { Name = "洗衣液", Spec = "2L", Unit = "瓶", Price = 19.90m },
                    new Core.Product { Name = "抽纸", Spec = "3层×6包", Unit = "提", Price = 15.00m },
                    new Core.Product { Name = "矿泉水", Spec = "550ml×24", Unit = "箱", Price = 26.00m },
                    new Core.Product { Name = "一次性纸杯", Spec = "100只装", Unit = "包", Price = 6.80m }
                })
                {
                    repo.SaveProduct(p);
                }
            }

            if (repo.Count("Customer") == 0)
            {
                foreach (var c in new[]
                {
                    new Core.Customer { Name = "示例客户：新华超市", Contact = "张经理", Phone = "139-0000-0001", Address = "示例路 88 号" },
                    new Core.Customer { Name = "示例客户：便民百货", Contact = "李老板", Phone = "139-0000-0002", Address = "示例街 12 号" }
                })
                {
                    repo.SaveCustomer(c);
                }
            }
        }
        catch
        {
            // 初始化示例数据失败不应阻止程序启动
        }
    }
}
