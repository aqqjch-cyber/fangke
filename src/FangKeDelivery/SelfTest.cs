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

        log.AppendLine();
        log.AppendLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        var reportPath = Path.Combine(outDir, "selftest.txt");
        File.WriteAllText(reportPath, log.ToString(), System.Text.Encoding.UTF8);
        Console.WriteLine(log.ToString());
        return failures;
    }
}
