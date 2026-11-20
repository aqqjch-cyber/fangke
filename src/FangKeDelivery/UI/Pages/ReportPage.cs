using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

public sealed class ReportPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly DateTimePicker _from = new();
    private readonly DateTimePicker _to = new();
    private readonly TabControl _tabs = new();
    private readonly DataGridView _byCustomer = new();
    private readonly DataGridView _byProduct = new();
    private readonly DataGridView _byMonth = new();
    private readonly Label _kpiCount = new();
    private readonly Label _kpiAmount = new();
    private readonly Label _kpiAvg = new();
    private readonly Label _kpiQty = new();

    public ReportPage(Repository repo)
    {
        _repo = repo;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildFilterBar(), 0, 0);
        root.Controls.Add(BuildKpiRow(), 0, 1);
        root.Controls.Add(BuildTabs(), 0, 2);

        Controls.Add(root);
    }

    private Control BuildFilterBar()
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(14, 10, 14, 10)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White
        };

        _from.Format = DateTimePickerFormat.Custom;
        _from.CustomFormat = "yyyy-MM-dd";
        _from.Width = 118;
        _from.Font = Theme.BodyFont;
        _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _from.Margin = new Padding(0, 3, 10, 0);

        _to.Format = DateTimePickerFormat.Custom;
        _to.CustomFormat = "yyyy-MM-dd";
        _to.Width = 118;
        _to.Font = Theme.BodyFont;
        _to.Value = DateTime.Today;
        _to.Margin = new Padding(0, 3, 12, 0);

        var stat = Theme.PrimaryButton("统计", 76, 30);
        stat.Margin = new Padding(0, 3, 8, 0);
        stat.Click += (_, _) => Reload();

        var thisMonth = Theme.SecondaryButton("本月", 64, 30);
        thisMonth.Margin = new Padding(0, 3, 8, 0);
        thisMonth.Click += (_, _) =>
        {
            _from.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _to.Value = DateTime.Today;
            Reload();
        };
        var lastMonth = Theme.SecondaryButton("上月", 64, 30);
        lastMonth.Margin = new Padding(0, 3, 8, 0);
        lastMonth.Click += (_, _) =>
        {
            var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _from.Value = first.AddMonths(-1);
            _to.Value = first.AddDays(-1);
            Reload();
        };
        var thisYear = Theme.SecondaryButton("本年", 64, 30);
        thisYear.Margin = new Padding(0, 3, 0, 0);
        thisYear.Click += (_, _) =>
        {
            _from.Value = new DateTime(DateTime.Today.Year, 1, 1);
            _to.Value = DateTime.Today;
            Reload();
        };

        flow.Controls.Add(new Label
        {
            Text = "统计区间", AutoSize = false, Width = 66, Height = 30,
            TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextSub,
            Font = Theme.BodyFont, Margin = new Padding(0, 3, 4, 0)
        });
        flow.Controls.Add(_from);
        flow.Controls.Add(new Label
        {
            Text = "至", AutoSize = false, Width = 20, Height = 30,
            TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextSub,
            Font = Theme.BodyFont, Margin = new Padding(0, 3, 4, 0)
        });
        flow.Controls.Add(_to);
        flow.Controls.Add(stat);
        flow.Controls.Add(thisMonth);
        flow.Controls.Add(lastMonth);
        flow.Controls.Add(thisYear);

        card.Controls.Add(flow);
        return card;
    }

    private Control BuildKpiRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Theme.PageBack,
            Margin = new Padding(0, 0, 0, 10)
        };
        for (var i = 0; i < 4; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        row.Controls.Add(KpiCard("单据总数", _kpiCount, Theme.Primary, 0), 0, 0);
        row.Controls.Add(KpiCard("销售金额", _kpiAmount, Theme.Accent, 1), 1, 0);
        row.Controls.Add(KpiCard("客单价", _kpiAvg, Theme.Warn, 2), 2, 0);
        row.Controls.Add(KpiCard("销售数量", _kpiQty, Color.FromArgb(123, 97, 255), 3), 3, 0);
        return row;
    }

    private Control KpiCard(string caption, Label value, Color accent, int index)
    {
        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(index == 0 ? 0 : 5, 0, index == 3 ? 0 : 5, 0),
            Padding = new Padding(18, 14, 18, 14)
        };

        var bar = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
        var cap = new Label
        {
            Text = caption,
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Theme.TextSub,
            Font = Theme.Font(9f)
        };
        value.Dock = DockStyle.Fill;
        value.Font = Theme.Font(18f, FontStyle.Bold);
        value.ForeColor = Theme.TextMain;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Text = "0";

        card.Controls.Add(value);
        card.Controls.Add(cap);
        card.Controls.Add(bar);
        return card;
    }

    private Control BuildTabs()
    {
        var card = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.BodyFont;
        _tabs.Padding = new Point(14, 6);

        _tabs.TabPages.Add(MakeTab("按客户汇总", _byCustomer,
            ("CustomerName", "客户名称", 200, "left"),
            ("Count", "单据数", 80, "right"),
            ("Qty", "数量合计", 100, "right"),
            ("Amount", "金额合计", 120, "right"),
            ("Percent", "占比", 80, "right")));

        _tabs.TabPages.Add(MakeTab("按商品汇总", _byProduct,
            ("ProductName", "商品名称", 220, "left"),
            ("Qty", "销售数量", 110, "right"),
            ("Amount", "销售金额", 130, "right")));

        _tabs.TabPages.Add(MakeTab("按月汇总", _byMonth,
            ("Month", "月份", 140, "left"),
            ("Count", "单据数", 100, "right"),
            ("Amount", "销售金额", 140, "right")));

        card.Controls.Add(_tabs);
        return card;
    }

    private static TabPage MakeTab(string title, DataGridView grid,
        params (string prop, string header, int fill, string align)[] cols)
    {
        var page = new TabPage(title) { BackColor = Color.White, Padding = new Padding(10) };
        Theme.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AutoGenerateColumns = false;
        grid.MultiSelect = false;

        foreach (var (prop, header, fill, align) in cols)
        {
            var c = new DataGridViewTextBoxColumn
            {
                Name = prop,
                DataPropertyName = prop,
                HeaderText = header,
                FillWeight = fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            c.DefaultCellStyle.Alignment = align switch
            {
                "right" => DataGridViewContentAlignment.MiddleRight,
                "center" => DataGridViewContentAlignment.MiddleCenter,
                _ => DataGridViewContentAlignment.MiddleLeft
            };
            grid.Columns.Add(c);
        }

        if (grid.Columns["Amount"] is { } amt)
        {
            amt.DefaultCellStyle.Format = "N2";
            amt.DefaultCellStyle.ForeColor = Theme.PrimaryDark;
            amt.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);
        }
        if (grid.Columns["Qty"] is { } qty)
            qty.DefaultCellStyle.Format = "N3";

        page.Controls.Add(grid);
        return page;
    }

    public void Reload()
    {
        var from = _from.Value.Date;
        var to = _to.Value.Date;

        var byCustomer = _repo.SalesByCustomer(from, to);
        var total = byCustomer.Sum(x => x.Amount);

        _byCustomer.DataSource = byCustomer.Select(x => new
        {
            CustomerName = string.IsNullOrWhiteSpace(x.Name) ? "(未填写客户)" : x.Name,
            x.Count,
            x.Qty,
            x.Amount,
            Percent = total == 0 ? "0.0%" : (x.Amount / total * 100).ToString("0.0") + "%"
        }).ToList();

        _byProduct.DataSource = _repo.SalesByProduct(from, to)
            .Select(x => new { ProductName = x.Name, x.Qty, x.Amount }).ToList();

        _byMonth.DataSource = _repo.MonthlySummary(from, to)
            .Select(x => new { x.Month, x.Count, x.Amount }).ToList();

        var (sum, count) = _repo.Summary(from, to);
        _kpiCount.Text = count.ToString("N0");
        _kpiAmount.Text = "¥ " + sum.ToString("N2");
        _kpiAvg.Text = count == 0 ? "¥ 0.00" : "¥ " + (sum / count).ToString("N2");

        var qty = _repo.SalesByProduct(from, to).Sum(x => x.Qty);
        _kpiQty.Text = qty.ToString("N3");
    }
}
