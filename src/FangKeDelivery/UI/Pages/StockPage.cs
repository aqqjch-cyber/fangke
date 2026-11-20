using FangKeDelivery.Core;
using FangKeDelivery.Data;

namespace FangKeDelivery.UI.Pages;

/// <summary>库存管理：现存量、低库存预警、库存流水、手工盘点。</summary>
public sealed class StockPage : UserControl, IRefreshable
{
    private readonly Repository _repo;
    private readonly MainForm _main;

    private readonly TabControl _tabs = new();
    private readonly TextBox _stockKeyword = Theme.Input();
    private readonly CheckBox _onlyLow = new() { Text = "只看低于安全库存", AutoSize = true };
    private readonly DataGridView _stockGrid = new();
    private readonly Label _stockSummary = new();

    private readonly TextBox _moveKeyword = Theme.Input();
    private readonly ComboBox _moveType = new();
    private readonly DataGridView _moveGrid = new();
    private readonly Label _moveSummary = new();

    private List<StockRow> _rows = new();
    private List<StockMove> _moves = new();

    public StockPage(Repository repo, MainForm main)
    {
        _repo = repo;
        _main = main;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = Theme.BodyFont;
        _tabs.Padding = new Point(16, 6);

        var stockTab = new TabPage("库存现存量") { BackColor = Theme.PageBack, Padding = new Padding(0, 8, 0, 0) };
        stockTab.Controls.Add(BuildStockTab());

        var moveTab = new TabPage("库存流水") { BackColor = Theme.PageBack, Padding = new Padding(0, 8, 0, 0) };
        moveTab.Controls.Add(BuildMoveTab());

        _tabs.TabPages.Add(stockTab);
        _tabs.TabPages.Add(moveTab);
        Controls.Add(_tabs);
    }

    private Control BuildStockTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var filter = new Theme.RoundedPanel
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

        _stockKeyword.Width = 220;
        _stockKeyword.PlaceholderText = "商品名称 / 规格";
        _stockKeyword.Margin = new Padding(0, 3, 10, 0);

        var search = Theme.PrimaryButton("查询", 76, 30);
        search.Margin = new Padding(0, 3, 8, 0);
        search.Click += (_, _) => ReloadStock();

        _onlyLow.Font = Theme.BodyFont;
        _onlyLow.ForeColor = Theme.TextSub;
        _onlyLow.Margin = new Padding(0, 7, 12, 0);
        _onlyLow.CheckedChanged += (_, _) => ReloadStock();

        var adjust = Theme.SecondaryButton("盘点调整", 100, 30);
        adjust.Margin = new Padding(0, 3, 8, 0);
        adjust.Click += (_, _) => AdjustSelected();

        var safety = Theme.SecondaryButton("设置安全库存", 120, 30);
        safety.Margin = new Padding(0, 3, 0, 0);
        safety.Click += (_, _) => SetSafetySelected();

