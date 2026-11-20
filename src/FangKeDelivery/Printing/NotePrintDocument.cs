using System.Drawing.Printing;
using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.Printing;

/// <summary>三联送货单打印：存根联 / 客户联 / 回单联。</summary>
public sealed class NotePrintDocument : PrintDocument
{
    private readonly DeliveryNote _note;
    private readonly string _company;
    private readonly string _companyPhone;
    private readonly string _companyAddress;
    private readonly string _footer;

    private static readonly string[] Copies = { "第一联 存根联", "第二联 客户联", "第三联 回单联" };

    public NotePrintDocument(DeliveryNote note, Repository repo)
    {
        _note = note;
        _company = repo.GetSetting("CompanyName", "方可商贸有限公司");
        _companyPhone = repo.GetSetting("CompanyPhone", "");
        _companyAddress = repo.GetSetting("CompanyAddress", "");
        _footer = repo.GetSetting("PrintFooter", "客户签收后请妥善保管，作为结算凭证。");

        DefaultPageSettings.PaperSize = new PaperSize("送货单", 950, 550);
        DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
        OriginAtMargins = true;
    }

    public bool PreviewOnly { get; set; }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var area = e.MarginBounds;
        var copyIndex = PrintPageIndex(area, g);
        _ = copyIndex;

