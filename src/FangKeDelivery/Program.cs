using FangKeDelivery.Data;
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
            if (args.Any(a => a is "--dump-layout" or "--selftest" or "--uitest"))
                Msg.AutoAnswer = true;

            var repo = new Repository(db);
            SeedIfEmpty(repo);

            var auth = new Core.AuthService(repo);
            auth.EnsureDefaultAdmin();

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
                "方可送货单管理系统", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RunLayoutSelfTest(Repository repo, string[] args, int dumpIndex)
    {
        var outDir = dumpIndex + 1 < args.Length ? args[dumpIndex + 1] : Path.Combine(AppContext.BaseDirectory, "layout");
        Directory.CreateDirectory(outDir);

        var form = new MainForm(repo, TestLogin.AsAdmin(repo));
        form.Show();
        Application.DoEvents();

        var report = new System.Text.StringBuilder();
        var pages = new[] { "note", "list", "customer", "product", "report", "settings", "users" };

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

        File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString(), System.Text.Encoding.UTF8);
        form.Close();
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