        flow.Controls.AddRange(new Control[] { _stockKeyword, search, _onlyLow, adjust, safety });
        filter.Controls.Add(flow);

        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };
        var head = new Label
        {
            Text = "库存现存量",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Theme.StyleGrid(_stockGrid);
        _stockGrid.Dock = DockStyle.Fill;
        _stockGrid.ReadOnly = true;
        _stockGrid.AutoGenerateColumns = false;
        _stockGrid.MultiSelect = false;
        AddCol(_stockGrid, "ProductName", "商品名称", 200);
        AddCol(_stockGrid, "Spec", "规格", 130);
        AddCol(_stockGrid, "Unit", "单位", 60, "center");
        AddCol(_stockGrid, "QtyText", "现存量", 90, "right");
        AddCol(_stockGrid, "SafetyText", "安全库存", 90, "right");
        AddCol(_stockGrid, "Amount", "库存金额", 110, "right");
        AddCol(_stockGrid, "Flag", "预警", 80, "center");
        _stockGrid.Columns["Amount"]!.DefaultCellStyle.Format = "N2";
        _stockGrid.Columns["QtyText"]!.DefaultCellStyle.Font = Theme.Font(9.5f, FontStyle.Bold);

        card.Controls.Add(_stockGrid);
        card.Controls.Add(head);

        var bar = new Theme.RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8) };
        _stockSummary.Dock = DockStyle.Fill;
        _stockSummary.TextAlign = ContentAlignment.MiddleLeft;
        _stockSummary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _stockSummary.ForeColor = Theme.TextMain;
        _stockSummary.Text = "共 0 种商品";
        bar.Controls.Add(_stockSummary);

        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(bar, 0, 2);
        return root;
    }

    private Control BuildMoveTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filter = new Theme.RoundedPanel
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

        _moveKeyword.Width = 220;
        _moveKeyword.PlaceholderText = "商品 / 单号 / 备注";
        _moveKeyword.Margin = new Padding(0, 3, 10, 0);

        _moveType.DropDownStyle = ComboBoxStyle.DropDownList;
        _moveType.Width = 110;
        _moveType.Font = Theme.BodyFont;
        _moveType.Margin = new Padding(0, 3, 10, 0);
        _moveType.Items.AddRange(new object[] { "全部类型", "入库", "出库", "盘点" });
        _moveType.SelectedIndex = 0;
        _moveType.SelectedIndexChanged += (_, _) => ReloadMoves();

        var search = Theme.PrimaryButton("查询", 76, 30);
        search.Margin = new Padding(0, 3, 0, 0);
        search.Click += (_, _) => ReloadMoves();

        flow.Controls.AddRange(new Control[] { _moveKeyword, _moveType, search });
        filter.Controls.Add(flow);

        var card = new Theme.RoundedPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10)
        };
        var head = new Label
        {
            Text = "库存流水（最近 500 条）",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Font(11f, FontStyle.Bold),
            ForeColor = Theme.TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Theme.StyleGrid(_moveGrid);
        _moveGrid.Dock = DockStyle.Fill;
        _moveGrid.ReadOnly = true;
        _moveGrid.AutoGenerateColumns = false;
        AddCol(_moveGrid, "CreatedAt", "时间", 130, "center");
        AddCol(_moveGrid, "ProductName", "商品名称", 170);
        AddCol(_moveGrid, "Spec", "规格", 110);
        AddCol(_moveGrid, "TypeText", "类型", 70, "center");
        AddCol(_moveGrid, "QtyText", "变动", 80, "right");
        AddCol(_moveGrid, "QtyAfterText", "结存", 80, "right");
        AddCol(_moveGrid, "SourceNo", "来源单号", 130);
        AddCol(_moveGrid, "Operator", "操作人", 90);
        AddCol(_moveGrid, "Remark", "说明", 160);

        _moveSummary.Dock = DockStyle.Bottom;
        _moveSummary.Height = 30;
        _moveSummary.TextAlign = ContentAlignment.MiddleLeft;
        _moveSummary.Font = Theme.Font(9.5f, FontStyle.Bold);
        _moveSummary.ForeColor = Theme.TextMain;
        _moveSummary.Text = "共 0 条流水";

        card.Controls.Add(_moveGrid);
        card.Controls.Add(_moveSummary);
        card.Controls.Add(head);

        root.Controls.Add(filter, 0, 0);
        root.Controls.Add(card, 0, 1);
        return root;
    }

    private static void AddCol(DataGridView g, string prop, string header, int fill, string align = "left")
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
        g.Columns.Add(c);
    }

    public void Reload()
    {
        ReloadStock();
        ReloadMoves();
    }

    private void ReloadStock()
    {
        _rows = _repo.GetStock(_stockKeyword.Text, _onlyLow.Checked);

        _stockGrid.DataSource = _rows.Select(s => new
        {
            s.ProductId,
            s.ProductName,
            s.Spec,
            s.Unit,
            QtyText = s.Qty.ToString("0.###"),
            SafetyText = s.SafetyQty > 0 ? s.SafetyQty.ToString("0.###") : "—",
            s.Amount,
            Flag = s.IsLow ? "⚠ 偏低" : ""
        }).ToList();

        var total = _rows.Sum(r => r.Amount);
        var low = _rows.Count(r => r.IsLow);
        _stockSummary.Text = $"共 {_rows.Count} 种商品 · 库存总额 {total:N2} 元" +
                             (low > 0 ? $" · ⚠ {low} 种低于安全库存" : "");
        _stockSummary.ForeColor = low > 0 ? Theme.Warn : Theme.TextMain;
    }

    private void ReloadMoves()
    {
        StockMoveType? type = _moveType.SelectedIndex switch
        {
            1 => StockMoveType.入库,
            2 => StockMoveType.出库,
            3 => StockMoveType.盘点,
            _ => null
        };

        _moves = _repo.QueryStockMoves(_moveKeyword.Text, null, null, type);

        _moveGrid.DataSource = _moves.Select(m => new
        {
            m.CreatedAt,
            m.ProductName,
            m.Spec,
            TypeText = m.Type.ToString(),
            QtyText = (m.Qty >= 0 ? "+" : "") + m.Qty.ToString("0.###"),
            QtyAfterText = m.QtyAfter.ToString("0.###"),
            m.SourceNo,
            m.Operator,
            m.Remark
        }).ToList();

        _moveSummary.Text = $"共 {_moves.Count} 条流水 · 入库 {_moves.Count(m => m.Qty > 0)} 笔 · 出库 {_moves.Count(m => m.Qty < 0)} 笔";
    }

    private StockRow? SelectedRow()
    {
        if (_stockGrid.SelectedRows.Count == 0) return null;
        var bound = _stockGrid.SelectedRows[0].DataBoundItem;
        if (bound is null) return null;
        var id = (long)bound.GetType().GetProperty("ProductId")!.GetValue(bound)!;
        return _rows.FirstOrDefault(r => r.ProductId == id);
    }

    private void AdjustSelected()
    {
        var row = SelectedRow();
        if (row is null)
        {
            Msg.Show(FindForm(), "请先在列表中选择一种商品。", "盘点调整",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new StockAdjustDialog(row, _repo);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        try
        {
            _repo.AdjustStock(row.ProductId, dlg.NewQty, dlg.Remark, _main.CurrentUserName);
            ReloadStock();
            ReloadMoves();
            _main.SetStatus($"已将「{row.ProductName}」库存调整为 {dlg.NewQty:0.###}");
        }
        catch (Exception ex)
        {
            Msg.Show(FindForm(), "盘点失败：" + ex.Message, "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetSafetySelected()
    {
        var row = SelectedRow();
        if (row is null)
        {
            Msg.Show(FindForm(), "请先在列表中选择一种商品。", "安全库存",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var input = PromptDialog.Ask(FindForm(), "设置安全库存",
            $"「{row.ProductName}」的安全库存数量（低于或等于该值时会预警，填 0 表示不预警）：",
            row.SafetyQty.ToString("0.###"));
        if (input is null) return;

        if (!decimal.TryParse(input, out var value) || value < 0)
        {
            Msg.Show(FindForm(), "请输入不小于 0 的数字。", "安全库存",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _repo.SetSafetyQty(row.ProductId, value);
        ReloadStock();
        _main.SetStatus($"「{row.ProductName}」安全库存已设为 {value:0.###}");
    }
}
