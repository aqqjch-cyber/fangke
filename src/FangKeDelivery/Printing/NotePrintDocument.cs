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
    private readonly string _companyFax;
    private readonly string _footer;

    /// <summary>参考单的联次抬头：白联存根 / 红联客户联 / 黄联结算联。</summary>
    private static readonly string[] Copies = { "白联：存根", "红联：客户联", "黄联：结算联" };

    /// <summary>参考单底部三联说明。</summary>
    private const string CopyLegend = "白联：存根      红联：客户联      黄联：结算联";

    public NotePrintDocument(DeliveryNote note, Repository repo)
    {
        _note = note;
        _company = repo.GetSetting("CompanyName", "方可商贸有限公司");
        _companyPhone = repo.GetSetting("CompanyPhone", "");
        _companyAddress = repo.GetSetting("CompanyAddress", "");
        _companyFax = repo.GetSetting("CompanyFax", "");
        _footer = repo.GetSetting("PrintFooter", "客户签收后请妥善保管，作为结算凭证。");
        AlignToPrePrinted = repo.GetSetting("PrintAlignPrePrinted", "1") != "0";

        DefaultPageSettings.PaperSize = new PaperSize("送货单", 950, 550);
        DefaultPageSettings.Margins = new Margins(30, 30, 30, 30);
        OriginAtMargins = true;
    }

    /// <summary>是否预览。预览时也按当前联次模式渲染，所见即所印。</summary>
    public bool PreviewOnly { get; set; }

    /// <summary>
    /// 是否用复写纸。复写纸靠压感一次复写出三联，软件只能输出一页；
    /// 输出三页会让一份单子走三遍纸，即用户反馈的「打一次出三张」。
    /// </summary>
    public bool CarbonCopy { get; set; } = true;

    /// <summary>本次要走几遍纸：复写纸 1 遍，普通纸分三联 3 遍。</summary>
    public int PageCopyCount => CarbonCopy ? 1 : Copies.Length;

    /// <summary>
    /// 按预印单（241-1）实际位置排版：内容只占纸面左侧约 79.5%，
    /// 右侧留白对应针式打印机打不到的区域。用预印纸时必须开启，
    /// 否则数据会错位到预印框外。用空白纸时可关闭，让表格铺满可打印区。
    /// </summary>
    public bool AlignToPrePrinted { get; set; } = true;

    /// <summary>纸张宽度（1/100 英寸），与 DefaultPageSettings.PaperSize 一致。</summary>
    public int PaperWidth => DefaultPageSettings.PaperSize.Width;

    /// <summary>绘图原点在纸张上的横向位置。OriginAtMargins 打印时为左边距，直接渲染时为 0。</summary>
    public float PaperOriginX { get; set; }

    /// <summary>当前是第几联（0=存根联、1=客户联、2=回单联）。</summary>
    private int _copyIndex;

    /// <summary>商品过多时续页，记录当前画到第几行。</summary>
    private int _itemCursor;

    public int CopyCount => Copies.Length;

    /// <summary>打印时间戳。置空则取当前时间；测试时冻结可确保三联差异仅来自联次标注。</summary>
    public DateTime? PrintTimeOverride { get; set; }

    private DateTime PrintTime => PrintTimeOverride ?? DateTime.Now;

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        base.OnBeginPrint(e);
        _copyIndex = 0;
        _itemCursor = 0;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // OriginAtMargins=true 时绘图原点已平移到左边距，记下以便换算纸张坐标
        PaperOriginX = DefaultPageSettings.Margins.Left;

        var area = new RectangleF(e.MarginBounds.X, e.MarginBounds.Y,
            e.MarginBounds.Width, e.MarginBounds.Height);

        // 画当前联，返回是否还有本联的续页
        var moreSameCopy = DrawCopy(g, area, _note, _copyIndex, _itemCursor, out var nextCursor);
        _itemCursor = nextCursor;

        if (moreSameCopy)
        {
            e.HasMorePages = true;
            return;
        }

        // 本联画完，进入下一联
        _copyIndex++;
        _itemCursor = 0;
        e.HasMorePages = _copyIndex < PageCopyCount;
    }

    private bool DrawCopy(Graphics g, RectangleF area, DeliveryNote note,
        int copyIndex, int startItem, out int nextCursor)
    {
        return DrawNote(g, area, note, copyIndex, startItem, out nextCursor);
    }

    public void DrawNote(Graphics g, RectangleF area, DeliveryNote note) =>
        DrawNote(g, area, note, 0, 0, out _);

    /// <summary>
    /// 按 241-1 预印单版式绘制一联。版式取自实单测量：
    /// 8 列（6.4/24.8/16.9/6.1/7.3/9.3/13.1/16.0%），明细区 10 行且行间无横线，
    /// 合计行为「大写金额 | 数量 | 空 | 金额」四格，签名区两格。
    /// </summary>
    public bool DrawNote(Graphics g, RectangleF area, DeliveryNote note,
        int copyIndex, int startItem, out int nextCursor)
    {
        nextCursor = startItem;

        using var titleFont = new Font(Theme2.FontFamily, 17f, FontStyle.Bold);
        using var companyFont = new Font(Theme2.FontFamily, 11.5f, FontStyle.Bold);
        using var headFont = new Font(Theme2.FontFamily, 8.5f, FontStyle.Regular);
        using var boldFont = new Font(Theme2.FontFamily, 8.5f, FontStyle.Bold);
        using var smallFont = new Font(Theme2.FontFamily, 7.5f, FontStyle.Regular);
        using var black = new SolidBrush(Color.Black);
        using var pen = new Pen(Color.Black, 1f);

        // 预印单实测（相对整张纸）：左边界 3.8%，内容宽 79.5%，右侧 16.7% 留白。
        // 打印路径开了 OriginAtMargins，绘图原点不在纸张左上角，故用 PaperOriginX 换算。
        const float preLeft = 0.038f;
        const float preWidth = 0.795f;
        var left = AlignToPrePrinted
            ? PaperWidth * preLeft - PaperOriginX
            : area.Left;
        var width = AlignToPrePrinted ? PaperWidth * preWidth : area.Width;
        var right = left + width;

        var near = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var far = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        // ── 抬头 ──────────────────────────────────────────────
        // 复写纸一份单子覆盖三联，标单一联次会与纸上的实际联次矛盾，故不标
        if (!CarbonCopy && copyIndex >= 0 && copyIndex < Copies.Length)
        {
            g.DrawString(Copies[copyIndex], boldFont, black,
                new RectangleF(left, area.Top, width, 14), far);
        }

        g.DrawString(_company, companyFont, black,
            new RectangleF(left, area.Top + 4, width * 0.58f, 22), near);

        g.DrawString("送  货  单", titleFont, black,
            new RectangleF(right - width * 0.42f, area.Top + 15, width * 0.42f, 28), far);

        if (!string.IsNullOrWhiteSpace(_companyAddress))
            g.DrawString($"地址：{_companyAddress}", smallFont, black,
                new RectangleF(left, area.Top + 28, width * 0.62f, 14), near);

        var tel = $"TEL：{_companyPhone}";
        if (!string.IsNullOrWhiteSpace(_companyFax)) tel += $"    FAX：{_companyFax}";
        g.DrawString(tel, smallFont, black,
            new RectangleF(left, area.Top + 44, width * 0.62f, 14), near);

        g.DrawString($"NO：{note.NoteNo}", boldFont, black,
            new RectangleF(right - width * 0.42f, area.Top + 44, width * 0.42f, 14), far);

        var cust1Y = area.Top + 66;
        g.DrawString($"客户名称：{note.CustomerName}", headFont, black,
            new RectangleF(left, cust1Y, width * 0.42f, 16), near);
        g.DrawString($"联系人：{note.Contact}", headFont, black,
            new RectangleF(left + width * 0.44f, cust1Y, width * 0.22f, 16), near);
        g.DrawString($"发货日期：{note.NoteDate:yyyy-MM-dd}", headFont, black,
            new RectangleF(right - width * 0.34f, cust1Y, width * 0.34f, 16), far);

        var cust2Y = area.Top + 86;
        g.DrawString($"联系地址：{note.CustomerAddress}", headFont, black,
            new RectangleF(left, cust2Y, width * 0.42f, 16), near);
        g.DrawString($"联系电话：{note.CustomerPhone}", headFont, black,
            new RectangleF(left + width * 0.44f, cust2Y, width * 0.22f, 16), near);
        g.DrawString($"送货方式：{note.DeliveryMethod}", headFont, black,
            new RectangleF(right - width * 0.34f, cust2Y, width * 0.34f, 16), far);

        // ── 表格 ──────────────────────────────────────────────
        var cols = new[] { 0.064f, 0.248f, 0.169f, 0.061f, 0.073f, 0.093f, 0.131f, 0.160f };
        var headers = new[] { "序号", "商品名称", "规格型号", "单位", "数量", "单价", "金额", "备注说明" };

        var tableTop = area.Top + 108;
        const float headerH = 24f;
        const int rowCount = 10;
        const float rowH = 24.5f;
        var bodyTop = tableTop + headerH;
        var bodyBottom = bodyTop + rowH * rowCount;

        var xs = new float[cols.Length + 1];
        xs[0] = left;
        for (var i = 0; i < cols.Length; i++) xs[i + 1] = xs[i] + width * cols[i];
        xs[cols.Length] = right;

        // 表头
        using (var headerBg = new SolidBrush(Color.FromArgb(240, 240, 240)))
            g.FillRectangle(headerBg, left, tableTop, width, headerH);
        for (var i = 0; i < headers.Length; i++)
        {
            var r = new RectangleF(xs[i] + 2, tableTop, xs[i + 1] - xs[i] - 4, headerH);
            g.DrawString(headers[i], boldFont, black, r, center);
        }

        // 明细行（行间不画横线，只有竖线贯穿，与预印单一致）
        var capacity = rowCount;
        var remaining = Math.Max(0, note.Items.Count - startItem);
        var onThisPage = Math.Min(capacity, remaining);

        for (var i = 0; i < capacity; i++)
        {
            var idx = startItem + i;
            var item = idx < note.Items.Count ? note.Items[idx] : null;
            var rowY = bodyTop + rowH * i;
            var cells = new[]
            {
                item is null ? "" : (idx + 1).ToString(),
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
                var fmt = c switch { 0 or 3 => center, 4 or 5 or 6 => far, _ => near };
                var r = new RectangleF(xs[c] + 2, rowY, xs[c + 1] - xs[c] - 4, rowH);
                g.DrawString(cells[c], headFont, black, r, fmt);
            }
        }

        nextCursor = startItem + onThisPage;

        // 竖线贯穿表头 + 明细
        for (var i = 0; i <= cols.Length; i++)
            g.DrawLine(pen, xs[i], tableTop, xs[i], bodyBottom);

        // 横线：表头顶、表头底、明细底
        g.DrawLine(pen, left, tableTop, right, tableTop);
        g.DrawLine(pen, left, bodyTop, right, bodyTop);
        g.DrawLine(pen, left, bodyBottom, right, bodyBottom);

        var isLastPage = nextCursor >= note.Items.Count;
        if (!isLastPage)
        {
            g.DrawString("（明细未完，续下页）", smallFont, black,
                new RectangleF(left, bodyBottom + 2, width, 14), far);
            return false;
        }

        // ── 合计行：大写金额 | 数量 | 空 | 金额 ──────────────
        var totalTop = bodyBottom;
        const float totalH = 24f;
        var totalBottom = totalTop + totalH;
        var tcol = new[] { 0.543f, 0.0725f, 0.0934f, 0.2911f };
        var tx = new float[tcol.Length + 1];
        tx[0] = left;
        for (var i = 0; i < tcol.Length; i++) tx[i + 1] = tx[i] + width * tcol[i];
        tx[tcol.Length] = right;

        var totalQty = note.Items.Sum(i => i.Qty);
        g.DrawString($"合计金额（大写）：{note.TotalAmountInWords}", boldFont, black,
            new RectangleF(tx[0] + 2, totalTop, tx[1] - tx[0] - 4, totalH), near);
        g.DrawString(totalQty.ToString("0.###"), boldFont, black,
            new RectangleF(tx[1] + 2, totalTop, tx[2] - tx[1] - 4, totalH), center);
        g.DrawString($"¥{note.TotalAmount:0.00}", boldFont, black,
            new RectangleF(tx[3] + 2, totalTop, tx[4] - tx[3] - 4, totalH), far);

        for (var i = 0; i <= tcol.Length; i++)
            g.DrawLine(pen, tx[i], totalTop, tx[i], totalBottom);
        g.DrawLine(pen, left, totalBottom, right, totalBottom);

        // ── 签名区：两行两格 ────────────────────────────────
        var signTop = totalBottom;
        const float signH = 40f;
        var signMid = signTop + signH / 2f;
        var signBottom = signTop + signH;
        var signSplit = left + width * 0.543f;

        g.DrawString($"收货单位（盖章）：", headFont, black,
            new RectangleF(left + 2, signTop, signSplit - left - 4, signH / 2), near);
        g.DrawString($"运货人：{note.Transporter}", headFont, black,
            new RectangleF(signSplit + 2, signTop, (right - signSplit) * 0.5f - 4, signH / 2), near);
        g.DrawString($"运货车号：{note.VehicleNo}", headFont, black,
            new RectangleF(signSplit + (right - signSplit) * 0.5f, signTop,
                (right - signSplit) * 0.5f - 2, signH / 2), near);

        g.DrawString($"收货人签名：{note.Receiver}", headFont, black,
            new RectangleF(left + 2, signMid, signSplit - left - 4, signH / 2), near);
        g.DrawString($"结算方式：{note.SettleMethod}", headFont, black,
            new RectangleF(signSplit + 2, signMid, right - signSplit - 4, signH / 2), near);

        g.DrawLine(pen, left, signTop, left, signBottom);
        g.DrawLine(pen, signSplit, signTop, signSplit, signBottom);
        g.DrawLine(pen, right, signTop, right, signBottom);
        g.DrawLine(pen, left, signBottom, right, signBottom);

        // ── 底部联次说明 + 制单人 ───────────────────────────
        var legendY = signBottom + 3;
        g.DrawString(CopyLegend, smallFont, black,
            new RectangleF(left, legendY, width, 14), center);
        if (!string.IsNullOrWhiteSpace(note.Maker) || !string.IsNullOrWhiteSpace(note.Salesman))
        {
            g.DrawString($"制单人：{note.Maker}    送货员：{note.Salesman}", smallFont, black,
                new RectangleF(left, legendY, width - 2, 14), far);
        }

        return false;
    }

    /// <summary>把三联全部分页渲染成位图。预览与打印共用同一套绘制逻辑，所见即所印。</summary>
    public List<Bitmap> RenderAllPages(float scale = 2f)
    {
        var pages = new List<Bitmap>();
        var ps = DefaultPageSettings;
        var m = ps.Margins;
        PaperOriginX = 0;  // 直接画到位图，原点即纸张左上角
        var area = new RectangleF(m.Left, m.Top,
            ps.PaperSize.Width - m.Left - m.Right,
            ps.PaperSize.Height - m.Top - m.Bottom);

        for (var copy = 0; copy < PageCopyCount; copy++)
        {
            var cursor = 0;
            do
            {
                var bmp = new Bitmap((int)(ps.PaperSize.Width * scale), (int)(ps.PaperSize.Height * scale));
                using (var g = Graphics.FromImage(bmp))
                {
                    g.ScaleTransform(scale, scale);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.White);
                    DrawNote(g, area, _note, copy, cursor, out var next);
                    cursor = next;
                }
                pages.Add(bmp);
            } while (cursor < _note.Items.Count && _note.Items.Count > 0);
        }

        return pages;
    }

    private static class Theme2
    {
        public static readonly string FontFamily = UI.Theme.FontFamily;
    }
}
