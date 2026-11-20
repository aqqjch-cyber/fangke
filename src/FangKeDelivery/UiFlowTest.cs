using FangKeDelivery.Data;
using FangKeDelivery.UI;
using FangKeDelivery.UI.Pages;

namespace FangKeDelivery;

/// <summary>通过真实控件走一遍开单流程，验证事件联动与保存。</summary>
internal static class UiFlowTest
{
    public static int Run(Repository repo, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var log = new System.Text.StringBuilder();
        var failures = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
        }

        var form = new MainForm(repo, TestLogin.AsAdmin(repo));
        form.Show();
        Application.DoEvents();

        form.ShowPage("note");
        Application.DoEvents();

        var editor = FindDescendant<NoteEditorPage>(form);
        if (editor is null)
        {
            log.AppendLine("FAIL  找不到开单页面");
            File.WriteAllText(Path.Combine(outDir, "uiflow.txt"), log.ToString(), System.Text.Encoding.UTF8);
            return 1;
        }

        // 通过真实控件录入
        var customer = FindDescendant<ComboBox>(editor)!;
        var grid = FindDescendant<DataGridView>(editor)!;
        var labels = FindDescendants<Label>(editor).ToList();

        customer.Text = "示例客户：新华超市";
        customer.Focus();
        Application.DoEvents();

        // 客户选择后应自动带出电话/地址
        var texts = FindDescendants<TextBox>(editor).ToList();
        Check("客户选择后自动填充电话", texts.Any(t => t.Text.Contains("139-0000-0001")),
            string.Join("|", texts.Select(t => t.Text).Where(t => t.Length > 0).Take(4)));
        Check("客户选择后自动填充地址", texts.Any(t => t.Text.Contains("示例路")),
            texts.FirstOrDefault(t => t.Text.Contains("路"))?.Text ?? "(空)");

        Check("明细表格已建好 8 列", grid.Columns.Count == 8, $"{grid.Columns.Count} 列");
        Check("默认给出 5 个空行", grid.Rows.Count == 5, $"{grid.Rows.Count} 行");

        // 第一行填商品：应自动带出规格/单位/单价
        grid.CurrentCell = grid.Rows[0].Cells["ProductName"];
        grid.Rows[0].Cells["ProductName"].Value = "纯棉毛巾";
        Application.DoEvents();
        Check("商品名自动带出规格", grid.Rows[0].Cells["Spec"].Value?.ToString() == "34×76cm",
            grid.Rows[0].Cells["Spec"].Value?.ToString() ?? "(空)");
        Check("商品名自动带出单位", grid.Rows[0].Cells["Unit"].Value?.ToString() == "条",
            grid.Rows[0].Cells["Unit"].Value?.ToString() ?? "(空)");
        Check("商品名自动带出单价", Num(grid.Rows[0].Cells["Price"].Value) == 8.50m,
            grid.Rows[0].Cells["Price"].Value?.ToString() ?? "(空)");

        // 数量 12 -> 金额 102.00
        grid.Rows[0].Cells["Qty"].Value = 12m;
        Application.DoEvents();
        Check("数量×单价 自动算金额", Num(grid.Rows[0].Cells["Amount"].Value) == 102.00m,
            grid.Rows[0].Cells["Amount"].Value?.ToString() ?? "(空)");

        // 第二行
        grid.Rows[1].Cells["ProductName"].Value = "洗衣液";
        grid.Rows[1].Cells["Qty"].Value = 3m;
        Application.DoEvents();
        Check("第二行金额", Num(grid.Rows[1].Cells["Amount"].Value) == 59.70m,
            grid.Rows[1].Cells["Amount"].Value?.ToString() ?? "(空)");

        // 合计应为 161.70
        var totalLabel = labels.FirstOrDefault(l => l.Text == "161.70");
        Check("合计金额 161.70", totalLabel is not null,
            string.Join(",", labels.Select(l => l.Text).Where(t => t.Contains(".")).Take(5)));

