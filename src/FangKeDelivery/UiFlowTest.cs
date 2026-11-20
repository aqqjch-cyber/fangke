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

        var form = new MainForm(repo);
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

        log.AppendLine();
        log.AppendLine(failures == 0 ? "ALL UI FLOW CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        File.WriteAllText(Path.Combine(outDir, "uiflow.txt"), log.ToString(), System.Text.Encoding.UTF8);
        Console.WriteLine(log.ToString());
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