        e.HasMorePages = false;
    }

    private int PrintPageIndex(Rectangle area, Graphics g)
    {
        var margin = new RectangleF(area.X, area.Y, area.Width, area.Height);
        DrawNote(g, margin, _note);
        return 0;
    }

    public void DrawNote(Graphics g, RectangleF area, DeliveryNote note)
    {
        using var titleFont = new Font(Theme2.FontFamily, 17f, FontStyle.Bold);
        using var headFont = new Font(Theme2.FontFamily, 9f, FontStyle.Regular);
        using var boldFont = new Font(Theme2.FontFamily, 9f, FontStyle.Bold);
        using var smallFont = new Font(Theme2.FontFamily, 8f, FontStyle.Regular);
        using var black = new SolidBrush(Color.Black);
        using var pen = new Pen(Color.Black, 1f);
        using var thinPen = new Pen(Color.FromArgb(90, 90, 90), 0.8f);

        var y = area.Top;

        // 公司名 + 标题
        var titleRect = new RectangleF(area.Left, y, area.Width, 30);
        using (var center = new StringFormat { Alignment = StringAlignment.Center })
        {
            g.DrawString("送  货  单", titleFont, black, titleRect, center);
        }
        y += 34;

        // 副标题：公司信息
        using (var center = new StringFormat { Alignment = StringAlignment.Center })
        {
            var sub = _company;
            if (!string.IsNullOrWhiteSpace(_companyPhone)) sub += $"    电话：{_companyPhone}";
            if (!string.IsNullOrWhiteSpace(_companyAddress)) sub += $"    地址：{_companyAddress}";
            g.DrawString(sub, smallFont, black, new RectangleF(area.Left, y, area.Width, 16), center);
        }
        y += 22;

        // 客户 / 单号 一行
        var infoY = y;
        g.DrawString($"客户名称：{note.CustomerName}", headFont, black, area.Left + 2, infoY);
        g.DrawString($"联系电话：{note.CustomerPhone}", headFont, black, area.Left + area.Width * 0.46f, infoY);
        using (var farFmt = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.DrawString($"单号：{note.NoteNo}", headFont, black,
                new RectangleF(area.Left, infoY, area.Width - 2, 16), farFmt);
        }
        y += 20;

        g.DrawString($"客户地址：{note.CustomerAddress}", headFont, black, area.Left + 2, y);
        using (var farFmt2 = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.DrawString($"日期：{note.NoteDate:yyyy-MM-dd}", headFont, black,
                new RectangleF(area.Left, y, area.Width - 2, 16), farFmt2);
        }
        y += 22;

        // 表格
        var tableTop = y;
        var footerHeight = 96f;
        var tableHeight = area.Bottom - footerHeight - tableTop;

        // 列宽比例
        var cols = new[] { 0.06f, 0.30f, 0.14f, 0.07f, 0.10f, 0.11f, 0.12f, 0.10f };
        var headers = new[] { "序号", "商品名称", "规格", "单位", "数量", "单价", "金额", "备注" };

        var x = area.Left;
        var headerH = 22f;
        var colRects = new RectangleF[cols.Length];
        for (var i = 0; i < cols.Length; i++)
        {
            colRects[i] = new RectangleF(x, tableTop, area.Width * cols[i], headerH);
            x += area.Width * cols[i];
        }

        using (var headerBg = new SolidBrush(Color.FromArgb(238, 238, 238)))
        {
            foreach (var r in colRects)
                g.FillRectangle(headerBg, r);
        }
        using (var center = new StringFormat
               {
                   Alignment = StringAlignment.Center,
                   LineAlignment = StringAlignment.Center
               })
        {
            for (var i = 0; i < headers.Length; i++)
                g.DrawString(headers[i], boldFont, black, colRects[i], center);
        }

        // 表头边框
        foreach (var r in colRects)
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

        // 行
        var rowH = 20f;
        var maxRows = Math.Max(note.Items.Count, (int)Math.Floor((tableHeight - headerH) / rowH));
        maxRows = Math.Min(maxRows, 12);
        var rowY = tableTop + headerH;

        var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        var centerCell = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        for (var i = 0; i < maxRows; i++)
        {
            var item = i < note.Items.Count ? note.Items[i] : null;
            var cells = new[]
            {
                (i + 1).ToString(),
                item?.ProductName ?? "",
                item?.Spec ?? "",
                item?.Unit ?? "",
                item is null ? "" : item.Qty.ToString("0.###"),
                item is null ? "" : item.Price.ToString("0.00"),
                item is null ? "" : item.Amount.ToString("0.00"),
                item?.Remark ?? ""
            };

            for (var c = 0; c < cells.Length; c++)
            {
                var r = new RectangleF(colRects[c].X, rowY, colRects[c].Width, rowH);
                var fmt = c switch
                {
                    0 or 3 => centerCell,
                    4 or 5 or 6 => right,
                    _ => left
                };
                var padded = new RectangleF(r.X + 2, r.Y, r.Width - 4, r.Height);
                g.DrawString(cells[c], headFont, black, padded, fmt);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
            rowY += rowH;
        }

        // 合计行
        var totalRect = new RectangleF(area.Left, rowY, area.Width, 24f);
        var totalQty = note.Items.Sum(i => i.Qty);
        var labelW = area.Width * (cols[0] + cols[1] + cols[2] + cols[3]);
        using (var bg = new SolidBrush(Color.FromArgb(245, 245, 245)))
            g.FillRectangle(bg, totalRect);
        g.DrawRectangle(pen, totalRect.X, totalRect.Y, totalRect.Width, totalRect.Height);
        g.DrawString("合    计", boldFont, black, new RectangleF(totalRect.X + 4, totalRect.Y, labelW - 8, totalRect.Height),
            centerCell);
        g.DrawString(totalQty.ToString("0.###"), boldFont, black,
            new RectangleF(totalRect.X + labelW, totalRect.Y, area.Width * cols[4] - 4, totalRect.Height), right);
        g.DrawString(note.TotalAmount.ToString("0.00"), boldFont, black,
            new RectangleF(totalRect.X + labelW + area.Width * (cols[4] + cols[5]), totalRect.Y,
                area.Width * cols[6] - 4, totalRect.Height), right);
        g.DrawLine(pen, totalRect.X + labelW, totalRect.Y, totalRect.X + labelW, totalRect.Bottom);
        g.DrawLine(pen, totalRect.X + labelW + area.Width * (cols[4] + cols[5]), totalRect.Y,
            totalRect.X + labelW + area.Width * (cols[4] + cols[5]), totalRect.Bottom);

        var y2 = totalRect.Bottom + 6;

        // 大写金额
        g.DrawString($"合计金额（大写）：{note.TotalAmountInWords}", boldFont, black, area.Left + 2, y2);
        y2 += 20;

        if (!string.IsNullOrWhiteSpace(note.Remark))
        {
            g.DrawString($"备注：{note.Remark}", smallFont, black, area.Left + 2, y2);
            y2 += 18;
        }

        // 签名区
        var signY = area.Bottom - 52;
        g.DrawString($"送货员：{note.Salesman}", headFont, black, area.Left + 2, signY);
        g.DrawString($"收货人：{note.Receiver}", headFont, black, area.Left + area.Width * 0.26f, signY);
        g.DrawString($"制单人：{note.Maker}", headFont, black, area.Left + area.Width * 0.52f, signY);
        g.DrawString($"客户签收：______________", headFont, black, area.Left + area.Width * 0.72f, signY);

        using (var gray = new SolidBrush(Color.FromArgb(110, 110, 110)))
        {
            g.DrawString(_footer, smallFont, gray, area.Left + 2, signY + 20);
        }

        using (var center = new StringFormat { Alignment = StringAlignment.Center })
        {
            g.DrawString($"打印时间：{DateTime.Now:yyyy-MM-dd HH:mm}", smallFont,
                new SolidBrush(Color.FromArgb(130, 130, 130)),
                new RectangleF(area.Left, signY + 34, area.Width, 14), center);
        }
    }

    private static class Theme2
    {
        public static readonly string FontFamily = UI.Theme.FontFamily;
    }
}