        var wordsLabel = labels.FirstOrDefault(l => l.Text.Contains("壹佰陆拾壹元"));
        Check("合计大写正确", wordsLabel is not null,
            labels.FirstOrDefault(l => l.Text.Contains("元"))?.Text ?? "(无)");

        // 保存
        var saveBtn = FindDescendants<Button>(editor).FirstOrDefault(b => b.Text == "保存");
        Check("找到保存按钮", saveBtn is not null);
        saveBtn?.PerformClick();
        Application.DoEvents();

        // 保存后会弹提示框，程序化关闭
        CloseAnyDialog("保存成功");

        Application.DoEvents();
        var notes = repo.QueryNotes("新华超市", DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), null);
        Check("单据已写入数据库", notes.Count >= 1, $"{notes.Count} 张");
        var saved = notes.FirstOrDefault();
        Check("数据库金额正确", saved is not null && saved.TotalAmount == 161.70m,
            saved?.TotalAmount.ToString("0.00") ?? "(无)");
        var savedFull = saved is null ? null : repo.GetNote(saved.Id);
        Check("数据库明细 2 行", savedFull?.Items.Count == 2, $"{savedFull?.Items.Count} 行");
        Check("列表查询带回数量合计", saved?.TotalQty == 15m, saved?.TotalQty.ToString("0.###") ?? "(无)");

        // 列表页应能查到
        form.ShowPage("list");
        Application.DoEvents();
        var listPage = FindDescendant<NoteListPage>(form);
        Check("单据列表页可加载", listPage is not null);
        if (listPage is not null)
        {
            var listGrid = FindDescendant<DataGridView>(listPage);
            Check("列表中有数据", listGrid is not null && listGrid.Rows.Count >= 1,
                $"{listGrid?.Rows.Count} 行");
        }

        // 从列表打开单据编辑：数据必须真的出现在开单页面
        if (saved is not null)
        {
            form.OpenNoteForEdit(saved.Id);
            Application.DoEvents();
            var editor2 = FindDescendant<NoteEditorPage>(form)!;
            var noBox = FindDescendants<TextBox>(editor2).FirstOrDefault(t => t.Text.StartsWith("SD"));
            Check("打开单据：单号正确回填", noBox?.Text == saved.NoteNo, noBox?.Text ?? "(空)");

            var grid2 = FindDescendant<DataGridView>(editor2)!;
            Check("打开单据：明细回填", grid2.Rows.Count >= 2 && 
                grid2.Rows[0].Cells["ProductName"].Value?.ToString() == "纯棉毛巾",
                grid2.Rows[0].Cells["ProductName"].Value?.ToString() ?? "(空)");

            // 复制为新单：应是新单号、今天日期、明细同样存在
            form.CopyNoteAsNew(saved.Id);
            Application.DoEvents();
            var editor3 = FindDescendant<NoteEditorPage>(form)!;
            var noBox2 = FindDescendants<TextBox>(editor3).FirstOrDefault(t => t.Text.StartsWith("SD"));
            Check("复制新单：生成了不同单号", noBox2?.Text is not null && noBox2.Text != saved.NoteNo,
                noBox2?.Text ?? "(空)");
            var grid3 = FindDescendant<DataGridView>(editor3)!;
            Check("复制新单：明细已带过", grid3.Rows[0].Cells["ProductName"].Value?.ToString() == "纯棉毛巾",
                grid3.Rows[0].Cells["ProductName"].Value?.ToString() ?? "(空)");

            // 复制单保存后应变成独立的新单据
            var beforeCount = repo.QueryNotes(null, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), null).Count;
            var saveBtn3 = FindDescendants<Button>(editor3).FirstOrDefault(b => b.Text == "保存");
            saveBtn3?.PerformClick();
            Application.DoEvents();
            var afterCount = repo.QueryNotes(null, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), null).Count;
            Check("复制单保存后新增一条记录", afterCount == beforeCount + 1,
                $"{beforeCount} -> {afterCount}");
        }

        // 报表页
        form.ShowPage("report");
        Application.DoEvents();
        var reportPage = FindDescendant<ReportPage>(form);
        Check("报表页可加载", reportPage is not null);

        // 打印文档可创建
        if (saved is not null)
        {
            try
            {
                using var doc = new Printing.NotePrintDocument(saved, repo);
                Check("打印文档可创建", true, $"{doc.DefaultPageSettings.PaperSize.Width}x{doc.DefaultPageSettings.PaperSize.Height}");
            }
            catch (Exception ex)
            {
                Check("打印文档可创建", false, ex.Message);
            }
        }

        form.Close();

        failures += RunAuthUiChecks(repo, outDir, log);
        failures += RunAuditUiChecks(repo, outDir, log);

        log.AppendLine();
        log.AppendLine(failures == 0 ? "ALL UI FLOW CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        File.WriteAllText(Path.Combine(outDir, "uiflow.txt"), log.ToString(), System.Text.Encoding.UTF8);
        Console.WriteLine(log.ToString());
        return failures;
    }

    /// <summary>登录窗口与角色权限的界面级校验。</summary>
    private static int RunAuthUiChecks(Repository repo, string outDir, System.Text.StringBuilder log)
    {
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) failures++;
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
        }

        // 登录窗口可构造，且控件齐全
        var auth = new Core.AuthService(repo);
        auth.EnsureDefaultAdmin();

        using (var login = new LoginForm(auth))
        {
            login.Show();
            Application.DoEvents();

            // 用户名是下拉选择，密码是文本框：不用每次手打账号
            var userCombo = FindDescendant<ComboBox>(login);
            Check("登录窗口用户名是下拉框", userCombo is not null, "");
            var userNames = userCombo?.Items.Cast<object>()
                .Select(o => o.ToString()).ToList() ?? new List<string?>();
            Check("用户名下拉列出 admin", userNames.Contains("admin"),
                string.Join(",", userNames));
            Check("用户名下拉列出 staff", userNames.Contains("staff"),
                string.Join(",", userNames));

            var passBox = FindDescendants<TextBox>(login)
                .FirstOrDefault(t => t.UseSystemPasswordChar);
            Check("登录窗口有密码框", passBox is not null, "");
            Check("登录窗口有登录按钮",
                FindDescendants<Button>(login).Any(b => b.Text.Contains("登")), "");

            // 错误密码应留在窗口内并提示，而不是放行
            userCombo!.Text = "admin";
            passBox!.Text = "definitely_wrong";
            var loginBtn = FindDescendants<Button>(login).First(b => b.Text.Contains("登"));
            loginBtn.PerformClick();
            Application.DoEvents();
            Check("错误密码不放行", login.DialogResult != DialogResult.OK,
                login.DialogResult.ToString());

            // 正确密码应放行
            passBox.Text = Core.AuthService.DefaultAdminPassword;
            loginBtn.PerformClick();
            Application.DoEvents();
            Check("正确密码通过登录", login.DialogResult == DialogResult.OK,
                login.DialogResult.ToString());
            Check("登录成功记住用户名", auth.LastLoginUser == "admin", auth.LastLoginUser);
        }

        // 管理员能看到全部导航项（含设置与用户管理）
        var adminAuth = TestLogin.AsAdmin(repo);
        using (var adminForm = new MainForm(repo, adminAuth))
        {
            adminForm.Show();
            Application.DoEvents();

            var navTexts = FindDescendants<Button>(adminForm)
                .Select(b => b.Text).ToList();
            Check("管理员导航含系统设置", navTexts.Any(t => t.Contains("系统设置")));
            Check("管理员导航含用户管理", navTexts.Any(t => t.Contains("用户管理")));
            Check("主界面显示当前登录人",
                FindDescendants<Label>(adminForm).Any(l => l.Text.Contains("管理员")),
                "");

            adminForm.Close();
        }

        // 员工看不到设置/用户管理
        var staffAuth = TestLogin.AsStaff(repo);
        using (var staffForm = new MainForm(repo, staffAuth))
        {
            staffForm.Show();
            Application.DoEvents();

            var navTexts = FindDescendants<Button>(staffForm)
                .Select(b => b.Text).ToList();
            Check("员工导航无系统设置", !navTexts.Any(t => t.Contains("系统设置")));
            Check("员工导航无用户管理", !navTexts.Any(t => t.Contains("用户管理")));
            Check("员工仍有开单入口", navTexts.Any(t => t.Contains("开单")));
            Check("员工仍有单据列表", navTexts.Any(t => t.Contains("单据列表")));

            staffForm.Close();
        }

        // 用户管理页可加载并列出账号
        var userPage = new UI.Pages.UserPage(repo, adminAuth);
        var host = new Form { Size = new Size(1000, 640) };
        host.Controls.Add(userPage);
        host.Show();
        Application.DoEvents();
        var grid = FindDescendants<DataGridView>(userPage).FirstOrDefault();
        Check("用户管理页有表格", grid is not null);
        Check("用户列表含 admin 与 staff",
            grid is not null && grid.Rows.Count >= 2, $"{grid?.Rows.Count} 行");
        host.Close();

        // 打印预览窗口：能打开、三联齐全、翻页/缩放可用
        var notes = repo.QueryNotes();
        if (notes.Count > 0)
        {
            var note = repo.GetNote(notes[0].Id)!;
            using var pv = new Printing.PreviewForm(note, repo);
            pv.Show();
            Application.DoEvents();

            var pics = FindDescendants<PictureBox>(pv).ToList();
            Check("预览窗口已打开", pv.Visible, pv.Text);
            Check("预览有三联缩略图", pics.Count >= 3, $"{pics.Count} 个缩略图");
            Check("预览主图已加载", pics.Any(p => p.Image is not null));

            var buttons = FindDescendants<Button>(pv).Select(b => b.Text).ToList();
            Check("预览有翻页按钮", buttons.Any(t => t.Contains("下一页")) && buttons.Any(t => t.Contains("上一页")));
            Check("预览有缩放按钮", buttons.Any(t => t.Contains("放大")) && buttons.Any(t => t.Contains("缩小")));
            Check("预览能直接打印", buttons.Any(t => t.Contains("打印")));
            Check("预览按钮全中文", buttons.All(t => !t.Contains("Print") && !t.Contains("Zoom")),
                string.Join(",", buttons));

            // 核对主图实际占位（DrawToBitmap 在 Wine 下渲染不全，改用坐标断言）
            var mainPic = pics.OrderByDescending(p => p.Width * p.Height).First();
            Check("预览主图占位非零", mainPic.Width > 100 && mainPic.Height > 100,
                $"{mainPic.Width}x{mainPic.Height}");
            Check("预览主图在可视区内", mainPic.Width <= pv.ClientSize.Width,
                $"{mainPic.Width} <= {pv.ClientSize.Width}");

            // 让窗口在 X 上真实绘制，供外部截图
            pv.TopMost = true;
            for (var i = 0; i < 40; i++) { Application.DoEvents(); Thread.Sleep(50); }

            var layout = new System.Text.StringBuilder();
            foreach (var c in FindDescendants<Control>(pv))
                layout.AppendLine($"{c.GetType().Name}\t{c.Text}\t{c.Bounds}");
            File.WriteAllText(Path.Combine(outDir, "preview-layout.txt"), layout.ToString());

            pv.Close();
        }

        return failures;
    }


    /// <summary>审核中心界面级校验：待审单据必须真的列出来，审核后库存与列表同步变化。</summary>
    private static int RunAuditUiChecks(Repository repo, string outDir, System.Text.StringBuilder log)
    {
        var failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            log.AppendLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  → " + detail : "")}");
            if (!ok) failures++;
        }

        var host = new MainForm(repo, TestLogin.AsAdmin(repo));
        host.Show();
        Application.DoEvents();

        // 审核中心：待审单据必须真的出现在表格里，而不是空表
        {
            var pending = repo.QueryNotesByApproval(Core.ApproveStatus.待审核);
            Check("存在待审核送货单供审核页显示", pending.Count > 0, $"{pending.Count} 张");

            var auditHost = new Form { Size = new Size(1200, 760) };
            var auditPage = new UI.Pages.AuditPage(repo, host);
            auditHost.Controls.Add(auditPage);
            auditHost.Show();
            Application.DoEvents();
            Application.DoEvents();

            var grids = FindDescendants<DataGridView>(auditPage).ToList();
            Check("审核页有三个表格", grids.Count >= 3, $"{grids.Count} 个");

            // 状态筛选：切到"全部"必须能看到已审核的历史单据，否则升级用户会以为审核页是空的
            var filters = FindDescendants<ComboBox>(auditPage).ToList();
            Check("审核页有状态筛选下拉", filters.Count >= 2, $"{filters.Count} 个");
            if (filters.Count > 0)
            {
                var f = filters[0];
                Check("审核筛选默认待审核", f.SelectedIndex == 0, f.Text);

                f.SelectedIndex = 3;
                Application.DoEvents();
                var allGrid = FindDescendants<DataGridView>(auditPage).First();
                var totalInDb = repo.QueryNotes().Count;
                Check("筛选切到全部能看到所有单据",
                    allGrid.Rows.Count == totalInDb,
                    $"{allGrid.Rows.Count} 行 / 库中 {totalInDb} 张");

                f.SelectedIndex = 0;
                Application.DoEvents();
            }

            var noteGrid = grids.FirstOrDefault();
            Check("审核页送货单表格有数据行",
                noteGrid is not null && noteGrid.Rows.Count == pending.Count,
                $"{noteGrid?.Rows.Count} 行 / 期望 {pending.Count} 行");

            if (noteGrid is not null && noteGrid.Rows.Count > 0)
            {
                var firstNo = noteGrid.Rows[0].Cells["NoteNo"].Value?.ToString() ?? "";
                Check("审核页显示的单号与库中一致",
                    pending.Any(n => n.NoteNo == firstNo), firstNo);
            }

            // 审核通过后，待审列表应减少、库存应变化
            if (pending.Count > 0)
            {
                var before = pending.Count;
                var stockBefore = repo.GetStock().Sum(s => s.Qty);
                var r = repo.ApproveNote(pending[0].Id, "管理员");
                Check("审核通过成功", r.Ok, r.Message);
                Check("审核后待审数量减少",
                    repo.QueryNotesByApproval(Core.ApproveStatus.待审核).Count == before - 1,
                    $"{repo.QueryNotesByApproval(Core.ApproveStatus.待审核).Count} / 期望 {before - 1}");
                Check("审核后库存已扣减", repo.GetStock().Sum(s => s.Qty) != stockBefore,
                    $"{stockBefore} → {repo.GetStock().Sum(s => s.Qty)}");

                // 切页回来必须看到最新数据，而不是缓存的旧表格
                auditPage.Reload();
                Application.DoEvents();
                Check("刷新后审核表格同步更新",
                    noteGrid!.Rows.Count == before - 1,
                    $"{noteGrid.Rows.Count} 行 / 期望 {before - 1} 行");

                var back = repo.UnapproveNote(pending[0].Id, "管理员");
                Check("反审核可回滚", back.Ok, back.Message);
            }

            auditHost.Close();
        }


        host.Close();
        return failures;
    }

    private static decimal Num(object? v) =>
        v is null ? -1m : decimal.TryParse(v.ToString(), out var d) ? d : -1m;

    private static void CloseAnyDialog(string expectedTitle)
    {
        for (var i = 0; i < 20; i++)
        {
            Application.DoEvents();
            var dlg = Application.OpenForms.Cast<Form>()
                .FirstOrDefault(f => f != Application.OpenForms[0] && f.Modal && f.Visible);
            if (dlg is null) return;
            foreach (var b in FindDescendants<Button>(dlg))
            {
                if (b.Text.Contains("确定") || b.Text.Contains("OK") || b.DialogResult == DialogResult.OK)
                {
                    b.PerformClick();
                    return;
                }
            }
            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        }
    }

    private static T? FindDescendant<T>(Control root) where T : Control =>
        FindDescendants<T>(root).FirstOrDefault();

    private static IEnumerable<T> FindDescendants<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T t) yield return t;
            foreach (var d in FindDescendants<T>(c)) yield return d;
        }
    }
}
